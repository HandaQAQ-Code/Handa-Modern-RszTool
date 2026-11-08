using System.ComponentModel;
using System.IO;
using System.Windows;
using RszTool.App.Common;

namespace RszTool.App.ViewModels;

/// <summary>One shared UVAR document for the manager and every GUID selector.</summary>
public sealed class UvarWorkspace : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public UvarFileViewModel? Current { get; private set; }
    public string DocumentTitle => Current == null ? "尚未设置 UVAR 文件" : Current.FilePath + (Current.Changed ? " * 未保存" : "");

    public UvarFileViewModel RequireDocument()
    {
        string path = App.Instance.SaveData.UvarFilePath;
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("请先在“设置”中选择 uvar.3 文件。");
        if (Current != null && SamePath(Current.FilePath!, path)) return Current;
        if (!Configure(path)) throw new OperationCanceledException("已取消切换 UVAR 文件。");
        return Current!;
    }

    public bool Configure(string path)
    {
        path = Path.GetFullPath(path.Trim());
        if (!File.Exists(path)) throw new FileNotFoundException("找不到 UVAR 文件。", path);
        if (!path.EndsWith(".uvar.3", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("请选择 .uvar.3 文件。");
        if (Current != null && SamePath(Current.FilePath!, path))
        {
            App.Instance.SaveData.UvarFilePath = path;
            App.Instance.SaveSettings();
            return true;
        }
        var candidate = new UvarFileViewModel(new(new RszFileOption(App.Instance.SaveData.GameName, false), new(path)));
        try
        {
            if (!candidate.Read()) throw new InvalidDataException("无法读取 UVAR 文件。");
            if (!ConfirmPendingChanges()) { candidate.File.Dispose(); return false; }
            string previousPath = App.Instance.SaveData.UvarFilePath;
            App.Instance.SaveData.UvarFilePath = path;
            try { App.Instance.SaveSettings(); }
            catch { App.Instance.SaveData.UvarFilePath = previousPath; throw; }
            if (Current != null)
            {
                Current.HeaderChanged -= NotifyTitle;
                Current.File.Dispose();
            }
            Current = candidate;
            Current.HeaderChanged += NotifyTitle;
            PropertyChanged?.Invoke(this, new(nameof(Current)));
            NotifyTitle();
            return true;
        }
        catch { candidate.File.Dispose(); throw; }
    }

    public bool ConfirmPendingChanges()
    {
        if (Current?.Changed != true) return true;
        var choice = MessageBoxUtils.YesNoCancel($"UVAR 有未保存修改：\n{Current.FilePath}\n是否保存？");
        if (choice == MessageBoxResult.Cancel) return false;
        if (choice == MessageBoxResult.Yes && !Current.Save()) throw new IOException("保存 UVAR 失败，修改仍然保留。");
        return true;
    }

    public void Reopen()
    {
        var document = RequireDocument();
        if (ConfirmPendingChanges() && !document.Reopen()) throw new IOException("重新读取 UVAR 失败。");
        NotifyTitle();
    }

    public void SaveAs(string path)
    {
        if (!path.EndsWith(".uvar.3", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UVAR 请另存为 .uvar.3 文件。");
        string previousPath = RequireDocument().FilePath!;
        if (!RequireDocument().SaveAs(path)) throw new IOException("另存 UVAR 失败。");
        App.Instance.SaveData.UvarFilePath = Path.GetFullPath(path);
        var locations = App.Instance.SaveData.UvarPickerLocations;
        if (locations.TryGetValue(Path.GetFullPath(previousPath).ToUpperInvariant(), out var location))
            locations[Path.GetFullPath(path).ToUpperInvariant()] = new UvarPickerLocation
            {
                GroupPath = location.GroupPath, Search = location.Search,
                SelectedGuid = location.SelectedGuid, UnusedOnly = location.UnusedOnly,
            };
        App.Instance.SaveSettings();
        NotifyTitle();
    }

    private void NotifyTitle() => PropertyChanged?.Invoke(this, new(nameof(DocumentTitle)));
    private static bool SamePath(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
}
