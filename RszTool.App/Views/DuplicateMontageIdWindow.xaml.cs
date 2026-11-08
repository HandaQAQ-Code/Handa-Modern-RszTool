using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App.Views;

public partial class DuplicateMontageIdWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly List<MontageIdPoolViewModel> sources;
    private readonly ObservableCollection<MontageIdPreset> presets;
    public bool Randomize => RandomizeInput.IsChecked == true;
    public Dictionary<string, List<uint>> ResolvedPools { get; private set; } = new();

    public DuplicateMontageIdWindow(IEnumerable<MontageIdSource> sources)
    {
        this.sources = sources.Select(x => new MontageIdPoolViewModel(x)).ToList();
        presets = new(App.Instance.SaveData.MontageIdPresets);
        foreach (var source in this.sources)
        {
            App.Instance.SaveData.MontageIdPools.TryGetValue(source.Key, out var stored);
            source.Load(stored);
        }
        InitializeComponent();
        SourceList.ItemsSource = this.sources;
        PresetPicker.ItemsSource = presets;
        PresetPicker.Text = App.Instance.SaveData.LastMontageIdPreset;
        if (this.sources.Count > 0) SourceList.SelectedIndex = 0;
        StatusText.Text = $"已读取 {this.sources.Count} 个 MontageID 来源，默认候选来自各自的枚举下拉框。";
    }
    private void SourceList_SelectionChanged(object sender, SelectionChangedEventArgs e) => PoolEditor.DataContext = SourceList.SelectedItem;
    private Dictionary<string, List<uint>> ResolvePools() => sources.ToDictionary(x => x.Key, x => x.Resolve());
    private static MontageIdPoolSetting Copy(MontageIdPoolSetting setting) => new() { IncludeEnumValues = setting.IncludeEnumValues, CustomIds = setting.CustomIds };
    private void RememberCurrent()
    {
        foreach (var source in sources) App.Instance.SaveData.MontageIdPools[source.Key] = source.Setting();
        App.Instance.SaveData.LastMontageIdPreset = PresetPicker.Text.Trim();
        App.Instance.SaveSettings();
    }
    private void OKButton_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        ResolvedPools = Randomize ? ResolvePools() : new();
        if (Randomize && ResolvedPools.Values.All(x => x.Count == 0)) throw new ArgumentException("随机池为空，请包含下拉枚举或添加自定义 MontageID。");
        RememberCurrent();
        DialogResult = true;
    });
    private void LoadPreset_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        var preset = presets.FirstOrDefault(x => x.Name == PresetPicker.Text.Trim()) ?? throw new ArgumentException("请选择已有预设。");
        foreach (var source in sources)
        {
            preset.Pools.TryGetValue(source.Key, out var setting);
            source.Load(setting);
        }
        StatusText.Text = $"已载入预设：{preset.Name}。预设中未记录的类型使用默认全部枚举。";
    });
    private void SavePreset_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        _ = ResolvePools();
        string name = PresetPicker.Text.Trim();
        if (name.Length == 0) throw new ArgumentException("请输入预设名称。");
        var existing = presets.FirstOrDefault(x => x.Name == name);
        if (existing != null && !MessageBoxUtils.Confirm($"覆盖 MontageID 预设“{name}”？")) return;
        var pools = existing?.Pools.ToDictionary(x => x.Key, x => Copy(x.Value)) ?? new();
        foreach (var source in sources) pools[source.Key] = source.Setting();
        var replacement = new MontageIdPreset { Name = name, Pools = pools };
        if (existing == null) presets.Add(replacement);
        else presets[presets.IndexOf(existing)] = replacement;
        App.Instance.SaveData.MontageIdPresets = presets.ToList();
        PresetPicker.SelectedItem = replacement;
        RememberCurrent();
        StatusText.Text = $"预设“{name}”已保存。";
    });
    private void DeletePreset_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        var preset = presets.FirstOrDefault(x => x.Name == PresetPicker.Text.Trim());
        if (preset == null) return;
        if (!MessageBoxUtils.Confirm($"删除 MontageID 预设“{preset.Name}”？")) return;
        presets.Remove(preset);
        App.Instance.SaveData.MontageIdPresets = presets.ToList();
        if (App.Instance.SaveData.LastMontageIdPreset == preset.Name) App.Instance.SaveData.LastMontageIdPreset = "";
        PresetPicker.SelectedItem = null;
        PresetPicker.Text = "";
        App.Instance.SaveSettings();
        StatusText.Text = "预设已删除，当前随机池配置仍保留。";
    });
    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var source in sources) source.Load(null);
        PresetPicker.SelectedItem = null;
        PresetPicker.Text = "";
        StatusText.Text = "已恢复为各下拉框的全部 MontageID，并清除本次配置中的额外 ID。";
    }
}
