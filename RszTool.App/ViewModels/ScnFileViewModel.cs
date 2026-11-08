using RszTool.App.Common;
using RszTool.App.Resources;
using System.Collections.ObjectModel;
using System.Windows;

namespace RszTool.App.ViewModels
{
    public class ScnFileViewModel(ScnFile file) : BaseRszFileViewModel
    {
        private static readonly Random SharedRandom = new();

        public override BaseRszFile File => ScnFile;
        public ScnFile ScnFile { get; } = file;
        public RszViewModel RszViewModel => new(ScnFile.RSZ!);
        public ObservableCollection<ScnFile.FolderData>? Folders => ScnFile.FolderDatas;
        public ObservableCollection<ScnFile.GameObjectData>? GameObjects => ScnFile.GameObjectDatas;
        public GameObjectSearchViewModel GameObjectSearchViewModel { get; } = new();
        public ObservableCollection<ScnFile.GameObjectData>? SearchGameObjectList { get; set; }

        public bool ResourceChanged
        {
            get => ScnFile.ResourceChanged;
            set => ScnFile.ResourceChanged = value;
        }

        public override void PostRead()
        {
            ScnFile.SetupGameObjects();
        }

        public override IEnumerable<object> TreeViewItems
        {
            get
            {
                yield return new TreeItemViewModel("Resources", ScnFile.ResourceInfoList);
                yield return new FoldersHeader("Folders", Folders);
                yield return new ScnGameObjectsHeader("GameObjects", GameObjects);
            }
        }

        public RelayCommand CopyGameObject => new(OnCopyGameObject);
        public RelayCommand CopyGameObjects => new(OnCopyGameObjects);
        public RelayCommand RemoveFolder => new(OnRemoveFolder);
        public RelayCommand RemoveGameObject => new(OnRemoveGameObject);
        public RelayCommand RemoveGameObjects => new(OnRemoveGameObjects);
        public RelayCommand DuplicateGameObject => new(OnDuplicateGameObject);
        public RelayCommand DuplicateMultiGameObject => new(OnDuplicateMultiGameObject);
        public RelayCommand PasteGameObject => new(OnPasteGameObject);
        public RelayCommand PasteGameObjectToFolder => new(OnPasteGameObjectToFolder);
        public RelayCommand AddFolder => new(OnAddFolder);
        public RelayCommand PasteGameobjectAsChild => new(OnPasteGameobjectAsChild);
        public RelayCommand SearchGameObjects => new(OnSearchGameObjects);
        public RelayCommand AddComponent => new(OnAddComponent);
        public RelayCommand PasteInstanceAsComponent => new(OnPasteInstanceAsComponent);
        public RelayCommand RemoveComponent => new(OnRemoveComponent);
        public RelayCommand BatchEditTransformV0 => new(OnBatchEditTransformV0);
        public RelayCommand ShuffleSelectedGameObjects => new(OnShuffleSelectedGameObjects);

        /// <summary>
        /// Update re4 chainsaw.ContextID
        /// </summary>
        /// <param name="gameObject"></param>
        private void UpdateContextID(IGameObjectData gameObject)
        {
            GameObjectCopyHelper.UpdateContextID(ScnFile.Option, gameObject);
        }
        private void UpdateContextIDs(IEnumerable<ScnFile.GameObjectData> gameObjects)
        {
            GameObjectCopyHelper.UpdateContextIDs(ScnFile.Option, gameObjects);
        }
        private void UpdateContextIDBatch(IGameObjectData gameObject,int Index,int Group)
        {
            GameObjectCopyHelper.UpdateContextIDBatch(ScnFile.Option, gameObject,Index,Group);
        }

        private void RandomizeMontageIds(ScnFile.GameObjectData gameObject, IReadOnlyDictionary<string, List<uint>> montageIds)
        {
            MontageIdRandomizer.Randomize(gameObject, ScnFile.Option.EnumParser.EnumDict, montageIds, SharedRandom);
        }

