using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.Win32;
using RszTool.App.Common;
using RszTool.App.Resources;


namespace RszTool.App.ViewModels
{
    public abstract class BaseRszFileViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public abstract BaseRszFile File { get; }
        public string? FilePath => File.FileHandler.FilePath;
        public string? FileName
        {
            get
            {
                string? path = FilePath;
                return path != null ? Path.GetFileName(path) : null;
            }
        }
        private bool changed;

        public bool Changed
        {
            get => changed;
            set
            {
                changed = value;
                HeaderChanged?.Invoke();
            }
        }
        public event Action<object?>? SelectedSearchResultChanged;
        public object? SelectedItem { get; set; }
        public ObservableCollection<ScnFile.GameObjectData> SelectedScnGameObjects { get; } = new();
        public ScnFile.GameObjectData? SelectedScnGameObjectAnchor { get; set; }
        public ObservableCollection<ScnFile.FolderData> SelectedScnFolders { get; } = new();
        public ScnFile.FolderData? SelectedScnFolderAnchor { get; set; }
        public ObservableCollection<ResourceInfo> SelectedResourceInfos { get; } = new();
        public ResourceInfo? SelectedResourceInfoAnchor { get; set; }
        private object? selectedSearchResult;
        public object? SelectedSearchResult
        {
            get => selectedSearchResult;
            set
            {
                selectedSearchResult = value;
                SelectedSearchResultChanged?.Invoke(value);
            }
        }
        public InstanceSearchViewModel InstanceSearchViewModel { get; } = new();
        public ObservableCollection<RszInstance>? SearchInstanceList { get; set; }
        public RszFileOption? RszFileOption => File.GetRSZ()?.Option;
        public EnumDict? EnumDict => RszFileOption?.EnumParser?.EnumDict;

        public static RelayCommand CopyInstance => new(OnCopyInstance);
        public RelayCommand PasteInstance => new(OnPasteInstance);
        public static RelayCommand CopyNormalField => new(OnCopyNormalField);
        public RelayCommand PasteNormalField => new(OnPasteNormalField);
        public static RelayCommand ArrayItemCopy => new(OnArrayItemCopy);
        public RelayCommand ArrayItemRemove => new(OnArrayItemRemove);
        public RelayCommand ArrayItemDuplicate => new(OnArrayItemDuplicate);
        public RelayCommand ArrayItemDuplicateMulti => new(OnArrayItemDuplicateMulti);
        public RelayCommand DuplicateRszObjectMulti => new(OnDuplicateRszObjectMulti);
        public RelayCommand BatchEditChildIds => new(OnBatchEditChildIds);
        public RelayCommand ArrayItemPasteAfter => new(OnArrayItemPasteAfter);
        public RelayCommand ArrayItemPasteToSelf => new(OnArrayItemPasteToSelf);
        public RelayCommand ArrayItemNew => new(OnArrayItemNew);
        public RelayCommand ArrayItemPaste => new(OnArrayItemPaste);
        public RelayCommand GenerateMercenariesSpawnPoints => new(OnGenerateMercenariesSpawnPoints);
        public RelayCommand ImportMercenariesSpawnPointsJson => new(OnImportMercenariesSpawnPointsJson);
        public RelayCommand ImportTransformPointsJson => new(OnImportTransformPointsJson);
        public RelayCommand PasteTransformV0FromClipboard => new(OnPasteTransformV0FromClipboard);
        public RelayCommand SearchInstances => new(OnSearchInstances);

        /// <summary>
        /// 标题改变(SaveAs或者Changed)
        /// </summary>
        public event Action? HeaderChanged;

        /// <summary>
        /// Item改变了，但Item本身不支持Notify，Items用了Convert的情况，通知TreeView更新
        /// </summary>
        // public event Action<object>? TreeViewItemChanged;

        public static RszInstance? CopiedInstance { get; private set; }
        public static RszField? CopiedNormalField { get; private set; }
        public static object? CopiedNormalValue { get; private set; }
        private const string MercenariesSpawnPointClassName = "chainsaw.CharacterSpawnPointMercenaries";
        private const string SpawnPointTransformFieldName = "_Transform";
        private const string SpawnPointOutOfCameraFieldName = "_IsOutOfCameraOnly";
        private static readonly Regex MercenariesSpawnPointInputRegex = new(
            @"[\(\uFF08]\s*(?<x>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*[,\uFF0C]\s*(?<y>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*[,\uFF0C]\s*(?<z>[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)\s*[,\uFF0C]\s*(?<b>[01])\s*[\)\uFF09]",
            RegexOptions.Compiled);

        public virtual void PostRead() {}

        public bool Read()
        {
            if (File.Read())
            {
                PostRead();
                Changed = false;
                return true;
            }
            return false;
        }

        public bool Save()
        {
            bool result = File.Save();
            if (result)
            {
                Changed = false;
            }
            return result;
        }

        public virtual bool SaveAs(string path)
        {
            bool result = File.SaveAs(path);
            if (result)
            {
                Changed = false;
                HeaderChanged?.Invoke();
                OnPropertyChanged(nameof(FilePath));
            }
            return result;
        }

