using System.Windows;
using RszTool.App.ViewModels;

namespace RszTool.App.Views
{
    /// <summary>
    /// Interaction logic for FileAssociationWindow.xaml
    /// </summary>
    public partial class FileAssociationWindow : Wpf.Ui.Controls.FluentWindow
    {
        public FileAssociationViewModel ViewModel { get; }

        public FileAssociationWindow()
        {
            InitializeComponent();
            ViewModel = new FileAssociationViewModel();
            DataContext = ViewModel;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ApplyAssociations();
        }

        private void UnregisterAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.UnregisterAll();
        }
    }
}
