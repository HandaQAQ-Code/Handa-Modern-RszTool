using System.ComponentModel;

namespace RszTool.App.ViewModels
{
    public abstract class BaseTreeItemViewModel(string name) : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public string Name { get; set; } = name;
        public abstract IEnumerable<object>? Items { get; }

        public override string ToString()
        {
            return Name;
        }
    }


    public class TreeItemViewModel(string name, IEnumerable<object>? items) : BaseTreeItemViewModel(name)
    {
        public override IEnumerable<object>? Items { get; } = items;
    }


    public class TreeItemDelegate(string name, Func<IEnumerable<object>>? itemsFunc) : BaseTreeItemViewModel(name)
    {
        public Func<IEnumerable<object>>? ItemsFunc { get; set; } = itemsFunc;
        public override IEnumerable<object>? Items => ItemsFunc?.Invoke() ?? null;
    }


    public class GameObjectsHeader(string name, IEnumerable<object>? items) : TreeItemViewModel(name, items);
    public class FoldersHeader(string name, IEnumerable<object>? items) : TreeItemViewModel(name, items);

    public class ScnGameObjectsHeader(
        string name,
        IEnumerable<ScnFile.GameObjectData>? items,
        ScnFile.FolderData? folder = null,
        ScnFile.GameObjectData? parent = null) : TreeItemViewModel(name, items)
    {
        public ScnFile.FolderData? Folder { get; } = folder;
        public ScnFile.GameObjectData? Parent { get; } = parent;
    }
}
