using System.Collections.Generic;
using System.Linq;
using RszTool.App.Common;

namespace RszTool.App.ViewModels
{
    public class UserFileViewModel(UserFile file) : BaseRszFileViewModel
    {
        public override BaseRszFile File => UserFile;
        public UserFile UserFile { get; } = file;

        public bool ResourceChanged
        {
            get => UserFile.ResourceChanged;
            set => UserFile.ResourceChanged = value;
        }

        public RszViewModel RszViewModel => new(UserFile.RSZ!);
        public RelayCommand CopyResourceInfo => new(OnCopyResourceInfo);
        public RelayCommand PasteResourceInfo => new(OnPasteResourceInfo);
        public RelayCommand RemoveResourceInfo => new(OnRemoveResourceInfo);

        private static List<ResourceInfo> CopiedResourceInfos { get; set; } = new();

        public override IEnumerable<object> TreeViewItems
        {
            get
            {
                yield return new TreeItemViewModel("Resources", UserFile.ResourceInfoList);
                yield return new TreeItemViewModel("Instances", RszViewModel.Instances);
                yield return new TreeItemViewModel("Objects", RszViewModel.Objects);
            }
        }

        private void OnCopyResourceInfo(object arg)
        {
            var items = SelectedResourceInfos.Count > 0
                ? SelectedResourceInfos.ToList()
                : arg is ResourceInfo resourceInfo ? [resourceInfo] : [];
            CopiedResourceInfos = items.Select(CloneResourceInfo).ToList();
        }

        private void OnPasteResourceInfo(object arg)
        {
            if (CopiedResourceInfos.Count == 0) return;
            int index = arg is ResourceInfo resourceInfo ? UserFile.ResourceInfoList.IndexOf(resourceInfo) + 1 : UserFile.ResourceInfoList.Count;
            foreach (var item in CopiedResourceInfos.Select(CloneResourceInfo))
            {
                UserFile.ResourceInfoList.Insert(index++, item);
            }
            ResourceChanged = true;
            Changed = true;
        }

        private void OnRemoveResourceInfo(object arg)
        {
            var items = SelectedResourceInfos.Count > 0
                ? SelectedResourceInfos.ToList()
                : arg is ResourceInfo resourceInfo ? [resourceInfo] : [];
            foreach (var item in items)
            {
                UserFile.ResourceInfoList.Remove(item);
            }
            SelectedResourceInfos.Clear();
            SelectedResourceInfoAnchor = null;
            ResourceChanged = true;
            Changed = true;
        }

        private static ResourceInfo CloneResourceInfo(ResourceInfo resourceInfo)
        {
            return new ResourceInfo
            {
                Path = resourceInfo.Path,
                HasOffset = resourceInfo.HasOffset,
                pathOffset = resourceInfo.pathOffset,
            };
        }
    }
}
