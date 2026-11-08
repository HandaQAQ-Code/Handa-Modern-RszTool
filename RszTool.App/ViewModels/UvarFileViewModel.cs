using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using RszTool.App.Common;
using RszTool.App.Views;
using RszTool.Common;

namespace RszTool.App.ViewModels;

public sealed class UvarFileViewModel : BaseRszFileViewModel
{
    private static readonly Lazy<UvarUsageStore> usage = new(() => new UvarUsageStore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RszTool", "uvar-flag-usage.json")));
    private readonly ResettableObservableCollection<UvarFlagRow> rows = new();
    public IEnumerable<UvarFlagRow> AllRows => rows;
    public event Action<UvarFlagRow>? RevealRow;
    public UvarFile UvarFile { get; }
    public override BaseRszFile File => UvarFile;
    public override IEnumerable<object> TreeViewItems => [];
    public ICollectionView Flags { get; }
    public ObservableCollection<UvarGroupChoice> Groups { get; } = new();
    public List<UvarFlagRow> SelectedFlags { get; } = new();
    public string UsagePath => usage.Value.StorePath;
    public string SelectionSummary => $"共 {rows.Count} 个变量 / {rows.Count(x => x.Variable.IsFlag)} 个 flag，已选 {SelectedFlags.Count} 项";
    public RelayCommand CreateFlags => new(_ => AppUtils.TryActionSimple(AddFlags));
    public RelayCommand BatchCreateFlags => new(_ => AppUtils.TryActionSimple(AddNumberedFlags));
    public RelayCommand DeleteFlags => new(_ => AppUtils.TryActionSimple(RemoveFlags));
    public RelayCommand MarkUsed => new(_ => AppUtils.TryActionSimple(() => Mark(true)));
    public RelayCommand MarkUnused => new(_ => AppUtils.TryActionSimple(() => Mark(false)));
    public RelayCommand SetUsageNote => new(_ => AppUtils.TryActionSimple(SetNote));
    private string search = "";
    public string Search { get => search; set { search = value; Flags.Refresh(); } }
    private int usageFilter;
    public int UsageFilter { get => usageFilter; set { usageFilter = value; Flags.Refresh(); } }
    private bool flagsOnly = true;
    public bool FlagsOnly { get => flagsOnly; set { flagsOnly = value; Flags.Refresh(); } }
    private UvarGroupChoice? selectedGroup;
    public UvarGroupChoice? SelectedGroup { get => selectedGroup; set { selectedGroup = value; Flags.Refresh(); } }

    public UvarFileViewModel(UvarFile file)
    {
        UvarFile = file;
        Flags = CollectionViewSource.GetDefaultView(rows);
        Flags.Filter = item => item is UvarFlagRow row &&
            (!FlagsOnly || row.Variable.IsFlag) &&
            (SelectedGroup?.Group == null || row.Group == SelectedGroup.Group) &&
            (UsageFilter == 0 || row.Used == (UsageFilter == 1)) &&
            (string.IsNullOrWhiteSpace(Search) || new[] { row.Name, row.GroupPath, row.Id, row.Note }
                .Any(x => x.IndexOf(Search, StringComparison.OrdinalIgnoreCase) >= 0));
    }
    public override void PostRead()
    {
        rows.Clear(); Groups.Clear(); SelectedFlags.Clear();
        Groups.Add(new(null));
        foreach (var group in UvarFile.Groups)
        {
            Groups.Add(new(group));
            foreach (var entry in group.Variables) rows.Add(MakeRow(group, entry));
        }
        SelectedGroup = Groups[0];
        OnPropertyChanged(nameof(SelectionSummary));
    }
    private UvarFlagRow MakeRow(UvarGroup group, UvarVariable variable)
    {
        var stored = usage.Value.Get(FilePath!, variable.Id);
        return new UvarFlagRow(group, variable, stored.Used, stored.Note, () => Changed = true);
    }
    public void SelectionChanged(IEnumerable<UvarFlagRow> selection)
    {
        SelectedFlags.Clear(); SelectedFlags.AddRange(selection);
        OnPropertyChanged(nameof(SelectionSummary));
    }
    private void AddFlags()
    {
        var group = SelectedGroup?.Group ?? (SelectedFlags.Select(x => x.Group).Distinct().Count() == 1 ? SelectedFlags[0].Group : null);
        if (group == null) throw new ArgumentException("请先选择目标分组，或选中目标分组中的一个 flag。");
        var dialog = new MultiLineInputDialog { Owner = Application.Current.MainWindow, Title = "创建 flag",
            Message = $"分组：{group.Path}\n每行一个 flag 名称。新 flag 的默认值为 False，使用标记默认为未使用。" };
        if (dialog.ShowDialog() != true) return;
        var names = dialog.InputText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (names.Count == 0) return;
        InsertFlags(group, names, false);
    }
    private void AddNumberedFlags()
    {
        var group = SelectedGroup?.Group ?? (SelectedFlags.Select(x => x.Group).Distinct().Count() == 1 ? SelectedFlags[0].Group : null);
        if (group == null) throw new ArgumentException("请先选择目标分组，或选中目标分组中的一个 flag。");
        var dialog = new BatchFlagCreateWindow(group, SelectedFlags.LastOrDefault(x => x.Group == group)?.Name) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        InsertFlags(group, dialog.Names, dialog.DefaultValue);
    }
    private void InsertFlags(UvarGroup group, IEnumerable<string> names, bool defaultValue)
    {
        var added = UvarFile.AddFlags(group, names, defaultValue);
        // Notify the selector once, outside DeferRefresh, after the entire batch is ready.
        var replacement = rows.Concat(added.Select(entry => MakeRow(group, entry))).ToList();
        Changed = true;
        rows.ReplaceAll(replacement);
        Search = "";
        UsageFilter = 0;
        SelectedGroup = Groups.First(x => x.Group == group);
        Changed = true;
        OnPropertyChanged(nameof(SelectionSummary));
        if (added.Count > 0) RevealRow?.Invoke(rows.First(x => x.Variable == added[0]));
    }

    public void MarkChosen(UvarFlagRow row, string note)
    {
        usage.Value.SetMany(FilePath!, [(row.Variable.Id, true, note)]);
        row.Used = true;
        row.Note = note;
        Flags.Refresh();
    }
    private void RemoveFlags()
    {
        var selected = SelectedFlags.Where(x => x.Variable.IsFlag).ToList();
        if (selected.Count == 0) return;
        if (!MessageBoxUtils.Confirm($"删除选中的 {selected.Count} 个 flag（其中 {selected.Count(x => x.Used)} 个标为已使用）？\n其他游戏文件中的引用需要你同步处理。保存文件时会自动备份。")) return;
        foreach (var row in selected) { row.Group.Variables.Remove(row.Variable); rows.Remove(row); }
        SelectedFlags.Clear();
        Changed = true;
        OnPropertyChanged(nameof(SelectionSummary));
    }
    private void Mark(bool used)
    {
        var selected = SelectedFlags.Where(x => x.Variable.IsFlag).ToList();
        usage.Value.SetMany(FilePath!, selected.Select(x => (x.Variable.Id, used, x.Note)));
        foreach (var row in selected) row.Used = used;
        Flags.Refresh();
    }
    private void SetNote()
    {
        var selected = SelectedFlags.Where(x => x.Variable.IsFlag).ToList();
        if (selected.Count == 0) return;
        var dialog = new MultiLineInputDialog { Owner = Application.Current.MainWindow, Title = "设置使用备注",
            Message = $"为选中的 {selected.Count} 个 flag 设置相同备注（留空可清除）。", InputText = selected[0].Note };
        if (dialog.ShowDialog() != true) return;
        usage.Value.SetMany(FilePath!, selected.Select(x => (x.Variable.Id, x.Used, dialog.InputText)));
        foreach (var row in selected) row.Note = dialog.InputText;
        Flags.Refresh();
    }
    public override bool SaveAs(string path)
    {
        string source = FilePath!;
        if (!base.SaveAs(path)) return false;
        usage.Value.CopyFileMarks(source, path);
        return true;
    }
}

public sealed class UvarGroupChoice(UvarGroup? group)
{
    public UvarGroup? Group { get; } = group;
    public string Name => Group?.Path ?? "全部分组";
}

public sealed class UvarFlagRow(UvarGroup group, UvarVariable variable, bool used, string note, Action changed) : INotifyPropertyChanged
{
    public UvarGroup Group { get; } = group;
    public UvarVariable Variable { get; } = variable;
    public string GroupPath => Group.Path;
    public string Name => Variable.Name;
    public string Id => Variable.Id.ToString();
    public string Type => Variable.IsFlag ? "Boolean flag" : $"Type {Variable.TypeBits & 0xffffff}";
    public bool Used { get; set; } = used;
    public string Note { get; set; } = note;
    public bool DefaultValue
    {
        get => Variable.DefaultValue;
        set { if (Variable.IsFlag && Variable.DefaultValue != value) { Variable.DefaultValue = value; changed(); } }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
