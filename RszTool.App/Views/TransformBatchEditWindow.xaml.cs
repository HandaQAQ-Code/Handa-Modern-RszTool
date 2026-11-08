using System.Windows;

namespace RszTool.App.Views
{
    /// <summary>
    /// TransformBatchEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TransformBatchEditWindow : Wpf.Ui.Controls.FluentWindow
    {
        public TransformBatchEditWindow()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
