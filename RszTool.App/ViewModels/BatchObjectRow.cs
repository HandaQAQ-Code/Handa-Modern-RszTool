using System.ComponentModel;

namespace RszTool.App.ViewModels;

public sealed class BatchObjectRow : INotifyPropertyChanged
{
    private bool isChecked = true;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IGameObjectData Object { get; }
    public string Path { get; }
    public string Name => Object.Name ?? "未命名子对象";
    public string GroupName { get; }
    public int ContextCount { get; private set; }
    public int StageCount { get; private set; }
    public int ItemCount { get; private set; }
    public string ItemSummary { get; private set; } = "";
    public string DropFlagSummary { get; private set; } = "";
    public bool IsChecked
    {
        get => isChecked;
        set { if (isChecked == value) return; isChecked = value; PropertyChanged?.Invoke(this, new(nameof(IsChecked))); }
    }
    public BatchObjectRow(string path, IGameObjectData gameObject, EnumDict enums)
    {
        Path = path; Object = gameObject;
        int separator = path.LastIndexOf('/');
        GroupName = $"{(separator < 0 ? "当前集合" : path.Substring(0, separator))} / {Name}";
        Refresh(enums);
    }
    public void Refresh(EnumDict enums)
    {
        ContextCount = Re4IdEditor.Contexts(Re4IdEditor.ExactObjectRoots([(Path, Object)]).Select(x => x.Instance)).Count;
        var fields = Re4IdEditor.PlanExact([(Path, Object)], null, null, 0, 0, 0);
        StageCount = fields.Count(x => Re4IdEditor.FieldType(x.Instance, x.Instance.Fields[x.FieldIndex]) == "chainsaw.StageID");
        var items = fields.Where(x => x.Instance.Fields[x.FieldIndex].name == "_DropItemID").ToList();
        ItemCount = items.Count;
        ItemSummary = string.Join("、", items.Select(x => EnumLabel(enums, "chainsaw.ItemID", x.OldValue)).Distinct().Take(4));
        var flags = Re4IdEditor.PlanExact([(Path, Object)], null, null, 0, null, null, false, false);
        DropFlagSummary = string.Join("；", flags.Select(x => $"{x.Field}={x.OldValue}").Distinct());
        foreach (string property in new[] { nameof(Name), nameof(ContextCount), nameof(StageCount), nameof(ItemCount), nameof(ItemSummary), nameof(DropFlagSummary) })
            PropertyChanged?.Invoke(this, new(property));
    }
    public static string EnumLabel(EnumDict enums, string type, object value)
    {
        if (value is bool flag) return flag ? "True（开启）" : "False（关闭）";
        if (enums[type] == null) return value.ToString() ?? "";
        ulong number = value is int signed ? unchecked((ulong)(uint)signed) : Convert.ToUInt64(value);
        string? name = enums[type]?.Items.FirstOrDefault(x => x.Value == number)?.Name;
        return name == null ? value.ToString() ?? "" : $"{name} ({value})";
    }
}