        public bool Reopen()
        {
            // TODO check Changed
            File.FileHandler.Reopen();
            bool result = Read();
            OnPropertyChanged(nameof(TreeViewItems));
            return result;
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Update re4 chainsaw.ContextID
        /// </summary>
        /// <param name="gameObject"></param>
        private void UpdateInstanceContextID(RszInstance instance)
        {
            GameObjectCopyHelper.UpdateInstanceContextID(RszFileOption!, instance);
        }

        private static void OnCopyInstance(object arg)
        {
            if (arg is RszInstance instance)
            {
                CopiedInstance = instance;
            }
            else if (arg is RszFieldInstanceViewModel fieldInstanceViewModel)
            {
                CopiedInstance = fieldInstanceViewModel.Instance;
            }
            else if (arg is GameObejctComponentViewModel componentViewModel)
            {
                CopiedInstance = componentViewModel.Instance;
            }
        }

        private void OnPasteInstance(object arg)
        {
            if (arg is RszInstance instance)
            {
                if (DoPasteInstance(instance))
                {
                    Changed = true;
                }
            }
            else if (arg is RszFieldInstanceViewModel fieldInstanceViewModel)
            {
                if (DoPasteInstance(fieldInstanceViewModel.Instance))
                {
                    fieldInstanceViewModel.NotifyItemsChanged();
                    Changed = true;
                }
            }
            else if (arg is GameObejctComponentViewModel componentViewModel)
            {
                if (DoPasteInstance(componentViewModel.Instance))
                {
                    componentViewModel.NotifyItemsChanged();
                    Changed = true;
                }
            }
        }

        private bool DoPasteInstance(RszInstance instance)
        {
            if (CopiedInstance == null) return false;
            if (CopiedInstance.RszClass != instance.RszClass)
            {
                MessageBoxUtils.Error(string.Format(Texts.RszClassMismatch, CopiedInstance.RszClass.name, instance.RszClass.name));
                return false;
            }
            if (File.GetRSZ() is not RSZFile rsz) return false;
            bool result = rsz.InstanceCopyValues(instance, CopiedInstance);
            if (result)
            {
                UpdateInstanceContextID(instance);
            }
            return result;
        }

        private static void OnCopyNormalField(object arg)
        {
            if (arg is RszFieldNormalViewModel item)
            {
                CopiedNormalField = item.Field;
                CopiedNormalValue = item.Value;
            }
        }

        private void OnPasteNormalField(object arg)
        {
            if (arg is RszFieldNormalViewModel item && CopiedNormalField != null && CopiedNormalValue != null)
            {
                if (CopiedNormalField.type != item.Field.type)
                {
                    MessageBoxUtils.Error(string.Format(Texts.RszClassMismatch, CopiedNormalField.type, item.Field.type));
                    return;
                }
                item.Value = CopiedNormalValue;
                Changed = true;
            }
        }

        private static void OnArrayItemCopy(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                CopiedInstance = item.Instance;
                Console.WriteLine(CopiedInstance?.Stringify());
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem)
            {
                CopiedNormalField = normalItem.Field;
                CopiedNormalValue = normalItem.Value;
            }
        }

        /// <summary>
        /// 移除数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemRemove(object arg)
        {
            if (arg is BaseRszFieldArrayItemViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                rsz.ArrayRemoveItem(item.Values, item.Value);
                item.Array.NotifyItemsChanged();
                Changed = true;
            }
        }

