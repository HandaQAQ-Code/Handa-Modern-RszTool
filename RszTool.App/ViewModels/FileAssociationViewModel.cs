using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using RszTool.App.Common;

namespace RszTool.App.ViewModels
{
    public class ExtensionItemViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Extension { get; set; } = string.Empty;
        public string ProgId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public bool IsSelected { get; set; } = true;
        public bool IsCurrentlyAssociated { get; set; }

        public string StatusText => IsCurrentlyAssociated ? "已关联" : "未关联";
    }

    public class FileAssociationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string ExecutablePath { get; }

        public ObservableCollection<ExtensionItemViewModel> Extensions { get; } = new();

        public bool IsContextMenuSelected { get; set; } = true;
        public bool IsContextMenuCurrentlyAssociated { get; set; }

        public string ContextMenuStatusText => IsContextMenuCurrentlyAssociated ? "已添加" : "未添加";

        public FileAssociationViewModel()
        {
            ExecutablePath = FileAssociationService.GetExecutablePath();
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            Extensions.Clear();
            foreach (var info in FileAssociationService.SupportedExtensions)
            {
                bool isAssoc = FileAssociationService.IsExtensionAssociated(info.Extension, info.ProgId, ExecutablePath);
                Extensions.Add(new ExtensionItemViewModel
                {
                    Extension = info.Extension,
                    ProgId = info.ProgId,
                    Description = info.Description,
                    Category = info.Category,
                    IsSelected = true,
                    IsCurrentlyAssociated = isAssoc,
                });
            }

            IsContextMenuCurrentlyAssociated = FileAssociationService.IsContextMenuRegistered(ExecutablePath);
            IsContextMenuSelected = true;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsContextMenuCurrentlyAssociated)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContextMenuStatusText)));
        }

        public void ApplyAssociations()
        {
            int successCount = 0;
            foreach (var ext in Extensions)
            {
                if (ext.IsSelected)
                {
                    if (FileAssociationService.RegisterExtension(ext.Extension, ext.ProgId, ext.Description, ExecutablePath))
                    {
                        successCount++;
                    }
                }
                else
                {
                    FileAssociationService.UnregisterExtension(ext.Extension, ext.ProgId);
                }
            }

            if (IsContextMenuSelected)
            {
                FileAssociationService.RegisterContextMenu(ExecutablePath);
            }
            else
            {
                FileAssociationService.UnregisterContextMenu();
            }

            FileAssociationService.NotifyShell();
            RefreshStatus();

            MessageBoxUtils.Info($"文件关联与右键菜单设置已生效！\n\n已成功配置 {successCount} 个格式关联{(IsContextMenuSelected ? "，并已添加资源管理器右键菜单" : "")}。\n在 Windows 资源管理器中双击文件即可直接使用 RszTool 打开。");
        }

        public void UnregisterAll()
        {
            foreach (var ext in Extensions)
            {
                FileAssociationService.UnregisterExtension(ext.Extension, ext.ProgId);
            }
            FileAssociationService.UnregisterContextMenu();

            FileAssociationService.NotifyShell();
            RefreshStatus();

            MessageBoxUtils.Info("已成功清除所有扩展名关联与资源管理器右键菜单。");
        }
    }
}
