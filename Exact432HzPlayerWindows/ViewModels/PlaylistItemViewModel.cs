namespace Exact432HzPlayerWindows.ViewModels
{
    public class PlaylistItemViewModel : ViewModelBase
    {
        private string _indexStr = string.Empty;
        public string IndexStr
        {
            get => _indexStr;
            set => SetProperty(ref _indexStr, value);
        }

        private string _durationStr = string.Empty;
        public string DurationStr
        {
            get => _durationStr;
            set => SetProperty(ref _durationStr, value);
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public string FullPath { get; set; } = string.Empty;
    }
}
