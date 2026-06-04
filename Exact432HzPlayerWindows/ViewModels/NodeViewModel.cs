using System.Collections.ObjectModel;

namespace Exact432HzPlayerWindows.ViewModels
{
    public class NodeViewModel : ViewModelBase
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        
        public ObservableCollection<NodeViewModel> Children { get; set; } = new ObservableCollection<NodeViewModel>();
    }
}
