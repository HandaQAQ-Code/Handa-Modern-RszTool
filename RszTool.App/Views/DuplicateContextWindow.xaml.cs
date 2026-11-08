using System.Windows;
using RszTool.App.Common;

namespace RszTool.App.Views;

public partial class DuplicateContextWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly int contextCount;
    public int Count { get; private set; }
    public bool AutoContext { get; private set; }
    public int Group { get; private set; }
    public int StartIndex { get; private set; }
    public int Step { get; private set; }
    public DuplicateContextWindow(int contextCount)
    {
        InitializeComponent();
        this.contextCount = contextCount;
        ContextSummary.Text = $"每个副本找到 {contextCount} 个独立 ContextID。";
        AutoInput.IsEnabled = contextCount > 0;
        AutoInput.IsChecked = contextCount > 0;
        var last = App.Instance.SaveData.LastContextID;
        GroupInput.Text = last.Group.ToString();
        IndexInput.Text = (last.Index < int.MaxValue ? last.Index + 1 : last.Index).ToString();
    }
    private void OK_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        Count = BatchIdEditWindow.Number(CountInput.Text);
        if (Count < 1 || Count > 10000) throw new ArgumentException("副本数量必须在 1 到 10000 之间。");
        AutoContext = AutoInput.IsChecked == true;
        if (AutoContext)
        {
            Group = BatchIdEditWindow.Number(GroupInput.Text);
            StartIndex = BatchIdEditWindow.Number(IndexInput.Text);
            Step = BatchIdEditWindow.Number(StepInput.Text);
            if (Step == 0 && checked(Count * contextCount) > 1) throw new ArgumentException("自动递增的步长不能为 0。");
            _ = checked(StartIndex + checked(Step * (checked(Count * contextCount) - 1)));
        }
        DialogResult = true;
    });
}
