using RszTool.App.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RszTool.App.Views
{
    /// <summary>
    /// RszFileView.xaml 的交互逻辑
    /// </summary>
    public partial class RszFileView : UserControl
    {
        private const int DragHoldMilliseconds = 350;
        private Point scnGameObjectDragStartPoint;
        private ScnFile.GameObjectData? scnGameObjectDragSource;
        private ScnFile.FolderData? scnFolderDragSource;
        private readonly Stopwatch scnGameObjectDragStopwatch = new();

        public RszFileView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is BaseRszFileViewModel viewModel)
            {
                viewModel.SelectedSearchResultChanged += OnSelectedSearchResultChanged;
            }
        }

        private void OnSelectedSearchResultChanged(object? obj)
        {
            if (obj != null)
            {
                // var item = TreeViewUtils.GetTreeViewItem(TreeView, obj);
                // if (item != null)
                // {
                //     item.IsSelected = true;
                // }
            }
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is BaseRszFileViewModel viewModel)
            {
                viewModel.SelectedItem = e.NewValue;
            }
        }

        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BaseRszFileViewModel viewModel) return;
            if (e.OriginalSource is not DependencyObject source) return;
            if (FindAncestor<ToggleButton>(source) != null) return;
            if (FindAncestor<TreeViewItem>(source) is not TreeViewItem item) return;
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (item.DataContext is ScnFile.FolderData folder)
            {
                scnGameObjectDragStartPoint = e.GetPosition(TreeView);
                scnFolderDragSource = folder;
                scnGameObjectDragSource = null;
                scnGameObjectDragStopwatch.Restart();

                ClearScnGameObjectSelection(viewModel);
                if (shift && viewModel.SelectedScnFolderAnchor != null)
                {
                    SelectScnFolderRange(viewModel, viewModel.SelectedScnFolderAnchor, folder);
                }
                else if (ctrl)
                {
                    ToggleScnFolderSelection(viewModel, folder);
                    viewModel.SelectedScnFolderAnchor = folder;
                }
                else
                {
                    SetScnFolderSelection(viewModel, [folder]);
                    viewModel.SelectedScnFolderAnchor = folder;
                }

                viewModel.SelectedItem = folder;
                item.Focus();
                e.Handled = true;
                return;
            }
            if (item.DataContext is not ScnFile.GameObjectData gameObject)
            {
                ResetScnDrag();
                ClearScnGameObjectSelection(viewModel);
                ClearScnFolderSelection(viewModel);
                return;
            }

            scnGameObjectDragStartPoint = e.GetPosition(TreeView);
            scnGameObjectDragSource = gameObject;
            scnFolderDragSource = null;
            scnGameObjectDragStopwatch.Restart();

            ClearScnFolderSelection(viewModel);
            if (shift && viewModel.SelectedScnGameObjectAnchor != null)
            {
                SelectScnGameObjectRange(viewModel, viewModel.SelectedScnGameObjectAnchor, gameObject);
            }
            else if (ctrl)
            {
                ToggleScnGameObjectSelection(viewModel, gameObject);
                viewModel.SelectedScnGameObjectAnchor = gameObject;
            }
            else
            {
                SetScnGameObjectSelection(viewModel, [gameObject]);
                viewModel.SelectedScnGameObjectAnchor = gameObject;
            }

            viewModel.SelectedItem = gameObject;
            item.Focus();
            e.Handled = true;
        }

        private void TreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || (scnGameObjectDragSource == null && scnFolderDragSource == null))
            {
                ResetScnDrag();
                return;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
            if (scnGameObjectDragStopwatch.ElapsedMilliseconds < DragHoldMilliseconds) return;

            Point currentPoint = e.GetPosition(TreeView);
            if (Math.Abs(currentPoint.X - scnGameObjectDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPoint.Y - scnGameObjectDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var dragSource = (object?)scnFolderDragSource ?? scnGameObjectDragSource;
            ResetScnDrag();
            DragDrop.DoDragDrop(TreeView, dragSource, DragDropEffects.Move);
        }

        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = CanDropScnGameObject(e) || CanDropScnFolder(e) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is not ScnFileViewModel viewModel) return;
            if (TryDropScnFolder(viewModel, e)) return;
            if (!TryGetDraggedScnGameObject(e, out var source)) return;
            if (e.OriginalSource is not DependencyObject originalSource) return;
            if (FindAncestor<TreeViewItem>(originalSource) is not TreeViewItem targetItem) return;
            if (targetItem.DataContext is not ScnFile.GameObjectData target) return;

            bool afterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            if (viewModel.MoveGameObject(source, target, afterTarget))
            {
                SetScnGameObjectSelection(viewModel, [source]);
                viewModel.SelectedScnGameObjectAnchor = source;
                ClearScnFolderSelection(viewModel);
                viewModel.SelectedItem = source;
            }
            e.Handled = true;
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;
            if (FindAncestor<ToggleButton>(source) != null) return;
            if (FindAncestor<TreeViewItem>(source) is not TreeViewItem item) return;
            if (item.DataContext is not ScnFile.GameObjectData) return;

            item.IsExpanded = !item.IsExpanded;
            e.Handled = true;
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BaseRszFileViewModel viewModel) return;
            if (e.OriginalSource is not DependencyObject source) return;
            if (FindAncestor<TreeViewItem>(source) is not TreeViewItem item) return;
            if (item.DataContext is ScnFile.FolderData folder)
            {
                if (!viewModel.SelectedScnFolders.Contains(folder))
                {
                    ClearScnGameObjectSelection(viewModel);
                    SetScnFolderSelection(viewModel, [folder]);
                    viewModel.SelectedScnFolderAnchor = folder;
                }
                viewModel.SelectedItem = folder;
                item.Focus();
                return;
            }
            if (item.DataContext is not ScnFile.GameObjectData gameObject) return;
            if (!viewModel.SelectedScnGameObjects.Contains(gameObject))
            {
                ClearScnFolderSelection(viewModel);
                SetScnGameObjectSelection(viewModel, [gameObject]);
                viewModel.SelectedScnGameObjectAnchor = gameObject;
            }
            viewModel.SelectedItem = gameObject;
            item.Focus();
        }

        private void ToggleScnGameObjectSelection(BaseRszFileViewModel viewModel, ScnFile.GameObjectData gameObject)
        {
            if (viewModel.SelectedScnGameObjects.Contains(gameObject))
            {
                viewModel.SelectedScnGameObjects.Remove(gameObject);
            }
            else
            {
                viewModel.SelectedScnGameObjects.Add(gameObject);
            }
            RefreshScnGameObjectSelectionVisuals();
        }

        private void ToggleScnFolderSelection(BaseRszFileViewModel viewModel, ScnFile.FolderData folder)
        {
            if (viewModel.SelectedScnFolders.Contains(folder))
            {
                viewModel.SelectedScnFolders.Remove(folder);
            }
            else
            {
                viewModel.SelectedScnFolders.Add(folder);
            }
            RefreshScnSelectionVisuals();
        }

        private void SelectScnGameObjectRange(
            BaseRszFileViewModel viewModel,
            ScnFile.GameObjectData anchor,
            ScnFile.GameObjectData gameObject)
        {
            var visibleGameObjects = GetVisibleScnGameObjects(TreeView).ToList();
            int anchorIndex = visibleGameObjects.IndexOf(anchor);
            int gameObjectIndex = visibleGameObjects.IndexOf(gameObject);
            if (anchorIndex < 0 || gameObjectIndex < 0)
            {
                SetScnGameObjectSelection(viewModel, [gameObject]);
                viewModel.SelectedScnGameObjectAnchor = gameObject;
                return;
            }

            int start = Math.Min(anchorIndex, gameObjectIndex);
            int end = Math.Max(anchorIndex, gameObjectIndex);
            SetScnGameObjectSelection(viewModel, visibleGameObjects.Skip(start).Take(end - start + 1));
        }

        private void SelectScnFolderRange(
            BaseRszFileViewModel viewModel,
            ScnFile.FolderData anchor,
            ScnFile.FolderData folder)
        {
            var visibleFolders = GetVisibleScnFolders(TreeView).ToList();
            int anchorIndex = visibleFolders.IndexOf(anchor);
            int folderIndex = visibleFolders.IndexOf(folder);
            if (anchorIndex < 0 || folderIndex < 0)
            {
                SetScnFolderSelection(viewModel, [folder]);
                viewModel.SelectedScnFolderAnchor = folder;
                return;
            }

            int start = Math.Min(anchorIndex, folderIndex);
            int end = Math.Max(anchorIndex, folderIndex);
            SetScnFolderSelection(viewModel, visibleFolders.Skip(start).Take(end - start + 1));
        }

        private void SetScnGameObjectSelection(
            BaseRszFileViewModel viewModel,
            IEnumerable<ScnFile.GameObjectData> gameObjects)
        {
            viewModel.SelectedScnGameObjects.Clear();
            foreach (var gameObject in gameObjects)
            {
                viewModel.SelectedScnGameObjects.Add(gameObject);
            }
            RefreshScnSelectionVisuals();
        }

        private void SetScnFolderSelection(
            BaseRszFileViewModel viewModel,
            IEnumerable<ScnFile.FolderData> folders)
        {
            viewModel.SelectedScnFolders.Clear();
            foreach (var folder in folders)
            {
                viewModel.SelectedScnFolders.Add(folder);
            }
            RefreshScnSelectionVisuals();
        }

        private void ClearScnGameObjectSelection(BaseRszFileViewModel viewModel)
        {
            if (viewModel.SelectedScnGameObjects.Count == 0) return;
            viewModel.SelectedScnGameObjects.Clear();
            viewModel.SelectedScnGameObjectAnchor = null;
            RefreshScnSelectionVisuals();
        }

        private void ClearScnFolderSelection(BaseRszFileViewModel viewModel)
        {
            if (viewModel.SelectedScnFolders.Count == 0) return;
            viewModel.SelectedScnFolders.Clear();
            viewModel.SelectedScnFolderAnchor = null;
            RefreshScnSelectionVisuals();
        }

        private IEnumerable<ScnFile.GameObjectData> GetVisibleScnGameObjects(ItemsControl container)
        {
            foreach (var item in container.Items)
            {
                if (item is ScnFile.GameObjectData gameObject)
                {
                    yield return gameObject;
                }

                if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem &&
                    treeViewItem.IsExpanded)
                {
                    foreach (var child in GetVisibleScnGameObjects(treeViewItem))
                    {
                        yield return child;
                    }
                }
            }
        }

        private IEnumerable<ScnFile.FolderData> GetVisibleScnFolders(ItemsControl container)
        {
            foreach (var item in container.Items)
            {
                if (item is ScnFile.FolderData folder)
                {
                    yield return folder;
                }

                if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem treeViewItem &&
                    treeViewItem.IsExpanded)
                {
                    foreach (var child in GetVisibleScnFolders(treeViewItem))
                    {
                        yield return child;
                    }
                }
            }
        }

        private void RefreshScnGameObjectSelectionVisuals()
        {
            RefreshScnSelectionVisuals();
        }

        private void RefreshScnSelectionVisuals()
        {
            if (DataContext is not BaseRszFileViewModel viewModel) return;
            foreach (var treeViewItem in EnumerateTreeViewItems(TreeView))
            {
                bool selected = treeViewItem.DataContext switch
                {
                    ScnFile.GameObjectData gameObject => viewModel.SelectedScnGameObjects.Contains(gameObject),
                    ScnFile.FolderData folder => viewModel.SelectedScnFolders.Contains(folder),
                    _ => false
                };
                TreeViewMultiSelect.SetIsMultiSelected(treeViewItem, selected);
            }
        }

        private IEnumerable<TreeViewItem> EnumerateTreeViewItems(ItemsControl container)
        {
            foreach (var item in container.Items)
            {
                if (container.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
                {
                    continue;
                }

                yield return treeViewItem;
                foreach (var child in EnumerateTreeViewItems(treeViewItem))
                {
                    yield return child;
                }
            }
        }

        private bool CanDropScnGameObject(DragEventArgs e)
        {
            if (DataContext is not ScnFileViewModel viewModel) return false;
            if (!TryGetDraggedScnGameObject(e, out var source)) return false;
            if (e.OriginalSource is not DependencyObject originalSource) return false;
            if (FindAncestor<TreeViewItem>(originalSource) is not TreeViewItem targetItem) return false;
            if (targetItem.DataContext is not ScnFile.GameObjectData target) return false;

            bool afterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            return viewModel.CanMoveGameObject(source, target, afterTarget);
        }

        private bool CanDropScnFolder(DragEventArgs e)
        {
            if (DataContext is not ScnFileViewModel viewModel) return false;
            if (!TryGetDraggedScnFolder(e, out var source)) return false;
            if (e.OriginalSource is not DependencyObject originalSource) return false;
            if (FindAncestor<TreeViewItem>(originalSource) is not TreeViewItem targetItem) return false;
            if (targetItem.DataContext is not ScnFile.FolderData target) return false;

            bool afterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            var sources = viewModel.SelectedScnFolders.Contains(source) ? viewModel.SelectedScnFolders : [source];
            return viewModel.CanMoveFolders(sources, target, afterTarget);
        }

        private bool TryDropScnFolder(ScnFileViewModel viewModel, DragEventArgs e)
        {
            if (!TryGetDraggedScnFolder(e, out var source)) return false;
            if (e.OriginalSource is not DependencyObject originalSource) return false;
            if (FindAncestor<TreeViewItem>(originalSource) is not TreeViewItem targetItem) return false;
            if (targetItem.DataContext is not ScnFile.FolderData target) return false;

            bool afterTarget = e.GetPosition(targetItem).Y > targetItem.ActualHeight / 2;
            var sources = viewModel.SelectedScnFolders.Contains(source) ? viewModel.SelectedScnFolders.ToList() : [source];
            if (viewModel.MoveFolders(sources, target, afterTarget))
            {
                SetScnFolderSelection(viewModel, sources);
                viewModel.SelectedScnFolderAnchor = source;
                ClearScnGameObjectSelection(viewModel);
                viewModel.SelectedItem = source;
            }
            e.Handled = true;
            return true;
        }

        private static bool TryGetDraggedScnGameObject(DragEventArgs e, out ScnFile.GameObjectData gameObject)
        {
            if (e.Data.GetData(typeof(ScnFile.GameObjectData)) is ScnFile.GameObjectData data)
            {
                gameObject = data;
                return true;
            }
            gameObject = null!;
            return false;
        }

        private static bool TryGetDraggedScnFolder(DragEventArgs e, out ScnFile.FolderData folder)
        {
            if (e.Data.GetData(typeof(ScnFile.FolderData)) is ScnFile.FolderData data)
            {
                folder = data;
                return true;
            }
            folder = null!;
            return false;
        }

        private void ResetScnDrag()
        {
            scnGameObjectDragSource = null;
            scnFolderDragSource = null;
            scnGameObjectDragStopwatch.Reset();
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T item) return item;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
