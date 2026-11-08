using System.Windows;
using Microsoft.Win32;
using RszTool.App.Common;

namespace RszTool.App.Views;

public partial class UvarSettingsWindow : Wpf.Ui.Controls.FluentWindow
{
    public UvarSettingsWindow()
    {
        InitializeComponent();
        PathInput.Text = App.Instance.SaveData.UvarFilePath;
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "UVAR v3|*.uvar.3", FileName = PathInput.Text, CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) PathInput.Text = dialog.FileName;
    }
    private void Save_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        if (string.IsNullOrWhiteSpace(PathInput.Text)) throw new ArgumentException("请先选择一个 uvar.3 文件。");
        if (App.Instance.UvarWorkspace.Configure(PathInput.Text)) DialogResult = true;
    });
}
