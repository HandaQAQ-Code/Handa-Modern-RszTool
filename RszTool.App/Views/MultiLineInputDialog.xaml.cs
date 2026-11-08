using System.Windows;

namespace RszTool.App.Views
{
    public partial class MultiLineInputDialog : Wpf.Ui.Controls.FluentWindow
    {
        public string Message { get; set; } = "";
        public string InputText { get; set; } = "";

        public MultiLineInputDialog()
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