        private bool TryGetMontageIdRandomization(
            IEnumerable<ScnFile.GameObjectData> gameObjects,
            out Dictionary<string, List<uint>> montageIds)
        {
            montageIds = new();
            var sources = MontageIdRandomizer.Sources(gameObjects, ScnFile.Option.EnumParser.EnumDict);
            if (sources.Count == 0) return false;
            var dialog = new Views.DuplicateMontageIdWindow(sources)
            {
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true || !dialog.Randomize) return false;
            montageIds = dialog.ResolvedPools;
            return montageIds.Values.Any(ids => ids.Count > 0);
        }

        private static IEnumerable<ScnFile.GameObjectData> GetDirectGameObjects(object arg)
        {
            return arg switch
            {
                ScnGameObjectsHeader header => header.Items?.OfType<ScnFile.GameObjectData>() ?? [],
                GameObjectsHeader header => header.Items?.OfType<ScnFile.GameObjectData>() ?? [],
                ScnFile.FolderData folder => folder.GameObjects,
                ScnFile.GameObjectData gameObject => gameObject.Children,
                IEnumerable<ScnFile.GameObjectData> gameObjects => gameObjects,
                _ => []
            };
        }

        private List<ScnFile.GameObjectData> GetSelectedOrSingleGameObjects(ScnFile.GameObjectData gameObject)
        {
            if (SelectedScnGameObjects.Contains(gameObject))
            {
                return SelectedScnGameObjects.ToList();
            }
            return [gameObject];
        }

        private List<ScnFile.GameObjectData> GetDuplicateSources(ScnFile.GameObjectData gameObject)
        {
            var gameObjects = GetSelectedOrSingleGameObjects(gameObject);
            if (gameObjects.Count < 2) return gameObjects;

            HashSet<ScnFile.GameObjectData> selectedSet = new(gameObjects);
            return gameObjects
                .Where(item => !HasSelectedAncestor(item, selectedSet))
                .ToList();
        }

