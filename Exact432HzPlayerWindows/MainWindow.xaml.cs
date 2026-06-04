using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Exact432HzPlayerWindows.ViewModels;

namespace Exact432HzPlayerWindows
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_MEDIA_NEXTTRACK = 11;
        private const int APPCOMMAND_MEDIA_PREVIOUSTRACK = 12;
        private const int APPCOMMAND_MEDIA_STOP = 13;
        private const int APPCOMMAND_MEDIA_PLAY_PAUSE = 14;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var source = PresentationSource.FromVisual(this) as HwndSource;
            source?.AddHook(WndProc);

            var hwnd = new WindowInteropHelper(this).Handle;
            int useImmersiveDarkMode = 1;
            int hr = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            if (hr != 0)
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useImmersiveDarkMode, sizeof(int));
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_APPCOMMAND)
            {
                int cmd = (int)((uint)lParam >> 16 & 0xFFFF);
                switch (cmd)
                {
                    case APPCOMMAND_MEDIA_PLAY_PAUSE:
                        if (_viewModel.PlayPauseCommand.CanExecute(null))
                            _viewModel.PlayPauseCommand.Execute(null);
                        handled = true;
                        break;
                    case APPCOMMAND_MEDIA_STOP:
                        if (_viewModel.StopCommand.CanExecute(null))
                            _viewModel.StopCommand.Execute(null);
                        handled = true;
                        break;
                    case APPCOMMAND_MEDIA_NEXTTRACK:
                        if (_viewModel.NextCommand.CanExecute(null))
                            _viewModel.NextCommand.Execute(null);
                        handled = true;
                        break;
                    case APPCOMMAND_MEDIA_PREVIOUSTRACK:
                        if (_viewModel.PreviousCommand.CanExecute(null))
                            _viewModel.PreviousCommand.Execute(null);
                        handled = true;
                        break;
                }
            }
            return IntPtr.Zero;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Nothing to do, bound directly for ContextMenu
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeView treeView && treeView.SelectedItem is NodeViewModel node)
            {
                if (_viewModel.PlayFolderCommand.CanExecute(node))
                {
                    _viewModel.PlayFolderCommand.Execute(node);
                }
            }
        }

        private void Playlist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is PlaylistItemViewModel item)
            {
                if (_viewModel.PlayPlaylistItemCommand.CanExecute(item))
                {
                    _viewModel.PlayPlaylistItemCommand.Execute(item);
                }
            }
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _viewModel.NotifyDragStarted();
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (sender is Slider slider)
                slider.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();
                
            _viewModel.NotifyDragCompleted();
        }

        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Slider slider)
                slider.GetBindingExpression(Slider.ValueProperty)?.UpdateSource();

            _viewModel.NotifyDragCompleted();
        }
    }
}