using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Exact432HzPlayerWindows.Services;

namespace Exact432HzPlayerWindows.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioPlayerService _playerService;
        private readonly DispatcherTimer _timer;
        private bool _isPlaying = false;
        private bool _isDraggingSlider = false;

        public ObservableCollection<NodeViewModel> RootNodes { get; set; } = new ObservableCollection<NodeViewModel>();
        
        public ObservableCollection<PlaylistItemViewModel> Playlist { get; set; } = new ObservableCollection<PlaylistItemViewModel>();

        private PlaylistItemViewModel? _selectedPlaylistItem;
        public PlaylistItemViewModel? SelectedPlaylistItem
        {
            get => _selectedPlaylistItem;
            set => SetProperty(ref _selectedPlaylistItem, value);
        }

        private string _nowPlayingText = "Now Playing: None";
        public string NowPlayingText
        {
            get => _nowPlayingText;
            set => SetProperty(ref _nowPlayingText, value);
        }

        private string _currentBaseFrequency = "Base Freq: N/A";
        public string CurrentBaseFrequency
        {
            get => _currentBaseFrequency;
            set => SetProperty(ref _currentBaseFrequency, value);
        }

        public double Volume
        {
            get => _playerService.Volume;
            set
            {
                if (Math.Abs(_playerService.Volume - value) > 0.001)
                {
                    _playerService.Volume = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        private double _currentPositionSeconds;
        public double CurrentPositionSeconds
        {
            get => _currentPositionSeconds;
            set
            {
                if (SetProperty(ref _currentPositionSeconds, value))
                {
                    CurrentTimeDisplay = TimeSpan.FromSeconds(value).ToString(@"mm\:ss");
                }
            }
        }

        private double _totalDurationSeconds;
        public double TotalDurationSeconds
        {
            get => _totalDurationSeconds;
            set
            {
                if (SetProperty(ref _totalDurationSeconds, value))
                {
                    TotalTimeDisplay = TimeSpan.FromSeconds(value).ToString(@"mm\:ss");
                }
            }
        }

        private string _currentTimeDisplay = "00:00";
        public string CurrentTimeDisplay
        {
            get => _currentTimeDisplay;
            set => SetProperty(ref _currentTimeDisplay, value);
        }

        private string _totalTimeDisplay = "00:00";
        public string TotalTimeDisplay
        {
            get => _totalTimeDisplay;
            set => SetProperty(ref _totalTimeDisplay, value);
        }

        public bool IsTreeViewEmpty => RootNodes.Count == 0;

        public RelayCommand SetParentFolderCommand { get; }
        public RelayCommand ExitCommand { get; }
        public RelayCommand ClearCacheCommand { get; }
        
        public RelayCommand PlayPauseCommand { get; }
        public RelayCommand StopCommand { get; }
        public RelayCommand NextCommand { get; }
        public RelayCommand PreviousCommand { get; }
        public RelayCommand ReShuffleCommand { get; }
        public RelayCommand UnShuffleCommand { get; }

        public RelayCommand PlayFolderCommand { get; }
        public RelayCommand ShufflePlayFolderCommand { get; }
        public RelayCommand PlayPlaylistItemCommand { get; }
        public RelayCommand ShowDetailCommand { get; }

        public MainViewModel()
        {
            _playerService = new AudioPlayerService();
            _playerService.PlaybackEnded += (s, e) => 
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => ExecuteNext()));
            };

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            SetParentFolderCommand = new RelayCommand(_ => ExecuteSetParentFolder());
            ExitCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
            ClearCacheCommand = new RelayCommand(_ => ExecuteClearCache());

            PlayPauseCommand = new RelayCommand(_ => ExecutePlayPause(), _ => SelectedPlaylistItem != null);
            StopCommand = new RelayCommand(_ => ExecuteStop(), _ => _isPlaying);
            NextCommand = new RelayCommand(_ => ExecuteNext(), _ => Playlist.Count > 0);
            PreviousCommand = new RelayCommand(_ => ExecutePrevious(), _ => Playlist.Count > 0);
            ReShuffleCommand = new RelayCommand(_ => ExecuteReShuffle(), _ => Playlist.Count > 0);
            UnShuffleCommand = new RelayCommand(_ => ExecuteUnShuffle(), _ => Playlist.Count > 0);

            PlayFolderCommand = new RelayCommand(param => ExecuteLoadFolder((NodeViewModel)param!, false));
            ShufflePlayFolderCommand = new RelayCommand(param => ExecuteLoadFolder((NodeViewModel)param!, true));
            PlayPlaylistItemCommand = new RelayCommand(param => 
            {
                if (param is PlaylistItemViewModel item)
                {
                    SelectedPlaylistItem = item;
                    PlayItem(item);
                }
            });

            ShowDetailCommand = new RelayCommand(param => 
            {
                if (param is PlaylistItemViewModel item)
                {
                    string details = AudioPlayerService.GetAudioDetails(item.FullPath);
                    System.Windows.MessageBox.Show(details, "Audio Details", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
            
            LoadSettings();
        }

        private static string GetSettingsFilePath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = System.IO.Path.Combine(appData, "Exact432HzPlayerWindows");
            System.IO.Directory.CreateDirectory(folder);
            return System.IO.Path.Combine(folder, "settings.json");
        }

        private string _lastFolder = string.Empty;

        private void LoadSettings()
        {
            var path = GetSettingsFilePath();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(path);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                    if (settings != null)
                    {
                        if (settings.TryGetValue("Volume", out string? volStr) && double.TryParse(volStr, out double vol))
                        {
                            Volume = vol;
                        }

                        if (settings.TryGetValue("LastFolder", out string? lastFolder) && System.IO.Directory.Exists(lastFolder))
                        {
                            _lastFolder = lastFolder;
                            RootNodes.Clear();
                            var nodes = FolderScanner.BuildTree(lastFolder);
                            foreach (var node in nodes) RootNodes.Add(node);
                            OnPropertyChanged(nameof(IsTreeViewEmpty));
                        }
                    }
                }
                catch { }
            }
        }

        private void SaveSettings()
        {
            try
            {
                var path = GetSettingsFilePath();
                var settings = new System.Collections.Generic.Dictionary<string, string> 
                { 
                    { "LastFolder", _lastFolder },
                    { "Volume", Volume.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                };
                var json = System.Text.Json.JsonSerializer.Serialize(settings);
                System.IO.File.WriteAllText(path, json);
            }
            catch { }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_isPlaying && !_isDraggingSlider)
            {
                CurrentPositionSeconds = _playerService.GetPositionSeconds();
            }
        }

        private void ExecuteSetParentFolder()
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Select Parent Folder";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _lastFolder = dialog.SelectedPath;
                SaveSettings();
                RootNodes.Clear();
                var nodes = FolderScanner.BuildTree(dialog.SelectedPath);
                foreach (var node in nodes) RootNodes.Add(node);
                OnPropertyChanged(nameof(IsTreeViewEmpty));
            }
        }

        private void ExecuteClearCache()
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to delete the Base Frequency and Volume Caches?", "Clear Cache", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                AudioAnalyzer.ClearCache();
                System.Windows.MessageBox.Show("Cache cleared.");
            }
        }

        private void ExecuteLoadFolder(NodeViewModel folder, bool shuffle)
        {
            var files = FolderScanner.GetAudioFilesRecursively(folder.FullPath);
            if (shuffle)
            {
                var rand = new Random();
                files = files.OrderBy(x => rand.Next()).ToList();
            }
            else
            {
                files = files.OrderBy(x => x).ToList();
            }

            Playlist.Clear();
            var itemsToLoad = new System.Collections.Generic.List<PlaylistItemViewModel>();
            for (int i = 0; i < files.Count; i++)
            {
                var item = new PlaylistItemViewModel
                {
                    IndexStr = (i + 1).ToString(),
                    FileName = System.IO.Path.GetFileName(files[i]),
                    FullPath = files[i],
                    DurationStr = "--:--"
                };
                Playlist.Add(item);
                itemsToLoad.Add(item);
            }

            if (Playlist.Count > 0)
            {
                SelectedPlaylistItem = Playlist.First();
                PlayItem(SelectedPlaylistItem);
            }

            System.Threading.Tasks.Task.Run(() => 
            {
                foreach (var item in itemsToLoad)
                {
                    var dur = AudioPlayerService.GetDurationString(item.FullPath);
                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() => item.DurationStr = $"[{dur}]");
                }
            });
        }

        private void ExecuteReShuffle()
        {
            if (Playlist.Count == 0) return;
            var rand = new Random();
            var shuffled = Playlist.OrderBy(x => rand.Next()).ToList();
            Playlist.Clear();
            for (int i = 0; i < shuffled.Count; i++)
            {
                shuffled[i].IndexStr = (i + 1).ToString();
                Playlist.Add(shuffled[i]);
            }
            if (Playlist.Count > 0)
            {
                SelectedPlaylistItem = Playlist.First();
                PlayItem(SelectedPlaylistItem);
            }
        }

        private void ExecuteUnShuffle()
        {
            if (Playlist.Count == 0) return;
            var ordered = Playlist.OrderBy(x => x.FullPath).ToList();
            Playlist.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].IndexStr = (i + 1).ToString();
                Playlist.Add(ordered[i]);
            }
            if (Playlist.Count > 0)
            {
                SelectedPlaylistItem = Playlist.First();
                PlayItem(SelectedPlaylistItem);
            }
        }

        private async void PlayItem(PlaylistItemViewModel item)
        {
            NowPlayingText = $"Analyzing: {item.FileName}...";
            double maxFreq = await AudioAnalyzer.AnalyzeToneAsync(item.FullPath);
            double peakVolume = await AudioAnalyzer.AnalyzePeakVolumeAsync(item.FullPath);
            
            // Calculate multiplier for peak normalization to 99% (cap at 10.0 to prevent extreme noise)
            double volumeMultiplier = Math.Min(0.99 / Math.Max(peakVolume, 0.01), 10.0);
            
            _playerService.Play(item.FullPath, maxFreq, volumeMultiplier);
            _isPlaying = true;
            TotalDurationSeconds = _playerService.GetTotalSeconds();
            NowPlayingText = $"Now Playing: {item.FileName}";
            CurrentBaseFrequency = $"Base Freq: {maxFreq}Hz";
            
            // Trigger background analysis for the NEXT item in playlist to ensure gapless preparation
            int index = Playlist.IndexOf(item);
            if (index >= 0 && index + 1 < Playlist.Count)
            {
                var nextItem = Playlist[index + 1];
                _ = AudioAnalyzer.AnalyzeToneAsync(nextItem.FullPath);
                _ = AudioAnalyzer.AnalyzePeakVolumeAsync(nextItem.FullPath);
            }
        }

        private void ExecutePlayPause()
        {
            if (_isPlaying)
            {
                _playerService.Pause();
                _isPlaying = false;
            }
            else
            {
                _playerService.Resume();
                _isPlaying = true;
            }
        }

        private void ExecuteStop()
        {
            _playerService.Stop();
            _isPlaying = false;
            CurrentPositionSeconds = 0;
        }

        private void ExecuteNext()
        {
            if (SelectedPlaylistItem == null) return;
            int index = Playlist.IndexOf(SelectedPlaylistItem);
            if (index >= 0 && index < Playlist.Count - 1)
            {
                SelectedPlaylistItem = Playlist[index + 1];
                PlayItem(SelectedPlaylistItem);
            }
            else
            {
                ExecuteStop();
            }
        }

        private void ExecutePrevious()
        {
            if (SelectedPlaylistItem == null) return;
            int index = Playlist.IndexOf(SelectedPlaylistItem);
            if (index > 0)
            {
                SelectedPlaylistItem = Playlist[index - 1];
                PlayItem(SelectedPlaylistItem);
            }
        }

        public void NotifyDragStarted() => _isDraggingSlider = true;
        public void NotifyDragCompleted()
        {
            _playerService.SetPositionSeconds(CurrentPositionSeconds);
            _isDraggingSlider = false;
        }
    }
}
