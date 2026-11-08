using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RszTool.App.ViewModels;

namespace RszTool.App.Views
{
    public partial class ResourceInfoListView : UserControl
    {
        private bool updatingSelection;

        public ResourceInfoListView()
        {
            InitializeComponent();
        }

        private void ResourceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingSelection || DataContext is not UserFileViewModel viewModel) return;
            updatingSelection = true;
            viewModel.SelectedResourceInfos.Clear();
            foreach (ResourceInfo item in ResourceListView.SelectedItems)
            {
                viewModel.SelectedResourceInfos.Add(item);
            }
            viewModel.SelectedResourceInfoAnchor = ResourceListView.SelectedItem as ResourceInfo;
            updatingSelection = false;
        }

        private void ResourceListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResourceListView.SelectedItem is ResourceInfo resourceInfo)
            {
                Clipboard.SetText(resourceInfo.Path ?? string.Empty);
                e.Handled = true;
            }
        }
    }
}
