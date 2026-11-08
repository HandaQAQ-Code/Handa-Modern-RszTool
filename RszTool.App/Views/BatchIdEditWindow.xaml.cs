using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App.Views;

public partial class BatchIdEditWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly List<IGameObjectData> roots;
    private readonly BaseRszFileViewModel document;
    private readonly ResettableObservableCollection<BatchObjectRow> rows = new();
    private readonly ObservableCollection<DropWeightSetting> weights = new();
    private ListCollectionView objectView = null!;
    private List<DropAssignment>? dropPreview;
    private List<IdFieldEdit> dropFlagPreview = new();
    private string dropSignature = "";
    private bool ready;
    private bool updatingRows;
    public EnumDict EnumDict { get; }
    public int AppliedCount { get; private set; }
    public static readonly DependencyProperty StageValueProperty = DependencyProperty.Register(nameof(StageValue), typeof(int), typeof(BatchIdEditWindow), new PropertyMetadata(0));
    public static readonly DependencyProperty ItemValueProperty = DependencyProperty.Register(nameof(ItemValue), typeof(int), typeof(BatchIdEditWindow), new PropertyMetadata(0));
    public int StageValue { get => (int)GetValue(StageValueProperty); set => SetValue(StageValueProperty, value); }
    public int ItemValue { get => (int)GetValue(ItemValueProperty); set => SetValue(ItemValueProperty, value); }

    public BatchIdEditWindow(IEnumerable<IGameObjectData> roots, BaseRszFileViewModel document, string scope, bool collection)
    {
        this.roots = roots.Distinct().ToList();
        this.document = document;
        EnumDict = document.File.Option.EnumParser.EnumDict;
        InitializeComponent();
        DataContext = document;
        DetailTree.DataContext = document;
        ScopeText.Text = scope;
        CollapseNames.IsChecked = App.Instance.SaveData.BatchGroupSameNameObjects;
        objectView = new ListCollectionView(rows);
        objectView.Filter = item => item is BatchObjectRow row && (string.IsNullOrWhiteSpace(ObjectSearch.Text) ||
            row.Name.IndexOf(ObjectSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0 ||
            row.Path.IndexOf(ObjectSearch.Text, StringComparison.OrdinalIgnoreCase) >= 0);
        ObjectList.ItemsSource = objectView;
        DropGrid.ItemsSource = weights;
        foreach (var stored in App.Instance.SaveData.DropWeights)
            weights.Add(new DropWeightSetting { ItemId = stored.ItemId, Weight = stored.Weight });
        if (weights.Count == 0)
        {
            var firstItem = EnumDict["chainsaw.ItemID"]?.Items.FirstOrDefault(x => x.Value > 0 && x.Value <= int.MaxValue);
            weights.Add(new DropWeightSetting { ItemId = firstItem == null ? 0 : (int)firstItem.Value, Weight = 1 });
        }
        var last = App.Instance.SaveData.LastContextID;
        GroupInput.Text = last.Group.ToString();
        IndexInput.Text = (last.Index < int.MaxValue ? last.Index + 1 : last.Index).ToString();
        SeedInput.Text = new Random().Next().ToString(CultureInfo.InvariantCulture);
        ready = true;
        GroupRows();
        Scan();
        Closing += (_, _) => AppUtils.TryActionSimple(RememberOptions);
    }

    private void RememberOptions()
    {
        App.Instance.SaveData.BatchGroupSameNameObjects = CollapseNames.IsChecked == true;
        if (!DropGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !DropGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;
        App.Instance.SaveData.DropWeights = weights.Select(x => new DropWeightSetting { ItemId = x.ItemId, Weight = x.Weight }).ToList();
        App.Instance.SaveSettings();
    }

    private void Scan()
    {
        if (!ready) return;
        var previous = rows.ToDictionary(x => x.Object, x => x.IsChecked);
        IEnumerable<(string Path, IGameObjectData Object)> targets = ScopeMode.SelectedIndex == 1
            ? roots.Select((x, i) => ($"{x.Name}[{i}]", x))
            : Re4IdEditor.GameObjects(roots, ScopeMode.SelectedIndex != 2);
        // Prepare everything before touching the bound list; a failed scan preserves the old rows.
        var replacement = targets.Select(target =>
        {
            var row = new BatchObjectRow(target.Path, target.Object, EnumDict);
            if (previous.TryGetValue(target.Object, out bool isChecked)) row.IsChecked = isChecked;
            return row;
        }).ToList();
        updatingRows = true;
        try
        {
            foreach (var row in rows) row.PropertyChanged -= RowChanged;
            foreach (var row in replacement) row.PropertyChanged += RowChanged;
            rows.ReplaceAll(replacement);
            DetailTree.ItemsSource = null;
            DetailTitle.Text = "从左侧选择物体，复用原始子物体编辑器";
        }
        finally { updatingRows = false; }
        dropPreview = null;
        PreviewGrid.ItemsSource = null;
        ScanText.Text = $"已扫描 {rows.Count} 个物体 / 子对象；ContextID {rows.Sum(x => x.ContextCount)} 个，StageID {rows.Sum(x => x.StageCount)} 项，ItemID {rows.Sum(x => x.ItemCount)} 项。可展开同名分组逐个勾选。";
        if (rows.Count == 0) ScanText.Text += " 当前范围为空，请切换为“入口物体及全部子对象”或“仅入口物体”。";
        UpdateSelection();
        if (rows.Count > 0) ObjectList.SelectedItem = rows[0];
    }
    private void GroupRows()
    {
        if (!ready) return;
        using (objectView.DeferRefresh())
        {
            objectView.GroupDescriptions.Clear();
            if (CollapseNames.IsChecked == true) objectView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BatchObjectRow.GroupName)));
        }
    }
    private void RowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BatchObjectRow.IsChecked)) return;
        dropPreview = null;
        UpdateSelection();
    }
    private void UpdateSelection() => SelectionText.Text = $"扫描 {rows.Count} 个，勾选 {rows.Count(x => x.IsChecked)} 个，高亮 {ObjectList.SelectedItems.Count} 个。批量设置和随机化只处理勾选项，关闭后请在主窗口保存文件。";
    private List<(string Path, IGameObjectData Object)> Targets()
    {
        var selected = rows.Where(x => x.IsChecked).Select(x => (x.Path, x.Object)).ToList();
        if (selected.Count == 0) throw new ArgumentException("请在左侧物体列表勾选至少一个物体。");
        return selected;
    }
    private void SetChecks(IEnumerable<BatchObjectRow> items, bool value)
    {
        foreach (var row in items.ToList()) row.IsChecked = value;
        UpdateSelection();
    }
    private void ScopeMode_Changed(object sender, SelectionChangedEventArgs e) { if (ready) Scan(); }
    private void Rescan_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(Scan);
    private void CollapseNames_Click(object sender, RoutedEventArgs e) => GroupRows();
    private void ObjectSearch_Changed(object sender, TextChangedEventArgs e) { if (ready) objectView.Refresh(); }
    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetChecks(rows, true);
    private void SelectNone_Click(object sender, RoutedEventArgs e) => SetChecks(rows, false);
    private void CheckHighlighted_Click(object sender, RoutedEventArgs e) => SetChecks(ObjectList.SelectedItems.Cast<BatchObjectRow>(), true);
    private void UncheckHighlighted_Click(object sender, RoutedEventArgs e) => SetChecks(ObjectList.SelectedItems.Cast<BatchObjectRow>(), false);
    private void CheckGroup_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is CollectionViewGroup group) SetChecks(group.Items.OfType<BatchObjectRow>(), true); }
    private void UncheckGroup_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is CollectionViewGroup group) SetChecks(group.Items.OfType<BatchObjectRow>(), false); }
    private void ObjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || updatingRows) return;
        if ((e.AddedItems.OfType<BatchObjectRow>().LastOrDefault() ?? ObjectList.SelectedItem as BatchObjectRow) is BatchObjectRow row)
        {
            DetailTitle.Text = row.Path;
            DetailTree.ItemsSource = new[] { row.Object };
        }
        UpdateSelection();
    }
    private void Detail_ValueChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        document.Changed = true;
        AppliedCount++;
        dropPreview = null;
        foreach (var row in rows) row.Refresh(EnumDict);
        StatusText.Text = "原始物体字段已修改，关闭窗口后请保存文件。";
    }
    private void Detail_ResourceChanged(object sender, RoutedEventArgs e)
    {
        if (document is ScnFileViewModel scn) scn.ResourceChanged = true;
        if (document is PfbFileViewModel pfb) pfb.ResourceChanged = true;
    }

    private void CopyFieldToChecked_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        if ((sender as MenuItem)?.CommandParameter is not IFieldValueViewModel source) return;
        int? arrayIndex = source is RszFieldArrayNormalItemViewModel arrayItem ? arrayItem.Index : null;
        var prepared = new List<(RszInstance Instance, int FieldIndex, int? ArrayIndex, object Value, string Path, object OldValue)>();
        foreach (var (path, instance) in Re4IdEditor.Walk(Re4IdEditor.ExactObjectRoots(Targets())))
        {
            if (instance.RszClass.name != source.OwnerClassName) continue;
            int index = instance.RszClass.IndexOfField(source.Field.name);
            if (index < 0 || instance.Fields[index].type != source.Field.type || instance.Fields[index].array != source.Field.array) continue;
            object oldValue;
            if (arrayIndex is int element)
            {
                if (instance.Values[index] is not List<object> values || element >= values.Count) continue;
                oldValue = values[element];
            }
            else oldValue = instance.Values[index];
            object value = source.Value is byte[] bytes ? bytes.Clone() : source.Value;
            if (value is byte[] newData && oldValue is byte[] oldData && newData.Length != oldData.Length) throw new ArgumentException("二进制字段长度不一致，已取消整批修改。");
            if (value.GetType() != oldValue.GetType()) value = Convert.ChangeType(value, oldValue.GetType(), CultureInfo.InvariantCulture);
            prepared.Add((instance, index, arrayIndex, value, path + "/" + source.Field.name, oldValue));
        }
        PreviewGrid.ItemsSource = prepared.Select(x => new { x.Path, Field = source.Field.name,
            OldValue = x.OldValue.ToString(), NewValue = x.Value.ToString() }).ToList();
        if (prepared.Count == 0) { StatusText.Text = "已勾选物体中没有同类型、同名字段或同序号数组项。"; return; }
        if (!MessageBoxUtils.Confirm($"将 {source.OwnerClassName}.{source.Field.name} 的当前值应用到已勾选物体中的 {prepared.Count} 个匹配字段？")) return;
        foreach (var change in prepared)
        {
            if (change.ArrayIndex is int element) ((List<object>)change.Instance.Values[change.FieldIndex])[element] = change.Value;
            else change.Instance.SetFieldValue(change.FieldIndex, change.Value);
        }
        document.Changed = true;
        AppliedCount += prepared.Count;
        dropPreview = null;
        foreach (var row in rows) row.Refresh(EnumDict);
        if (source.Value is string resource && RszUtils.IsResourcePath(resource)) Detail_ResourceChanged(sender, e);
        StatusText.Text = $"已批量修改 {prepared.Count} 个同类字段，关闭后请保存文件。";
        RefreshDetails();
    });

    internal static int Number(string text)
    {
        text = text.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.Parse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : int.Parse(text, CultureInfo.InvariantCulture);
    }
    private List<IdFieldEdit> IdPlan()
    {
        if (SetContext.IsChecked != true && SetStage.IsChecked != true && SetItem.IsChecked != true &&
            !ShouldDropInput.IsChecked.HasValue && !ShouldDropRandomInput.IsChecked.HasValue)
            throw new ArgumentException("请勾选要批量设置的 ID 类型，或为掉落开关设置开启 / 关闭。");
        return Re4IdEditor.PlanExact(Targets(), SetContext.IsChecked == true ? Number(GroupInput.Text) : null,
            SetContext.IsChecked == true ? Number(IndexInput.Text) : null, SetContext.IsChecked == true ? Number(StepInput.Text) : 0,
            SetStage.IsChecked == true ? StageValue : null, SetItem.IsChecked == true ? ItemValue : null,
            ShouldDropInput.IsChecked, ShouldDropRandomInput.IsChecked);
    }
    private object PreviewRow(IdFieldEdit edit)
    {
        string type = Re4IdEditor.FieldType(edit.Instance, edit.Instance.Fields[edit.FieldIndex]);
        return new { edit.Path, edit.Field, OldValue = BatchObjectRow.EnumLabel(EnumDict, type, edit.OldValue), NewValue = BatchObjectRow.EnumLabel(EnumDict, type, edit.NewValue) };
    }
    private void DisplayPlan(IReadOnlyList<IdFieldEdit> edits) => PreviewGrid.ItemsSource = edits.Select(PreviewRow).ToList();
    private void ApplyEdits(List<IdFieldEdit> edits)
    {
        if (edits.Count == 0) { StatusText.Text = "勾选的物体没有匹配字段。"; return; }
        Re4IdEditor.Apply(edits);
        AppliedCount += edits.Count;
        document.Changed = true;
        foreach (var row in rows) row.Refresh(EnumDict);
        dropPreview = null;
        StatusText.Text = $"已修改 {edits.Count} 个字段。可继续编辑，关闭后请在主窗口保存文件。";
        RefreshDetails();
    }
    private void RefreshDetails()
    {
        if (ObjectList.SelectedItem is not BatchObjectRow row) return;
        DetailTree.ItemsSource = null;
        DetailTree.ItemsSource = new[] { row.Object };
    }
    private void Preview_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        var edits = IdPlan(); DisplayPlan(edits);
        StatusText.Text = $"勾选 {Targets().Count} 个物体，预览 {edits.Count} 个字段修改。";
    });
    private void Apply_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        var edits = IdPlan(); DisplayPlan(edits);
        if (edits.Count == 0 || !MessageBoxUtils.Confirm($"将修改已勾选物体的 {edits.Count} 个字段，是否应用？")) return;
        ApplyEdits(edits);
        var last = edits.LastOrDefault(x => x.Instance.RszClass.name == "chainsaw.ContextID" && x.Field == "_Index");
        if (last != null) { App.Instance.SaveData.LastContextID.Group = Number(GroupInput.Text); App.Instance.SaveData.LastContextID.Index = (int)last.NewValue; }
    });

    private void AddDrop_Click(object sender, RoutedEventArgs e) => weights.Add(new DropWeightSetting { ItemId = ItemValue, Weight = 1 });
    private void RemoveDrop_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in DropGrid.SelectedItems.Cast<DropWeightSetting>().ToList()) weights.Remove(row);
    }
    private void NewSeed_Click(object sender, RoutedEventArgs e)
    {
        SeedInput.Text = new Random().Next().ToString(CultureInfo.InvariantCulture);
        dropPreview = null;
        AppUtils.TryActionSimple(PreviewDrops);
    }
    private string DropSignature()
    {
        if (!DropGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !DropGrid.CommitEdit(DataGridEditingUnit.Row, true) ||
            weights.Any(x => x.Weight < 0)) throw new ArgumentException("请检查掉落比例输入，比例不能为负数。");
        return string.Join("|", Targets().Select(x => x.Path)) + ";" + ShouldDropInput.IsChecked + ";" + ShouldDropRandomInput.IsChecked + ";" + SeedInput.Text + ";" +
            string.Join("|", weights.Select(x => x.ItemId.ToString(CultureInfo.InvariantCulture) + ":" + x.Weight.ToString(CultureInfo.InvariantCulture)));
    }
    private void PreviewDrops()
    {
        dropSignature = DropSignature();
        var selected = Targets();
        dropPreview = DropItemRandomizer.Plan(selected, weights.Select(x => new DropChoice(x.ItemId, x.Weight)), Number(SeedInput.Text));
        dropFlagPreview = Re4IdEditor.PlanExact(selected, null, null, 0, null, null, ShouldDropInput.IsChecked, ShouldDropRandomInput.IsChecked);
        DisplayPlan(dropPreview.SelectMany(x => x.Edits).Concat(dropFlagPreview).ToList());
        StatusText.Text = $"勾选 {selected.Count} 个，为 {dropPreview.Sum(x => x.ObjectCount)} 个物体分配 {dropPreview.Count} 个独立掉落单元，跳过 {selected.Count - dropPreview.Sum(x => x.ObjectCount)} 个无匹配字段的物体。分配单元数量：" +
            string.Join("；", dropPreview.GroupBy(x => x.ItemId).Select(x => BatchObjectRow.EnumLabel(EnumDict, "chainsaw.ItemID", x.Key) + $" ×{x.Count()}"));
    }
    private void PreviewDrops_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(PreviewDrops);
    private void ApplyDrops_Click(object sender, RoutedEventArgs e) => AppUtils.TryActionSimple(() =>
    {
        if (dropPreview == null || dropSignature != DropSignature())
        {
            PreviewDrops();
            StatusText.Text += " 配置或范围已变化，请检查预览后再次点击应用。";
            return;
        }
        var edits = dropPreview.SelectMany(x => x.Edits).Concat(dropFlagPreview).ToList();
        if (edits.Any(x => !Equals(x.OldValue, x.ArrayIndex is int i ? ((List<object>)x.Instance.Values[x.FieldIndex])[i] : x.Instance.Values[x.FieldIndex])))
        {
            PreviewDrops(); StatusText.Text += " 原字段已变化，请检查新预览后再次应用。"; return;
        }
        if (!MessageBoxUtils.Confirm($"按当前预览为 {dropPreview.Count} 个勾选物体随机分配掉落物，修改 {edits.Count} 个字段？")) return;
        ApplyEdits(edits);
        RememberOptions();
    });
}