        /// <summary>
        /// 重复数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemDuplicate(object arg)
        {
            if (File.GetRSZ() is not RSZFile rsz) return;
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                var newItem = rsz.ArrayInsertInstance(item.Values, item.Instance, item.Index + 1);
                UpdateInstanceContextID(newItem);
                item.Array.NotifyItemsChanged();
                Changed = true;
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem)
            {
                rsz.ArrayInsertItem(normalItem.Values, normalItem.Value, normalItem.Index + 1);
                normalItem.Array.NotifyItemsChanged();
                Changed = true;
            }
        }

        /// <summary>
        /// 重复多次数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemDuplicateMulti(object arg)
        {
            if (File.GetRSZ() is not RSZFile rsz) return;
            int contexts = arg is RszFieldArrayInstanceItemViewModel source && File.Option.GameName == GameName.re4
                ? Re4IdEditor.Contexts([source.Instance]).Count : 0;
            Views.DuplicateContextWindow dialog = new(contexts) { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() != true) return;
            int count = dialog.Count;
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                var copies = new List<RszInstance>();
                for (int i = 0; i < count; i++)
                {
                    copies.Add(rsz.ArrayInsertInstance(item.Values, item.Instance, item.Index + 1 + i));
                }
                ApplyDuplicateContexts(dialog, copies);
                item.Array.NotifyItemsChanged();
                Changed = true;
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem)
            {
                for (int i = 0; i < count; i++)
                {
                    rsz.ArrayInsertItem(normalItem.Values, normalItem.Value, normalItem.Index + 1);
                }
                normalItem.Array.NotifyItemsChanged();
                Changed = true;
            }
        }

        private void OnDuplicateRszObjectMulti(object arg)
        {
            if (File is not UserFile || File.GetRSZ() is not RSZFile rsz || arg is not RszInstance source || !rsz.ObjectList.Contains(source))
            {
                MessageBoxUtils.Info("请在 USER 文件的 Objects 列表中选择一个 RSZ object。数组对象请使用数组项的“重复多次”。");
                return;
            }
            Views.DuplicateContextWindow dialog = new(File.Option.GameName == GameName.re4 ? Re4IdEditor.Contexts([source]).Count : 0)
                { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() != true) return;
            var copies = new List<RszInstance>();
            for (int i = 0; i < dialog.Count; i++)
            {
                var copy = rsz.CloneInstance(source);
                rsz.AddToObjectTable(copy);
                copies.Add(copy);
            }
            ApplyDuplicateContexts(dialog, copies);
            Changed = true;
            OnPropertyChanged(nameof(TreeViewItems));
        }

        private static void ApplyDuplicateContexts(Views.DuplicateContextWindow dialog, List<RszInstance> copies)
        {
            if (!dialog.AutoContext) return;
            var contexts = Re4IdEditor.Contexts(copies);
            Re4IdEditor.SetContexts(contexts, dialog.Group, dialog.StartIndex, dialog.Step);
            if (contexts.Count > 0)
            {
                App.Instance.SaveData.LastContextID.Group = dialog.Group;
                App.Instance.SaveData.LastContextID.Index = checked(dialog.StartIndex + checked(dialog.Step * (contexts.Count - 1)));
            }
        }

        private void OnBatchEditChildIds(object arg)
        {
            if (File.Option.GameName != GameName.re4) { MessageBoxUtils.Info("此功能适用于 RE4 chainsaw 类型。"); return; }
            var roots = new List<IGameObjectData>();
            bool collection = true;
            string scope;
            switch (arg)
            {
                case IGameObjectData gameObject:
                    if (gameObject is ScnFile.GameObjectData selected && SelectedScnGameObjects.Contains(selected) && SelectedScnGameObjects.Count > 1)
                    {
                        roots.AddRange(SelectedScnGameObjects);
                        scope = $"多选物体：{roots.Count} 个";
                    }
                    else
                    {
                        roots.Add(gameObject);
                        collection = false;
                        scope = $"父物体：{gameObject.Name}";
                    }
                    break;
                case ScnGameObjectsHeader header:
                    roots.AddRange(header.Items?.OfType<IGameObjectData>() ?? []);
                    scope = header.Parent != null ? $"{header.Parent.Name} / 子对象集合" : header.Name;
                    break;
                case GameObjectsHeader header:
                    roots.AddRange(header.Items?.OfType<IGameObjectData>() ?? []);
                    scope = header.Name;
                    break;
                case ScnFile.FolderData folder:
                    var pending = new Stack<ScnFile.FolderData>();
                    var visited = new HashSet<ScnFile.FolderData>();
                    pending.Push(folder);
                    while (pending.Count > 0)
                    {
                        var next = pending.Pop();
                        if (!visited.Add(next)) continue;
                        roots.AddRange(next.GameObjects);
                        foreach (var child in next.Children.Reverse()) pending.Push(child);
                    }
                    scope = $"文件夹及子文件夹：{folder.Name}";
                    break;
                default:
                    MessageBoxUtils.Info("请选择物体、子对象集合或文件夹。");
                    return;
            }
            var dialog = new Views.BatchIdEditWindow(roots, this, scope, collection) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();
            if (dialog.AppliedCount > 0) Changed = true;
            if (Changed) OnPropertyChanged(nameof(TreeViewItems));
        }

        /// <summary>
        /// 在数组项后面粘贴
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemPasteAfter(object arg)
        {
            if (File.GetRSZ() is not RSZFile rsz) return;
            if (arg is RszFieldArrayInstanceItemViewModel item && CopiedInstance != null)
            {
                if (CopiedInstance.RszClass != item.Instance.RszClass)
                {
                    MessageBoxUtils.Error(string.Format(Texts.RszClassMismatch, CopiedInstance.RszClass.name, item.Instance.RszClass.name));
                    return;
                }
                var newItem = rsz.ArrayInsertInstance(item.Values, CopiedInstance, item.Index + 1);
                UpdateInstanceContextID(newItem);
                item.Array.NotifyItemsChanged();
                Changed = true;
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem
                && CopiedNormalField != null && CopiedNormalValue != null)
            {
                if (CopiedNormalField.type != normalItem.Field.type)
                {
                    MessageBoxUtils.Error(string.Format(Texts.RszClassMismatch, CopiedNormalField.type, normalItem.Field.type));
                    return;
                }
                rsz.ArrayInsertItem(normalItem.Values, CopiedNormalValue, normalItem.Index + 1);
                normalItem.Array.NotifyItemsChanged();
                Changed = true;
            }
        }

        /// <summary>
        /// 粘贴到自身
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemPasteToSelf(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                if (DoPasteInstance(item.Instance))
                {
                    item.NotifyItemsChanged();
                    Changed = true;
                }
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem
                && CopiedNormalField != null && CopiedNormalValue != null)
            {
                if (CopiedNormalField.type != normalItem.Field.type)
                {
                    MessageBoxUtils.Error(string.Format(Texts.RszClassMismatch, CopiedNormalField.type, normalItem.Field.type));
                    return;
                }
                normalItem.Value = CopiedNormalValue;
                Changed = true;
            }
        }

        /// <summary>
        /// 数组新建项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemNew(object arg)
        {
            if (arg is RszFieldArrayViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                string? className = null;
                if (item.Field.IsReference)
                {
                    className = RszInstance.GetElementType(item.Field.original_type);
                    Views.InputDialog dialog = new()
                    {
                        Title = Texts.NewItem,
                        Message = Texts.InputClassName,
                        InputText = className,
                        Owner = Application.Current.MainWindow,
                    };
                    if (dialog.ShowDialog() != true) return;
                    className = dialog.InputText;
                }
                var newItem = RszInstance.CreateArrayItem(rsz.RszParser, item.Field, className);
                if (newItem == null) return;
                if (newItem is RszInstance instance)
                {
                    rsz.ArrayInsertInstance(item.Values, instance, clone: false);
                }
                else
                {
                    rsz.ArrayInsertItem(item.Values, newItem);
                }
                item.NotifyItemsChanged();
                Changed = true;
            }
        }

        /// <summary>
        /// 粘贴到数组
        /// </summary>
        private void OnArrayItemPaste(object arg)
        {
            if (arg is RszFieldArrayViewModel item && CopiedInstance != null &&
                File.GetRSZ() is RSZFile rsz)
            {
                string className = RszInstance.GetElementType(item.Field.original_type);
                if (className != CopiedInstance.RszClass.name &&
                    !MessageBoxUtils.Confirm(string.Format(
                        Texts.RszClassMismatchConfirm, CopiedInstance.RszClass.name, className)))
                {
                    return;
                }
                var newItem = rsz.ArrayInsertInstance(item.Values, CopiedInstance);
                UpdateInstanceContextID(newItem);
                item.NotifyItemsChanged();
                Changed = true;
            }
        }

        private void OnGenerateMercenariesSpawnPoints(object arg)
        {
            if (arg is not RszFieldArrayViewModel item || File.GetRSZ() is not RSZFile rsz)
            {
                return;
            }
            if (!item.CanGenerateMercenariesSpawnPoints)
            {
                MessageBoxUtils.Error("This generator only supports CharacterSpawnPointMercenaries SpawnPoints arrays.");
                return;
            }

            Views.MultiLineInputDialog dialog = new()
            {
                Title = "Generate Mercenaries SpawnPoints",
                Message = "Input spawn point groups as (x,y,z,b), separated by commas. b=0 sets IsOutOfCameraOnly to False; b=1 sets it to True.",
                InputText = "(0,0,0,1)",
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true) return;

            if (!TryParseMercenariesSpawnPointInput(dialog.InputText, out var points, out string error))
            {
                MessageBoxUtils.Error(error);
                return;
            }
            if (item.Values.Count > 0 &&
                !MessageBoxUtils.Confirm($"Replace {item.Values.Count} existing SpawnPoints with {points.Count} generated SpawnPoints?"))
            {
                return;
            }

            AppUtils.TryActionSimple(() =>
            {
                ReplaceMercenariesSpawnPoints(rsz, item, points);
            });
        }

        private void OnImportMercenariesSpawnPointsJson(object arg)
        {
            if (arg is not RszFieldArrayViewModel item || File.GetRSZ() is not RSZFile rsz)
            {
                return;
            }
            if (!item.CanGenerateMercenariesSpawnPoints)
            {
                MessageBoxUtils.Error("This importer only supports CharacterSpawnPointMercenaries SpawnPoints arrays.");
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Mercenaries SpawnPoints JSON|*.json|All files|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            if (!TryLoadMercenariesSpawnPointJson(dialog.FileName, out var groups, out string? activeGroup, out string error))
            {
                MessageBoxUtils.Error(error);
                return;
            }

            var group = SelectMercenariesSpawnPointGroup(groups, activeGroup);
            if (group == null) return;
            if (group.Points.Count == 0)
            {
                MessageBoxUtils.Error($"Group '{group.Name}' has no points.");
                return;
            }
            if (item.Values.Count > 0 &&
                !MessageBoxUtils.Confirm($"Replace {item.Values.Count} existing SpawnPoints with {group.Points.Count} points from '{group.Name}'?"))
            {
                return;
            }

            AppUtils.TryActionSimple(() =>
            {
                ReplaceMercenariesSpawnPoints(rsz, item, group.Points);
            });
        }

        private void OnImportTransformPointsJson(object arg)
        {
            if (arg is not GameObejctComponentViewModel component ||
                !component.CanImportTransformPointsJson)
            {
                return;
            }

            Views.TransformPointsJsonWindow dialog = new()
            {
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true || dialog.SelectedPoint == null)
            {
                return;
            }

            if (!TryApplyTransformPoint(component.Instance, dialog.SelectedPoint, out string error))
            {
                MessageBoxUtils.Error(error);
                return;
            }

            component.NotifyItemsChanged();
            Changed = true;
        }

        private static bool TryApplyTransformPoint(
            RszInstance transform,
            TransformPointJson point,
            out string error)
        {
            error = "";
            if (!string.Equals(transform.RszClass.name, "via.Transform", StringComparison.OrdinalIgnoreCase))
            {
                error = "The selected component is not via.Transform.";
                return false;
            }

            int transformIndex = FindFieldIndex(transform, "v0", "Position");
            int rotationIndex = FindFieldIndex(transform, "v1", "Rotation");
            if (transformIndex < 0 || rotationIndex < 0 ||
                transformIndex >= transform.Values.Length || rotationIndex >= transform.Values.Length)
            {
                error = "via.Transform does not contain v0/Position and v1/Rotation fields.";
                return false;
            }

            RszField transformField = transform.Fields[transformIndex];
            RszField rotationField = transform.Fields[rotationIndex];
            if (!CanStoreVector4(transformField, transform.Values[transformIndex]))
            {
                error = "via.Transform v0/Position is not a four-component vector field.";
                return false;
            }
            if (!TryCreateRotationValue(
                rotationField,
                transform.Values[rotationIndex],
                point.Rotation,
                out object rotationValue))
            {
                error = "via.Transform v1/Rotation is not a four-component vector or quaternion field.";
                return false;
            }

            // v0 stores position as a four-component vector; its fourth component is always zero.
            transform.SetFieldValue(transformIndex, new Vector4(point.Position, 0f));
            transform.SetFieldValue(rotationIndex, rotationValue);
            return true;
        }

        private static int FindFieldIndex(RszInstance instance, params string[] names)
        {
            foreach (string name in names)
            {
                int index = instance.RszClass.IndexOfField(name);
                if (index >= 0) return index;

                for (int fieldIndex = 0; fieldIndex < instance.Fields.Length; fieldIndex++)
                {
                    if (string.Equals(instance.Fields[fieldIndex].name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return fieldIndex;
                    }
                }
            }
            return -1;
        }

        private static bool CanStoreVector4(RszField field, object? currentValue)
        {
            return currentValue is Vector4 ||
                field.type is RszFieldType.Vec4 or RszFieldType.Float4 ||
                field.original_type.Contains("Vector4", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryCreateRotationValue(
            RszField field,
            object? currentValue,
            Quaternion rotation,
            out object value)
        {
            if (currentValue is Quaternion || field.type == RszFieldType.Quaternion ||
                field.original_type.Contains("Quaternion", StringComparison.OrdinalIgnoreCase))
            {
                value = rotation;
                return true;
            }
            if (currentValue is Vector4 || field.type is RszFieldType.Vec4 or RszFieldType.Float4 ||
                field.original_type.Contains("Vector4", StringComparison.OrdinalIgnoreCase))
            {
                value = new Vector4(rotation.X, rotation.Y, rotation.Z, rotation.W);
                return true;
            }
            value = new Vector4();
            return false;
        }

        private void ReplaceMercenariesSpawnPoints(
            RSZFile rsz,
            RszFieldArrayViewModel item,
            IReadOnlyList<MercenariesSpawnPointInput> points)
        {
            RszInstance? template = item.Values.OfType<RszInstance>().FirstOrDefault();
            while (item.Values.Count > 0)
            {
                rsz.ArrayRemoveItem(item.Values, item.Values[0]);
            }

            foreach (var point in points)
            {
                RszInstance instance = template != null ?
                    rsz.CloneInstance(template) :
                    CreateMercenariesSpawnPointInstance(rsz, item.Field);

                ApplyMercenariesSpawnPoint(instance, point);
                rsz.ArrayInsertInstance(item.Values, instance, clone: false);
            }

            item.NotifyItemsChanged();
            Changed = true;
        }

        private static RszInstance CreateMercenariesSpawnPointInstance(RSZFile rsz, RszField field)
        {
            var newItem = RszInstance.CreateArrayItem(rsz.RszParser, field, MercenariesSpawnPointClassName);
            if (newItem is not RszInstance instance)
            {
                throw new InvalidOperationException("Failed to create CharacterSpawnPointMercenaries item.");
            }
            return instance;
        }

        private static bool TryLoadMercenariesSpawnPointJson(
            string path,
            out List<MercenariesSpawnPointGroup> groups,
            out string? activeGroup,
            out string error)
        {
            groups = [];
            activeGroup = null;
            error = "";
            try
            {
                using FileStream stream = System.IO.File.OpenRead(path);
                using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
                JsonElement root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    activeGroup = GetStringProperty(root, "activeGroup") ??
                        GetStringProperty(root, "active_group") ??
                        GetStringProperty(root, "currentGroup");
                    if (TryGetPropertyIgnoreCase(root, "groups", out JsonElement groupsElement))
                    {
                        return TryReadMercenariesSpawnPointGroups(groupsElement, out groups, out error);
                    }
                    if (TryGetPropertyIgnoreCase(root, "points", out JsonElement pointsElement))
                    {
                        string name = GetStringProperty(root, "name") ?? Path.GetFileNameWithoutExtension(path);
                        return TryReadMercenariesSpawnPointGroup(name, pointsElement, out var group, out error) &&
                            AddSingleGroup(group, groups);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    return TryReadRootArray(root, Path.GetFileNameWithoutExtension(path), out groups, out error);
                }

                error = "JSON must contain either a groups array or a points array.";
                return false;
            }
            catch (Exception ex)
            {
                error = $"Failed to read JSON: {ex.Message}";
                return false;
            }
        }

        private static bool AddSingleGroup(MercenariesSpawnPointGroup group, List<MercenariesSpawnPointGroup> groups)
        {
            groups.Add(group);
            return true;
        }

        private static MercenariesSpawnPointGroup? SelectMercenariesSpawnPointGroup(
            IReadOnlyList<MercenariesSpawnPointGroup> groups,
            string? activeGroup)
        {
            if (groups.Count == 0)
            {
                MessageBoxUtils.Error("JSON has no groups.");
                return null;
            }
            if (groups.Count == 1)
            {
                return groups[0];
            }

            string defaultInput = "";
            if (!string.IsNullOrWhiteSpace(activeGroup) &&
                groups.Any(item => string.Equals(item.Name, activeGroup, StringComparison.OrdinalIgnoreCase)))
            {
                defaultInput = activeGroup!;
            }
            else
            {
                defaultInput = groups[0].Name;
            }

            string groupList = string.Join(", ", groups.Select((item, index) => $"{index + 1}:{item.Name}({item.Points.Count})"));
            Views.InputDialog dialog = new()
            {
                Title = "Import SpawnPoints JSON",
                Message = $"Input group name or index. Groups: {groupList}",
                InputText = defaultInput,
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true) return null;

            string input = dialog.InputText.Trim();
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 1 && index <= groups.Count)
            {
                return groups[index - 1];
            }

            var group = groups.FirstOrDefault(item =>
                string.Equals(item.Name, input, StringComparison.OrdinalIgnoreCase));
            if (group == null)
            {
                MessageBoxUtils.Error($"Group '{input}' not found.");
            }
            return group;
        }

        private static bool TryReadRootArray(
            JsonElement root,
            string defaultName,
            out List<MercenariesSpawnPointGroup> groups,
            out string error)
        {
            groups = [];
            error = "";
            var items = root.EnumerateArray().ToList();
            if (items.Count > 0 &&
                items.All(item => item.ValueKind == JsonValueKind.Object &&
                    TryGetPropertyIgnoreCase(item, "points", out _)))
            {
                return TryReadMercenariesSpawnPointGroups(root, out groups, out error);
            }
            return TryReadMercenariesSpawnPointGroup(defaultName, root, out var group, out error) &&
                AddSingleGroup(group, groups);
        }

        private static bool TryReadMercenariesSpawnPointGroups(
            JsonElement groupsElement,
            out List<MercenariesSpawnPointGroup> groups,
            out string error)
        {
            groups = [];
            error = "";
            if (groupsElement.ValueKind != JsonValueKind.Array)
            {
                error = "groups must be an array.";
                return false;
            }

            int index = 1;
            foreach (JsonElement groupElement in groupsElement.EnumerateArray())
            {
                if (groupElement.ValueKind != JsonValueKind.Object ||
                    !TryGetPropertyIgnoreCase(groupElement, "points", out JsonElement pointsElement))
                {
                    error = $"Group {index} must be an object with a points array.";
                    return false;
                }

                string name = GetStringProperty(groupElement, "name") ?? $"Group {index}";
                if (!TryReadMercenariesSpawnPointGroup(name, pointsElement, out var group, out error))
                {
                    error = $"Group '{name}': {error}";
                    return false;
                }
                groups.Add(group);
                index++;
            }

            if (groups.Count == 0)
            {
                error = "groups is empty.";
                return false;
            }
            return true;
        }

        private static bool TryReadMercenariesSpawnPointGroup(
            string name,
            JsonElement pointsElement,
            out MercenariesSpawnPointGroup group,
            out string error)
        {
            group = new MercenariesSpawnPointGroup(name, []);
            error = "";
            if (pointsElement.ValueKind != JsonValueKind.Array)
            {
                error = "points must be an array.";
                return false;
            }

            int index = 1;
            foreach (JsonElement pointElement in pointsElement.EnumerateArray())
            {
                if (!TryReadMercenariesSpawnPoint(pointElement, out var point, out error))
                {
                    error = $"Point {index}: {error}";
                    return false;
                }
                group.Points.Add(point);
                index++;
            }
            return true;
        }

        private static bool TryReadMercenariesSpawnPoint(
            JsonElement element,
            out MercenariesSpawnPointInput point,
            out string error)
        {
            point = default;
            error = "";
            if (element.ValueKind == JsonValueKind.Object)
            {
                float x = 0;
                float y = 0;
                float z = 0;
                bool hasPosition = TryReadFloatProperty(element, "x", out x) &&
                    TryReadFloatProperty(element, "y", out y) &&
                    TryReadFloatProperty(element, "z", out z);
                if (!hasPosition)
                {
                    hasPosition = TryReadMercenariesPointPosition(element, out x, out y, out z);
                }
                if (!hasPosition)
                {
                    error = "point object must contain numeric x, y and z or a transform array.";
                    return false;
                }

                bool isOutOfCameraOnly = true;
                if (TryGetPropertyIgnoreCase(element, "b", out JsonElement bElement) ||
                    TryGetPropertyIgnoreCase(element, "isOutOfCameraOnly", out bElement) ||
                    TryGetPropertyIgnoreCase(element, "_IsOutOfCameraOnly", out bElement) ||
                    TryGetPropertyIgnoreCase(element, "outOfCameraOnly", out bElement))
                {
                    if (!TryReadBoolFlag(bElement, out isOutOfCameraOnly))
                    {
                        error = "b must be 0, 1, false or true.";
                        return false;
                    }
                }

                point = new MercenariesSpawnPointInput(x, y, z, isOutOfCameraOnly);
                return true;
            }
            if (element.ValueKind == JsonValueKind.Array)
            {
                var values = element.EnumerateArray().ToList();
                if (values.Count < 3)
                {
                    error = "point array must contain at least x, y and z.";
                    return false;
                }
                if (!TryReadFloat(values[0], out float x) ||
                    !TryReadFloat(values[1], out float y) ||
                    !TryReadFloat(values[2], out float z))
                {
                    error = "point array x, y and z must be numeric.";
                    return false;
                }
                bool isOutOfCameraOnly = true;
                if (values.Count >= 4 && !TryReadBoolFlag(values[3], out isOutOfCameraOnly))
                {
                    error = "point array b must be 0, 1, false or true.";
                    return false;
                }

                point = new MercenariesSpawnPointInput(x, y, z, isOutOfCameraOnly);
                return true;
            }

            error = "point must be an object or array.";
            return false;
        }

        private static bool TryReadFloatProperty(JsonElement element, string name, out float value)
        {
            value = 0;
            return TryGetPropertyIgnoreCase(element, name, out JsonElement property) &&
                TryReadFloat(property, out value);
        }

        private static bool TryReadMercenariesPointPosition(
            JsonElement element,
            out float x,
            out float y,
            out float z)
        {
            foreach (string name in new[] { "transform", "position", "pos", "translation", "v0" })
            {
                if (!TryGetPropertyIgnoreCase(element, name, out JsonElement position))
                {
                    continue;
                }

                if (position.ValueKind == JsonValueKind.Array)
                {
                    var values = position.EnumerateArray().ToList();
                    if (values.Count >= 3 &&
                        TryReadFloat(values[0], out x) &&
                        TryReadFloat(values[1], out y) &&
                        TryReadFloat(values[2], out z))
                    {
                        return true;
                    }
                }
                else if (position.ValueKind == JsonValueKind.Object &&
                    TryReadFloatProperty(position, "x", out x) &&
                    TryReadFloatProperty(position, "y", out y) &&
                    TryReadFloatProperty(position, "z", out z))
                {
                    return true;
                }
            }

            x = y = z = 0;
            return false;
        }

        private static bool TryReadFloat(JsonElement element, out float value)
        {
            value = 0;
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetSingle(out value),
                JsonValueKind.String => float.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value),
                _ => false,
            };
        }

        private static bool TryReadBoolFlag(JsonElement element, out bool value)
        {
            value = true;
            if (element.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (element.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int intValue) &&
                (intValue == 0 || intValue == 1))
            {
                value = intValue == 1;
                return true;
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                string text = element.GetString()?.Trim() ?? "";
                if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                {
                    value = false;
                    return true;
                }
                if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                {
                    value = true;
                    return true;
                }
            }
            return false;
        }

        private static string? GetStringProperty(JsonElement element, string name)
        {
            return TryGetPropertyIgnoreCase(element, name, out JsonElement property) &&
                property.ValueKind == JsonValueKind.String ?
                property.GetString() :
                null;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement property)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty item in element.EnumerateObject())
                {
                    if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        property = item.Value;
                        return true;
                    }
                }
            }
            property = default;
            return false;
        }

        private static bool TryParseMercenariesSpawnPointInput(
            string input,
            out List<MercenariesSpawnPointInput> points,
            out string error)
        {
            points = [];
            error = "";
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Input is empty. Expected groups like (1,2,3,0),(4,5,6,1).";
                return false;
            }

            int lastEnd = 0;
            foreach (Match match in MercenariesSpawnPointInputRegex.Matches(input))
            {
                if (!IsSpawnPointSeparatorOnly(input, lastEnd, match.Index - lastEnd))
                {
                    error = "Invalid text between spawn point groups. Expected comma-separated groups like (1,2,3,0),(4,5,6,1).";
                    return false;
                }

                if (!float.TryParse(match.Groups["x"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                    !float.TryParse(match.Groups["y"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                    !float.TryParse(match.Groups["z"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
                    !int.TryParse(match.Groups["b"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int b))
                {
                    error = $"Invalid spawn point group: {match.Value}";
                    return false;
                }

                points.Add(new MercenariesSpawnPointInput(x, y, z, b == 1));
                lastEnd = match.Index + match.Length;
            }

            if (points.Count == 0)
            {
                error = "No valid spawn point groups found. Expected groups like (1,2,3,0),(4,5,6,1).";
                return false;
            }
            if (!IsSpawnPointSeparatorOnly(input, lastEnd, input.Length - lastEnd))
            {
                error = "Invalid trailing text after spawn point groups.";
                return false;
            }
            return true;
        }

        private static bool IsSpawnPointSeparatorOnly(string input, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                char c = input[i];
                if (!char.IsWhiteSpace(c) && c != ',' && c != '\uFF0C' && c != ';' && c != '\uFF1B')
                {
                    return false;
                }
            }
            return true;
        }

        private void OnPasteTransformV0FromClipboard(object arg)
        {
            if (!TryReadClipboardTransformV0(out Vector4 value)) return;
            int count = arg switch
            {
                ScnFile.GameObjectData gameObject => TransformV0Helper.SetTransformV0(GetSelectedOrSingleScnGameObjects(gameObject), value),
                PfbFile.GameObjectData gameObject => TransformV0Helper.SetTransformV0([gameObject], value),
                GameObejctComponentViewModel componentViewModel => TransformV0Helper.SetTransformV0(componentViewModel.Instance, value),
                RszFieldInstanceViewModel fieldInstanceViewModel => TransformV0Helper.SetTransformV0(fieldInstanceViewModel.Instance, value),
                RszFieldArrayInstanceItemViewModel arrayItemViewModel => TransformV0Helper.SetTransformV0(arrayItemViewModel.Instance, value),
                RszInstance instance => TransformV0Helper.SetTransformV0(instance, value),
                _ => 0,
            };
            if (count == 0)
            {
                MessageBoxUtils.Info(Texts.NoTransformEdited);
                return;
            }
            Changed = true;
        }

        private IEnumerable<IGameObjectData> GetSelectedOrSingleScnGameObjects(ScnFile.GameObjectData gameObject)
        {
            if (SelectedScnGameObjects.Contains(gameObject))
            {
                return SelectedScnGameObjects;
            }
            return [gameObject];
        }

        private static bool TryReadClipboardTransformV0(out Vector4 value)
        {
            value = default;
            string text;
            try
            {
                text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
            }
            catch (Exception ex)
            {
                MessageBoxUtils.Error(string.Format(
                    GetResourceText("TransformClipboardReadFailed", "Failed to read clipboard text: {0}"),
                    ex.Message));
                return false;
            }

            if (TransformV0Helper.TryParseClipboardVector(text, out value))
            {
                return true;
            }

            MessageBoxUtils.Error(GetResourceText(
                "TransformClipboardInvalid",
                "Clipboard format must be (1,2,3,4)."));
            return false;
        }

        private static string GetResourceText(string name, string fallback)
        {
            return Texts.ResourceManager.GetString(name, Texts.Culture) ?? fallback;
        }

        private static void ApplyMercenariesSpawnPoint(RszInstance instance, MercenariesSpawnPointInput point)
        {
            int transformIndex = instance.RszClass.IndexOfField(SpawnPointTransformFieldName);
            int outOfCameraIndex = instance.RszClass.IndexOfField(SpawnPointOutOfCameraFieldName);
            if (transformIndex == -1 || outOfCameraIndex == -1)
            {
                throw new InvalidOperationException("CharacterSpawnPointMercenaries is missing _Transform or _IsOutOfCameraOnly.");
            }
            if (instance.Fields[transformIndex].type != RszFieldType.Mat4 ||
                instance.Fields[outOfCameraIndex].type != RszFieldType.Bool)
            {
                throw new InvalidOperationException("CharacterSpawnPointMercenaries has unexpected field types.");
            }

            instance.Values[transformIndex] = new via.mat4
            {
                m00 = 1,
                m11 = 1,
                m22 = 1,
                m30 = point.X,
                m31 = point.Y,
                m32 = point.Z,
                m33 = 1,
            };
            instance.Values[outOfCameraIndex] = point.IsOutOfCameraOnly;
        }

        private readonly struct MercenariesSpawnPointInput(
            float x,
            float y,
            float z,
            bool isOutOfCameraOnly)
        {
            public float X { get; } = x;
            public float Y { get; } = y;
            public float Z { get; } = z;
            public bool IsOutOfCameraOnly { get; } = isOutOfCameraOnly;
        }

        private sealed class MercenariesSpawnPointGroup(
            string name,
            List<MercenariesSpawnPointInput> points)
        {
            public string Name { get; } = name;
            public List<MercenariesSpawnPointInput> Points { get; } = points;
        }

        private void OnSearchInstances(object arg)
        {
            SearchInstanceList ??= new();
            SearchInstanceList.Clear();
            if (File.GetRSZ() is not RSZFile rsz) return;
            InstanceFilter filter = new(InstanceSearchViewModel);
            if (!filter.Enable) return;
            foreach (var instance in rsz.InstanceList)
            {
                if (filter.IsMatch(instance))
                {
                    SearchInstanceList.Add(instance);
                }
            }
        }

        public abstract IEnumerable<object> TreeViewItems { get; }
    }


    public class GameObejctComponentViewModel(IGameObjectData gameObject, RszInstance instance) : BaseViewModel
    {
        public IGameObjectData GameObject { get; } = gameObject;
        public RszInstance Instance { get; } = instance;
        public bool CanImportTransformPointsJson =>
            string.Equals(Instance.RszClass.name, "via.Transform", StringComparison.OrdinalIgnoreCase);

        public string Name => Instance.Name;
        public IEnumerable<object> Items => Converters.RszInstanceFieldsConverter.GetItems(Instance);

        public void NotifyItemsChanged()
        {
            OnPropertyChanged(nameof(Items));
        }

        public override string ToString()
        {
            return Instance.Name;
        }

        public static ObservableCollection<GameObejctComponentViewModel> MakeList(IGameObjectData gameObject)
        {
            ObservableCollection<GameObejctComponentViewModel> list = new();
            foreach (var item in gameObject.Components)
            {
                list.Add(new(gameObject, item));
            }
            gameObject.Components.CollectionChanged += (_, e) =>
            {
                ObjectModelUtils.SyncObservableCollection(list,
                    obj => new GameObejctComponentViewModel(gameObject, (RszInstance)obj), e);
            };
            return list;
        }
    }
}
