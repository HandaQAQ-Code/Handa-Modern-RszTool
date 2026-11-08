using System.ComponentModel;
using System.IO;
using System.Windows.Data;

namespace RszTool.App.ViewModels;

public sealed class UvarGuidPickerViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public UvarFileViewModel Document { get; }
    public ListCollectionView Flags { get; }
    public IEnumerable<UvarGroupChoice> Groups => Document.Groups;
    public string FilePath => Document.FilePath!;
    private string locationKey => Path.GetFullPath(FilePath).ToUpperInvariant();
    private string lastSelectedGuid = "";
    private UvarFlagRow? selectedFlag;
    public UvarFlagRow? SelectedFlag
    {
        get => selectedFlag;
        set { selectedFlag = value; if (value != null) lastSelectedGuid = value.Id; }
    }
    private UvarGroupChoice? selectedGroup;
    public UvarGroupChoice? SelectedGroup
    {
        get => selectedGroup;
        set { selectedGroup = value; Flags.Refresh(); }
    }
    private string search = "";
    public string Search { get => search; set { search = value; Flags.Refresh(); } }
    private bool unusedOnly;
    public bool UnusedOnly { get => unusedOnly; set { unusedOnly = value; Flags.Refresh(); } }

    public UvarGuidPickerViewModel(UvarFileViewModel document, Guid currentValue)
    {
        Document = document;
        var rows = document.AllRows.Where(x => x.Variable.IsFlag).ToList();
        Flags = new ListCollectionView(rows);
        Flags.Filter = value => value is UvarFlagRow row &&
            (selectedGroup?.Group == null || row.Group == selectedGroup.Group) && (!unusedOnly || !row.Used) &&
            (string.IsNullOrWhiteSpace(search) || new[] { row.Name, row.GroupPath, row.Id, row.Note }
                .Any(x => x.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
        App.Instance.SaveData.UvarPickerLocations.TryGetValue(locationKey, out var stored);
        selectedGroup = document.Groups.FirstOrDefault(x => x.Group?.Path == stored?.GroupPath) ?? document.Groups[0];
        search = stored?.Search ?? "";
        unusedOnly = stored?.UnusedOnly ?? false;
        lastSelectedGuid = stored?.SelectedGuid ?? "";
        var target = rows.FirstOrDefault(x => x.Id == lastSelectedGuid) ?? rows.FirstOrDefault(x => x.Variable.Id == currentValue);
        // A flag selected last time may now be marked used; keep that remembered row reachable.
        if (target?.Used == true && target.Id == lastSelectedGuid) unusedOnly = false;
        Flags.Refresh();
        if (target != null && Flags.Contains(target)) SelectedFlag = target;
        else if (stored == null && target != null)
        {
            selectedGroup = document.Groups.First(x => x.Group == target.Group);
            unusedOnly = false;
            Flags.Refresh();
            SelectedFlag = target;
        }
    }

    public void RememberPosition()
    {
        App.Instance.SaveData.UvarPickerLocations[locationKey] = new UvarPickerLocation
        {
            GroupPath = SelectedGroup?.Group?.Path ?? "",
            Search = Search,
            SelectedGuid = lastSelectedGuid,
            UnusedOnly = UnusedOnly,
        };
        App.Instance.SaveSettings();
    }
}
