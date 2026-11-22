using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using Dragablz;
using Microsoft.Win32;
using RszTool.App.Common;
using RszTool.App.Resources;
using RszTool.App.Views;
using RszTool.Common;

namespace RszTool.App.ViewModels
{
    public class MainWindowModel : INotifyPropertyChanged
    {
        public static MainWindowModel? Instance { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<HeaderedItemViewModel> Items { get; } = new();
        public HeaderedItemViewModel? SelectedTabItem { get; set; }
        public FileExplorerViewModel FileExplorerViewModel { get; } = new();
        public UvarWorkspace UvarWorkspace => App.Instance.UvarWorkspace;
        public bool IsUvarMode { get; set; }
        public string UvarToggleText => IsUvarMode ? "返回文件编辑" : "UVAR / Flag 管理";

        
        private BaseRszFileViewModel? CurrentFile =>
            IsUvarMode ? UvarWorkspace.Current : SelectedTabItem is FileTabItemViewModel fileTabItemViewModel ?
            fileTabItemViewModel.FileViewModel : null;

       

        public CustomInterTabClient InterTabClient { get; } = new();
        public SaveData SaveData => App.Instance.SaveData;
        public static SaveData StaticSaveData => App.Instance.SaveData;

        public double AppFontSize
        {
            get => SaveData.AppFontSize;
            set
            {
                if (Math.Abs(SaveData.AppFontSize - value) > 0.01)
                {
                    SaveData.AppFontSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppFontSize)));
                    App.Instance.SaveSettings();
                }
            }
        }

        public record FontSizeOption(double Size, string Label);
        public static FontSizeOption[] FontSizeOptions { get; } = new[]
        {
            new FontSizeOption(11.0, "11 (极小)"),
            new FontSizeOption(12.0, "12 (小)"),
            new FontSizeOption(13.0, "13 (标准)"),
            new FontSizeOption(14.0, "14 (适中)"),
            new FontSizeOption(15.0, "15 (较大)"),
            new FontSizeOption(16.0, "16 (大)"),
            new FontSizeOption(18.0, "18 (特大)"),
        };

        public static string[] RowHeightOptions { get; } = new[]
        {
            "20 (极紧凑)",
            "24 (紧凑)",
            "26 (较紧凑)",
            "28 (紧凑标准)",
            "30 (默认标准)",
            "34 (宽松)",
            "38 (舒适)",
            "44 (较大)",
            "50 (特大)",
        };

        public double TreeItemHeight
        {
            get => SaveData.TreeItemHeight;
            set
            {
                var clamped = Math.Max(0.0, Math.Min(100.0, value));
                if (Math.Abs(SaveData.TreeItemHeight - clamped) > 0.01)
                {
                    SaveData.TreeItemHeight = clamped;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TreeItemHeight)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TreeItemHeightText)));
                    App.Instance.ApplyDisplaySettings();
                    App.Instance.SaveSettings();
                }
            }
        }

        public string TreeItemHeightText
        {
            get
            {
                var prefix = $"{TreeItemHeight:0.#} ";
                var opt = RowHeightOptions.FirstOrDefault(o => o.StartsWith(prefix));
                return opt ?? $"{TreeItemHeight:0.#}";
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                string clean = value;
                int paren = clean.IndexOf('(');
                if (paren >= 0) clean = clean.Substring(0, paren).Trim();
                if (double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed) ||
                    double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out parsed))
                {
                    TreeItemHeight = parsed;
                }
            }
        }

        public double AnimationSpeed
        {
            get => SaveData.AnimationSpeed;
            set
            {
                if (Math.Abs(SaveData.AnimationSpeed - value) > 0.01)
                {
                    SaveData.AnimationSpeed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnimationSpeed)));
                    App.Instance.ApplyDisplaySettings();
                    App.Instance.SaveSettings();
                }
            }
        }

        public record AnimationSpeedOption(double Speed, string Label);
        public static AnimationSpeedOption[] AnimationSpeedOptions { get; } = new[]
        {
            new AnimationSpeedOption(0.0, "0x (关闭动画)"),
            new AnimationSpeedOption(0.5, "0.5x (慢速)"),
            new AnimationSpeedOption(1.0, "1.0x (标准)"),
            new AnimationSpeedOption(1.5, "1.5x (快速)"),
            new AnimationSpeedOption(2.0, "2.0x (极速)"),
            new AnimationSpeedOption(3.0, "3.0x (飞速)"),
        };

        public RelayCommand OpenCommand => new(OnOpen);
        public RelayCommand SaveCommand => new(OnSave);
        public RelayCommand SaveAsCommand => new(OnSaveAs);
        public RelayCommand ReopenCommand => new(OnReopen);
        public RelayCommand AddFolderCommand => new(OnAddFolder);
        public RelayCommand CloseCommand => new(OnClose);
        public RelayCommand QuitCommand => new(OnQuit);
        public RelayCommand ClearRecentFilesHistory => new(OnClearRecentFilesHistory);
        public RelayCommand OpenRecentFile => new(OnOpenRecentFile);
        public RelayCommand OpenAbout => new(OnOpenAbout);
        public RelayCommand OpenSettings => new(_ => AppUtils.TryActionSimple(ShowSettings));
        public RelayCommand OpenFileAssociationCommand => new(_ => AppUtils.TryActionSimple(ShowFileAssociation));
        public RelayCommand ToggleUvar => new(_ => AppUtils.TryActionSimple(OnToggleUvar));

        private void ShowSettings()
        {
            new UvarSettingsWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        private void ShowFileAssociation()
        {
            new FileAssociationWindow { Owner = Application.Current.MainWindow }.ShowDialog();
        }

        private void OnToggleUvar()
        {
            if (IsUvarMode) { IsUvarMode = false; return; }
            if (string.IsNullOrWhiteSpace(SaveData.UvarFilePath)) ShowSettings();
            if (string.IsNullOrWhiteSpace(SaveData.UvarFilePath)) return;
            UvarWorkspace.RequireDocument();
            IsUvarMode = true;
        }

        public ItemActionCallback ClosingTabItemHandler => ClosingTabItemHandlerImpl;

        public MainWindowModel()
        {
            Instance = this;
            foreach (var path in SaveData.OpenedFolders)
            {
                FileExplorerViewModel.Folders.Add(new(path));
            }
            if (FileExplorerViewModel.Folders.Count == 0)
            {
                FileExplorerViewModel.Folders.Add(new(Directory.GetCurrentDirectory()));
            }
            FileExplorerViewModel.OnFileSelected += f => OpenFile(f.Path);
        }

        

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="path"></param>
        public void OpenFile(string path)
        {
            path = Path.GetFullPath(path);
            var fileType = RszUtils.GetFileType(path);
            if (fileType == FileType.uvar)
            {
                if (UvarWorkspace.Configure(path))
                {
                    IsUvarMode = true;
                    SaveData.AddRecentFile(path);
                }
                return;
            }
            // check file opened
            foreach (var item in Items)
            {
                if (item is FileTabItemViewModel fileTab)
                {
                    if (fileTab.FileViewModel.FilePath == path)
                    {
                        SelectedTabItem = item;
                        IsUvarMode = false;
                        return;
                    }
                }
            }

            string rszJsonFile = $"rsz{SaveData.GameName}.json";
            if (!File.Exists(rszJsonFile))
            {
                MessageBoxUtils.Warning(string.Format(Texts.RszJsonNotFound, rszJsonFile));
                return;
            }

            BaseRszFileViewModel? fileViewModel = null;
            ContentControl? content = null;
            RszFileOption option = new(SaveData.GameName);
            switch (fileType)
            {
                case FileType.user:
                    fileViewModel = new UserFileViewModel(new(option, new(path)));
                    content = new RszUserFileView();
                    break;
                case FileType.pfb:
                    fileViewModel = new PfbFileViewModel(new(option, new(path)));
                    content = new RszPfbFileView();
                    break;
                case FileType.scn:
                    fileViewModel = new ScnFileViewModel(new(option, new(path)));
                    content = new RszScnFileView();
                    break;
            }
            if (fileViewModel != null && content != null)
            {
                bool readSuccess = false;
                for (int tryCount = 0; tryCount < 5; tryCount++)
                {
                    try
                    {
                        readSuccess = fileViewModel.Read();
                        break;
                    }
                    catch (RszRetryOpenException e)
                    {
                        fileViewModel.File.FileHandler.Seek(0);
                        MessageBoxUtils.Info(string.Format(Texts.TryReopen, e.Message));
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                if (!readSuccess)
                {
                    MessageBoxUtils.Error(Texts.ReadFailed);
                    return;
                }
                content.DataContext = fileViewModel;
                HeaderedItemViewModel header = new FileTabItemViewModel(fileViewModel, content, this);
                Items.Add(header);
                SelectedTabItem = header;
                IsUvarMode = false;

                SaveData.AddRecentFile(path);
            }
            else
            {
                MessageBoxUtils.Info(Texts.NotSupportedFormat);
            }
        }

        public void TryOpenFile(string path)
        {
            AppUtils.TryAction(() => OpenFile(path));
        }

        public void OnDropFile(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                TryOpenFile(file);
            }
        }

        /// <summary>
        /// Callback to handle tab closing.
        /// </summary>
        private void ClosingTabItemHandlerImpl(ItemActionCallbackArgs<TabablzControl> args)
        {
            if (args.DragablzItem.DataContext is not FileTabItemViewModel fileTab) return;
            if (!OnTabClose(fileTab))
            {
                args.Cancel();
            }
        }

        private static readonly (string, string)[] SupportedFile = {
            ("User file", "*.user.*"),
            ("Scene file", "*.scn.*"),
            ("Prefab file", "*.pfb.*"),
            ("UVAR flag file", "*.uvar.3"),
        };

        private static readonly string OpenFileFilter =
            $"All file|{string.Join(";", SupportedFile.Select(item => item.Item2))}|" +
            string.Join("|", SupportedFile.Select(item => $"{item.Item1}|{item.Item2}"));

        private void OnOpen(object arg)
        {
            var dialog = new OpenFileDialog
            {
                Filter = OpenFileFilter
            };
            if (dialog.ShowDialog() == true)
            {
                TryOpenFile(dialog.FileName);
            }
        }

        private void OnSave(object arg)
        {
            AppUtils.TryAction(() =>
            {
                if (CurrentFile != null && !CurrentFile.Save()) throw new IOException("保存文件失败。");
            });
        }

        private void OnSaveAs(object arg)
        {
            var currentFile = CurrentFile;
            if (currentFile == null) return;
            var dialog = new SaveFileDialog
            {
                FileName = currentFile.FileName,
                Filter = IsUvarMode ? "UVAR v3|*.uvar.3" : OpenFileFilter,
                DefaultExt = IsUvarMode ? ".uvar.3" : "",
                AddExtension = true,
            };
            if (dialog.ShowDialog() == true)
            {
                // Open document
                string? path = dialog.FileName;
                if (path != null)
                {
                    AppUtils.TryAction(() =>
                    {
                        if (IsUvarMode)
                        {
                            UvarWorkspace.SaveAs(path);
                            SaveData.AddRecentFile(path);
                        }
                        else if(currentFile.SaveAs(path))
                        {
                            SaveData.AddRecentFile(path);
                        }
                    });
                }
            }
        }

        private void OnReopen(object arg)
        {
            AppUtils.TryAction(() =>
            {
                if (IsUvarMode) UvarWorkspace.Reopen();
                else CurrentFile?.Reopen();
            });
        }

        private static bool OnTabClose(FileTabItemViewModel fileTab)
        {
            if (fileTab.FileViewModel.Changed)
            {
                // Check changed
                var result = MessageBoxUtils.YesNoCancel(
                    $"{Texts.FileChangedPrompt}\n{fileTab.FileViewModel.FilePath}");
                if (result == MessageBoxResult.Yes)
                {
                    AppUtils.TryAction(() => fileTab.FileViewModel.Save());
                }
                else if (result == MessageBoxResult.Cancel) return false;
            }
            return true;
        }

        private void OnClose(object arg)
        {
            if (IsUvarMode) { IsUvarMode = false; return; }
            if (SelectedTabItem is FileTabItemViewModel fileTab && OnTabClose(fileTab))
            {
                Items.Remove(fileTab);
            }
        }

        private void OnQuit(object arg)
        {
            if (OnExit())
            {
                Application.Current.Shutdown();
            }
        }

        private void OnClearRecentFilesHistory(object arg)
        {
            SaveData.RecentFiles.Clear();
        }

        private void OnOpenRecentFile(object arg)
        {
            if (arg is string path)
            {
                TryOpenFile(path);
            }
        }

        private void OnAddFolder(object arg)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true && dialog.FolderName != null)
            {
                FileExplorerViewModel.AddFolder(dialog.FolderName);
            }
        }

        public bool OnExit()
        {
            foreach (var item in Items)
            {
                if (item is FileTabItemViewModel fileTab)
                {
                    if (!OnTabClose(fileTab)) return false;
                }
            }
            bool canClose = false;
            AppUtils.TryActionSimple(() => canClose = UvarWorkspace.ConfirmPendingChanges());
            return canClose;
        }

        private void OnOpenAbout(object arg)
        {
            var about = new AboutWindow();
            about.ShowDialog();
        }

        public void CloseTab(FileTabItemViewModel tab)
        {
            if (OnTabClose(tab))
            {
                Items.Remove(tab);
            }
        }

        public void CloseOtherTabs(FileTabItemViewModel keepTab)
        {
            SelectedTabItem = keepTab;
            var toClose = Items.OfType<FileTabItemViewModel>().Where(t => t != keepTab).ToList();
            foreach (var tab in toClose)
            {
                if (!OnTabClose(tab)) break;
                Items.Remove(tab);
            }
        }

        public void CloseRightTabs(FileTabItemViewModel currentTab)
        {
            int index = Items.IndexOf(currentTab);
            if (index < 0) return;
            var toClose = Items.OfType<FileTabItemViewModel>().Skip(index + 1).ToList();
            foreach (var tab in toClose)
            {
                if (!OnTabClose(tab)) break;
                Items.Remove(tab);
            }
        }

        public void CloseAllTabs()
        {
            var toClose = Items.OfType<FileTabItemViewModel>().ToList();
            foreach (var tab in toClose)
            {
                if (!OnTabClose(tab)) break;
                Items.Remove(tab);
            }
        }
    }


    public class FileTabItemViewModel : HeaderedItemViewModel
    {
        public MainWindowModel? OwnerModel { get; set; }

        public FileTabItemViewModel(BaseRszFileViewModel fileViewModel, object content, MainWindowModel? owner = null, bool isSelected = false)
            : base(fileViewModel.FileName!, content, isSelected)
        {
            OwnerModel = owner;
            FileViewModel = fileViewModel;
            fileViewModel.HeaderChanged += UpdateHeader;
        }

        public BaseRszFileViewModel FileViewModel { get; set; }

        public void UpdateHeader()
        {
            Header = FileViewModel.FileName + (FileViewModel.Changed ? "*" : "");
        }

        private MainWindowModel? GetModel() => OwnerModel ?? MainWindowModel.Instance ?? (Application.Current?.MainWindow?.DataContext as MainWindowModel);

        public RelayCommand CloseCommand => new(_ => GetModel()?.CloseTab(this));
        public RelayCommand CloseOthersCommand => new(_ => GetModel()?.CloseOtherTabs(this));
        public RelayCommand CloseRightCommand => new(_ => GetModel()?.CloseRightTabs(this));
        public RelayCommand CloseAllCommand => new(_ => GetModel()?.CloseAllTabs());
        public RelayCommand CopyPathCommand => new(_ =>
        {
            if (!string.IsNullOrEmpty(FileViewModel.FilePath))
            {
                Clipboard.SetText(FileViewModel.FilePath);
            }
        });
        public RelayCommand OpenInExplorerCommand => new(_ =>
        {
            if (!string.IsNullOrEmpty(FileViewModel.FilePath))
            {
                try
                {
                    if (File.Exists(FileViewModel.FilePath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{FileViewModel.FilePath}\"");
                    }
                    else
                    {
                        string? dir = Path.GetDirectoryName(FileViewModel.FilePath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"\"{dir}\"");
                        }
                    }
                }
                catch { }
            }
        });
    }
}
