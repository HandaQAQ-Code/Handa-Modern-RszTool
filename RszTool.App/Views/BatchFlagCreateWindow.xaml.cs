using System.Globalization;
using System.Windows;
using RszTool.App.Common;
using RszTool.Common;

namespace RszTool.App.Views;

public partial class BatchFlagCreateWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly UvarGroup group;
    public List<string> Names { get; private set; } = new();
    public bool DefaultValue => DefaultInput.IsChecked == true;
    public BatchFlagCreateWindow(UvarGroup group, string? selectedName)
    {
        InitializeComponent();
        this.group = group;
        GroupText.Text = $"目标分组：{group.Path}";
        string? reference = selectedName != null && System.Text.RegularExpressions.Regex.IsMatch(selectedName, "[0-9]+")
            ? selectedName : group.Variables.LastOrDefault(x => x.IsFlag && System.Text.RegularExpressions.Regex.IsMatch(x.Name, "[0-9]+"))?.Name;
        ReferenceInput.Text = reference ?? group.Name + "_000";
        if (reference == null) StartInput.Text = "0";
        else ContinueSequence();
        Preview();
    }
    private void ContinueSequence() => StartInput.Text = new FlagNameSequence(ReferenceInput.Text.Trim()).Next(group.Variables.Select(x => x.Name)).ToString(CultureInfo.InvariantCulture);
    private List<string> Plan()
    {
        var names = new FlagNameSequence(ReferenceInput.Text.Trim()).Generate(
            long.Parse(StartInput.Text, CultureInfo.InvariantCulture),
            int.Parse(CountInput.Text, CultureInfo.InvariantCulture),
            long.Parse(StepInput.Text, CultureInfo.InvariantCulture));
        if (names.Count > short.MaxValue - group.Variables.Count) throw new ArgumentException("创建数量超过分组剩余容量。");
        var hashes = new HashSet<uint>(group.Variables.Select(x => MurMur3HashUtils.GetHash(x.Name)));
        foreach (string name in names)
            if (name.IndexOf('\0') >= 0 || !hashes.Add(MurMur3HashUtils.GetHash(name))) throw new ArgumentException($"名称重复、无效或哈希冲突：{name}");
        return names;
    }
    private void Preview()
    {
        try
        {
            var names = Plan();
            NamesList.ItemsSource = names;
            StatusText.Text = $"将创建 {names.Count} 个 flag：{names[0]} → {names[names.Count - 1]}";
        }
        catch (Exception error) { NamesList.ItemsSource = null; StatusText.Text = error.Message; }
    }
    private void Continue_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() => { ContinueSequence(); Preview(); });
    private void Preview_Click(object sender, RoutedEventArgs e) => Preview();
    private void Create_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() => { Names = Plan(); DialogResult = true; });
}