        private static bool HasSelectedAncestor(
            ScnFile.GameObjectData gameObject,
            HashSet<ScnFile.GameObjectData> selectedSet)
        {
            for (var parent = gameObject.Parent; parent != null; parent = parent.Parent)
            {
                if (selectedSet.Contains(parent))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadDuplicateCount(out int count)
        {
            Views.InputDialog dialog = new()
            {
                Title = "Tip",
                Message = Texts.InputObjectCount,
                InputText = "5",
                Owner = Application.Current.MainWindow,
            };

            count = 0;
            if (dialog.ShowDialog() != true) return false;
            if (!int.TryParse(dialog.InputText, out count) || count <= 0)
            {
                MessageBoxUtils.Error("Count must be greater than 0");
                return false;
            }
            return true;
        }

        private bool TryGetMontageIdRandomization(ScnFile.GameObjectData gameObject, out Dictionary<string, List<uint>> montageIds)
        {
            return TryGetMontageIdRandomization([gameObject], out montageIds);
        }

        private static bool TryReadDuplicateContextBatch(out int groupId, out int indexId, out int stepId)
        {
            groupId = 0;
            indexId = 0;
            stepId = 1;

            Views.InputDialog groupDialog = new()
            {
                Title = "Group",
                Message = Texts.InputGroupDefault,
                InputText = "5",
                Owner = Application.Current.MainWindow,
            };
            if (groupDialog.ShowDialog() != true) return false;

            Views.InputDialog indexDialog = new()
            {
                Title = "Index",
                Message = Texts.InputIndexDefault,
                InputText = "5",
                Owner = Application.Current.MainWindow,
            };
            if (indexDialog.ShowDialog() != true) return false;

            Views.InputDialog stepDialog = new()
            {
                Title = "Step",
                Message = Texts.InputStepDefault,
                InputText = "1",
                Owner = Application.Current.MainWindow,
            };
            if (stepDialog.ShowDialog() != true) return false;

            return int.TryParse(groupDialog.InputText, out groupId)
                && int.TryParse(indexDialog.InputText, out indexId)
                && int.TryParse(stepDialog.InputText, out stepId);
        }

        public bool MoveGameObject(ScnFile.GameObjectData source, ScnFile.GameObjectData target, bool afterTarget)
        {
            if (!CanMoveGameObject(source, target, afterTarget)) return false;
            var sourceItems = GetGameObjectSiblings(source)!;

            sourceItems.Remove(source);
            int targetIndex = sourceItems.IndexOf(target);
            if (targetIndex < 0)
            {
                sourceItems.Add(source);
                return false;
            }

            int insertIndex = afterTarget ? targetIndex + 1 : targetIndex;
            sourceItems.Insert(insertIndex, source);
            OnPropertyChanged(nameof(GameObjects));
            ScnFile.MarkStructChanged();
            Changed = true;
            return true;
        }

        public bool CanMoveGameObject(ScnFile.GameObjectData source, ScnFile.GameObjectData target, bool afterTarget)
        {
            if (source == target) return false;
            var sourceItems = GetGameObjectSiblings(source);
            var targetItems = GetGameObjectSiblings(target);
            if (sourceItems == null || targetItems == null || !ReferenceEquals(sourceItems, targetItems)) return false;

            int sourceIndex = sourceItems.IndexOf(source);
            int targetIndex = targetItems.IndexOf(target);
            if (sourceIndex < 0 || targetIndex < 0) return false;
            return afterTarget ? sourceIndex != targetIndex + 1 : sourceIndex != targetIndex - 1;
        }

        private ObservableCollection<ScnFile.GameObjectData>? GetGameObjectSiblings(ScnFile.GameObjectData gameObject)
        {
            if (gameObject.Parent != null) return gameObject.Parent.Children;
            if (gameObject.Folder != null) return gameObject.Folder.GameObjects;
            return GameObjects;
        }

        private void OnShuffleSelectedGameObjects(object arg)
        {
            var gameObjects = arg is ScnFile.GameObjectData gameObject ?
                GetSelectedOrSingleGameObjects(gameObject) :
                SelectedScnGameObjects.ToList();
            if (gameObjects.Count < 2) return;

            var siblings = GetGameObjectSiblings(gameObjects[0]);
            if (siblings == null || gameObjects.Any(item => !ReferenceEquals(GetGameObjectSiblings(item), siblings)))
            {
                MessageBoxUtils.Error(Texts.ShuffleGameObjectsSameParentOnly);
                return;
            }

            var originalSlots = siblings
                .Select((item, index) => (item, index))
                .Where(item => gameObjects.Contains(item.item))
                .ToList();
            if (originalSlots.Count < 2) return;

            var shuffled = originalSlots.Select(item => item.item).ToList();
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = SharedRandom.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            if (originalSlots.Select(item => item.item).SequenceEqual(shuffled) && shuffled.Count > 1)
            {
                (shuffled[0], shuffled[1]) = (shuffled[1], shuffled[0]);
            }

            for (int i = 0; i < originalSlots.Count; i++)
            {
                siblings[originalSlots[i].index] = shuffled[i];
            }

            OnPropertyChanged(nameof(GameObjects));
            ScnFile.MarkStructChanged();
            Changed = true;
        }

        public bool MoveFolders(IEnumerable<ScnFile.FolderData> sources, ScnFile.FolderData target, bool afterTarget)
        {
            var sourceList = sources.Distinct().ToList();
            if (!CanMoveFolders(sourceList, target, afterTarget)) return false;
            var items = GetFolderSiblings(target)!;
            var movingItems = items.Where(sourceList.Contains).ToList();

            foreach (var source in movingItems)
            {
                items.Remove(source);
            }

            int targetIndex = items.IndexOf(target);
            if (targetIndex < 0)
            {
                foreach (var source in movingItems)
                {
                    items.Add(source);
                }
                return false;
            }

            int insertIndex = afterTarget ? targetIndex + 1 : targetIndex;
            foreach (var source in movingItems)
            {
                items.Insert(insertIndex++, source);
            }

            OnPropertyChanged(nameof(Folders));
            ScnFile.MarkStructChanged();
            Changed = true;
            return true;
        }

        public bool CanMoveFolders(IEnumerable<ScnFile.FolderData> sources, ScnFile.FolderData target, bool afterTarget)
        {
            var sourceList = sources.Distinct().ToList();
            if (sourceList.Count == 0 || sourceList.Contains(target)) return false;
            var targetItems = GetFolderSiblings(target);
            if (targetItems == null) return false;
            if (sourceList.Any(source => !ReferenceEquals(GetFolderSiblings(source), targetItems))) return false;

            var movingItems = targetItems.Where(sourceList.Contains).ToList();
            if (movingItems.Count != sourceList.Count) return false;

            var result = targetItems.Where(item => !sourceList.Contains(item)).ToList();
            int targetIndex = result.IndexOf(target);
            if (targetIndex < 0) return false;
            int insertIndex = afterTarget ? targetIndex + 1 : targetIndex;
            result.InsertRange(insertIndex, movingItems);
            return !targetItems.SequenceEqual(result);
        }

        private ObservableCollection<ScnFile.FolderData>? GetFolderSiblings(ScnFile.FolderData folder)
        {
            if (folder.Parent != null) return folder.Parent.Children;
            return ScnFile.FolderDatas;
        }

        private List<ScnFile.GameObjectData> PasteCopiedGameObjects(
            ScnFile.FolderData? folder = null,
            ScnFile.GameObjectData? parent = null)
        {
            var gameObjects = GameObjectCopyHelper.GetCopiedScnGameObjects().ToList();
            if (gameObjects.Count == 0) return [];

            var newGameObjects = ScnFile.ImportGameObjects(gameObjects, folder, parent);
            UpdateContextIDs(newGameObjects);
            OnPropertyChanged(nameof(GameObjects));
            Changed = true;
            return newGameObjects;
        }

        /// <summary>
        /// 复制游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnCopyGameObject(object arg)
        {
            var gameObject = (ScnFile.GameObjectData)arg;
            var gameObjects = GetSelectedOrSingleGameObjects(gameObject);
            if (gameObjects.Count == 1)
            {
                GameObjectCopyHelper.CopyGameObject(gameObjects[0]);
            }
            else
            {
                GameObjectCopyHelper.CopyGameObjects(gameObjects);
            }
        }

        private static void OnCopyGameObjects(object arg)
        {
            var gameObjects = GetDirectGameObjects(arg).ToList();
            if (gameObjects.Count > 0)
            {
                GameObjectCopyHelper.CopyGameObjects(gameObjects);
            }
        }

        /// <summary>
        /// 删除目录
        /// </summary>
        /// <param name="arg"></param>
        private void OnRemoveFolder(object arg)
        {
            ScnFile.RemoveFolder((ScnFile.FolderData)arg);
            Changed = true;
        }

        /// <summary>
        /// 删除游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnRemoveGameObject(object arg)
        {
            var gameObjects = GetSelectedOrSingleGameObjects((ScnFile.GameObjectData)arg);
            if (gameObjects.Count > 1 &&
                !MessageBoxUtils.Confirm(string.Format(Texts.RemoveGameObjectsConfirm, gameObjects.Count)))
            {
                return;
            }
            foreach (var gameObject in gameObjects)
            {
                ScnFile.RemoveGameObject(gameObject);
                SelectedScnGameObjects.Remove(gameObject);
            }
            SelectedScnGameObjectAnchor = null;
            OnPropertyChanged(nameof(GameObjects));
            Changed = true;
        }

        private void OnRemoveGameObjects(object arg)
        {
            var gameObjects = GetDirectGameObjects(arg).ToList();
            if (gameObjects.Count == 0) return;
            if (!MessageBoxUtils.Confirm(string.Format(Texts.RemoveGameObjectsConfirm, gameObjects.Count))) return;
            foreach (var gameObject in gameObjects)
            {
                ScnFile.RemoveGameObject(gameObject);
            }
            OnPropertyChanged(nameof(GameObjects));
            Changed = true;
        }

        /// <summary>
        /// 重复游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnDuplicateGameObject(object arg)
        {
            var sourceGameObject = (ScnFile.GameObjectData)arg;
            bool randomizeMontage = TryGetMontageIdRandomization(sourceGameObject, out var montageIds);
            var newGameObject = ScnFile.DuplicateGameObject(sourceGameObject);
            Changed = true;
            UpdateContextID(newGameObject);
            if (randomizeMontage)
            {
                RandomizeMontageIds(newGameObject, montageIds);
            }
            Changed = true;
        }

        /// <summary>
        ///New MultiObject
        /// </summary>
        private void OnDuplicateMultiGameObject(object arg)
        {
            var sourceGameObjects = arg is ScnFile.GameObjectData gameObject
                ? GetDuplicateSources(gameObject)
                : SelectedScnGameObjects.ToList();
            if (sourceGameObjects.Count == 0) return;
            if (!TryReadDuplicateCount(out int count)) return;
            TryGetMontageIdRandomization(sourceGameObjects, out var montageIds);

            MessageBoxResult judge = MessageBox.Show(
                messageBoxText: Texts.InputContextCheck,
                caption: "Attention!",
                button: MessageBoxButton.YesNo,
                icon: MessageBoxImage.Question
            );

            bool useBatchContextID = judge == MessageBoxResult.Yes;
            int groupId = 0;
            int indexId = 0;
            int stepId = 1;
            if (useBatchContextID && !TryReadDuplicateContextBatch(out groupId, out indexId, out stepId))
            {
                return;
            }

            var duplicatedBySource = ScnFile.DuplicateGameObjects(sourceGameObjects, count);
            Changed = true;
            int duplicateIndex = 0;
            foreach (var sourceGameObject in sourceGameObjects)
            {
                foreach (var newGameObject in duplicatedBySource[sourceGameObject])
                {
                    if (useBatchContextID)
                    {
                        UpdateContextIDBatch(newGameObject, indexId + (stepId * duplicateIndex), groupId);
                    }
                    duplicateIndex++;
                }
            }

            if (!useBatchContextID)
            {
                UpdateContextIDs(sourceGameObjects.SelectMany(source => duplicatedBySource[source]));
            }

            if (montageIds.Count > 0)
            {
                foreach (var newGameObject in sourceGameObjects.SelectMany(source => duplicatedBySource[source]))
                {
                    RandomizeMontageIds(newGameObject, montageIds);
                }
            }

            Changed = true;
        }

    
        /// <summary>
        /// 粘贴游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameObject(object arg)
        {
            if (arg is ScnGameObjectsHeader header)
            {
                PasteCopiedGameObjects(header.Folder, header.Parent);
                return;
            }
            PasteCopiedGameObjects();
        }

        /// <summary>
        /// 粘贴游戏对象到文件夹
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameObjectToFolder(object arg)
        {
            var folder = (ScnFile.FolderData)arg;
            PasteCopiedGameObjects(folder);
        }

        /// <summary>
        /// 粘贴游戏对象到父对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameobjectAsChild(object arg)
        {
            var parent = (ScnFile.GameObjectData)arg;
            PasteCopiedGameObjects(parent: parent);
        }

        /// <summary>
        /// 添加文件夹
        /// </summary>
        /// <param name="arg"></param>
        private void OnAddFolder(object arg)
        {
            Views.InputDialog dialog = new()
            {
                Title = Texts.AddFolder,
                Message = Texts.InputFolderName,
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true) return;
            if (string.IsNullOrWhiteSpace(dialog.InputText))
            {
                MessageBoxUtils.Error("FolderName is empty");
                return;
            }
            lastInputClassName = dialog.InputText;
            ScnFile.AddFolder(dialog.InputText, arg as ScnFile.FolderData);
            Changed = true;
        }

        private void OnSearchGameObjects(object arg)
        {
            SearchGameObjectList ??= new();
            SearchGameObjectList.Clear();
            GameObjectFilter filter = new(GameObjectSearchViewModel);
            if (!filter.Enable) return;
            if (ScnFile.GameObjectDatas == null) return;
            foreach (var gameObject in ScnFile.IterAllGameObjects(GameObjectSearchViewModel.IncludeChildren))
            {
                if (filter.IsMatch(gameObject))
                {
                    SearchGameObjectList.Add(gameObject);
                }
            }
        }

        private string lastInputClassName = "";
        private void OnAddComponent(object arg)
        {
            var gameObject = (ScnFile.GameObjectData)arg;
            Views.InputDialog dialog = new()
            {
                Title = Texts.NewItem,
                Message = Texts.InputClassName,
                InputText = lastInputClassName,
                Owner = Application.Current.MainWindow,
            };
            if (dialog.ShowDialog() != true) return;
            lastInputClassName = dialog.InputText;
            if (string.IsNullOrWhiteSpace(lastInputClassName))
            {
                MessageBoxUtils.Error("ClassName is empty");
                return;
            }
            AppUtils.TryActionSimple(() =>
            {
                ScnFile.AddComponent(gameObject, lastInputClassName);
                Changed = true;
            });
        }

        private void OnPasteInstanceAsComponent(object arg)
        {
            var gameObject = (ScnFile.GameObjectData)arg;
            if (CopiedInstance != null && File.GetRSZ() is RSZFile rsz)
            {
                RszInstance component = rsz.CloneInstance(CopiedInstance);
                ScnFile.AddComponent(gameObject, component);
                Changed = true;
            }
        }

        private void OnRemoveComponent(object arg)
        {
            var item = (GameObejctComponentViewModel)arg;
            if (ScnFile.RemoveComponent(item.GameObject, item.Instance))
            {
                Changed = true;
            }
        }

        private void OnBatchEditTransformV0(object arg)
        {
            var gameObjects = GetBatchTransformGameObjects(arg);
            if (gameObjects.Count == 0) return;

            TransformBatchEditViewModel viewModel = new()
            {
                SelectedGameObjectCount = gameObjects.Count
            };
            Views.TransformBatchEditWindow dialog = new()
            {
                Owner = Application.Current.MainWindow,
                DataContext = viewModel,
                Title = Texts.BatchTransformV0,
            };
            if (dialog.ShowDialog() != true) return;
            int count = viewModel.Apply(gameObjects);
            if (count == 0)
            {
                MessageBoxUtils.Info(Texts.NoTransformEdited);
                return;
            }
            ScnFile.MarkStructChanged();
            Changed = true;
        }

        private List<ScnFile.GameObjectData> GetBatchTransformGameObjects(object arg)
        {
            return arg switch
            {
                ScnFile.GameObjectData gameObject => GetSelectedOrSingleGameObjects(gameObject),
                ScnFile.FolderData folder => GetFolderGameObjects(folder).ToList(),
                _ => SelectedScnGameObjects.ToList()
            };
        }

        private IEnumerable<ScnFile.GameObjectData> GetFolderGameObjects(ScnFile.FolderData folder)
        {
            foreach (var gameObject in folder.GameObjects)
            {
                foreach (var item in ScnFile.IterGameObjects(gameObject, true))
                {
                    yield return item;
                }
            }
            foreach (var child in folder.Children)
            {
                foreach (var item in GetFolderGameObjects(child))
                {
                    yield return item;
                }
            }
        }
    }


    public class RszViewModel(RSZFile rsz)
    {
        public List<RszInstance> Instances => rsz.InstanceList;

        public List<RszInstance> Objects => rsz.ObjectList;
    }
}
