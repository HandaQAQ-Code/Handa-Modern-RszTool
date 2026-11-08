using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App.Views;

public partial class UvarGuidPickerWindow : Wpf.Ui.Controls.FluentWindow
{
    public UvarGuidPickerViewModel Model { get; }
    public UvarFlagRow? ChosenFlag { get; private set; }
    public bool MarkUsed => MarkUsedInput.IsChecked == true;
    public UvarGuidPickerWindow(UvarFileViewModel document, Guid currentValue)
    {
        InitializeComponent();
        Model = new(document, currentValue);
        DataContext = Model;
        Loaded += (_, _) => { if (Model.SelectedFlag != null) FlagGrid.ScrollIntoView(Model.SelectedFlag); };
        Closing += (_, _) => AppUtils.TryActionSimple(Model.RememberPosition);
    }
    private void Choose_Click(object sender, RoutedEventArgs e) => Choose();
    private void Choose() => AppUtils.TryActionSimple(() =>
    {
        var flag = Model.SelectedFlag;
        if (flag == null) return;
        if (flag.Used && !MessageBoxUtils.Confirm($"该 flag 已标记为使用过：{flag.Name}\n备注：{flag.Note}\n仍要使用它的 GUID 吗？")) return;
        if (Model.Document.Changed)
        {
            if (!MessageBoxUtils.Confirm("UVAR 有未保存修改。先保存 UVAR，再使用所选 GUID？")) return;
            if (!Model.Document.Save()) throw new System.IO.IOException("保存 UVAR 失败。");
        }
        ChosenFlag = flag;
        DialogResult = true;
    });
    private void Grid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(FlagGrid, source) is DataGridRow) Choose();
    }
}
