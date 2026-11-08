using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RszTool.App.ViewModels;

public sealed class MontageIdPoolViewModel(MontageIdSource source) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public MontageIdSource Source { get; } = source;
    public string Key => Source.Key;
    public string Title => Source.ToString();
    public EnumItem[] EnumItems => Source.EnumItems;
    private bool includeEnumValues = true;
    public bool IncludeEnumValues
    {
        get => includeEnumValues;
        set
        {
            if (includeEnumValues == value) return;
            includeEnumValues = value;
            PropertyChanged?.Invoke(this, new(nameof(IncludeEnumValues)));
            PropertyChanged?.Invoke(this, new(nameof(Summary)));
        }
    }
    private string customIds = "";
    public string CustomIds
    {
        get => customIds;
        set
        {
            if (customIds == value) return;
            customIds = value;
            PropertyChanged?.Invoke(this, new(nameof(CustomIds)));
            PropertyChanged?.Invoke(this, new(nameof(Summary)));
        }
    }
    public string Summary
    {
        get
        {
            try { return $"当前随机池：{Resolve().Count} 个不重复 ID（枚举与自定义 ID 合并去重）"; }
            catch (Exception error) { return error.Message; }
        }
    }
    public List<uint> Resolve()
    {
        var values = IncludeEnumValues ? EnumItems.Select(x => (uint)x.Value).ToList() : new List<uint>();
        foreach (string token in Regex.Split(CustomIds ?? "", @"[\s,，;；]+"))
        {
            if (string.IsNullOrWhiteSpace(token)) continue;
            bool hex = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!uint.TryParse(hex ? token.Substring(2) : token, hex ? NumberStyles.HexNumber : NumberStyles.None, CultureInfo.InvariantCulture, out uint id))
                throw new ArgumentException($"自定义 MontageID 无效：{token}。请输入 UInt32 十进制或 0x 十六进制 ID。");
            values.Add(id);
        }
        return values.Distinct().ToList();
    }
    public MontageIdPoolSetting Setting() => new() { IncludeEnumValues = IncludeEnumValues, CustomIds = CustomIds };
    public void Load(MontageIdPoolSetting? setting)
    {
        IncludeEnumValues = setting?.IncludeEnumValues ?? true;
        CustomIds = setting?.CustomIds ?? "";
    }
}
