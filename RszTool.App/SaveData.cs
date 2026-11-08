using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using RszTool.Common;

namespace RszTool.App
{
    public class SaveData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public const string JsonPath = "RszTool.App.SaveData.json";

        [JsonConverter(typeof(EnumJsonConverter<GameName>))]
        public GameName GameName { get; set; } = GameName.re4;

        private ThemeE _curTheme = ThemeE.Winui;
        [JsonConverter(typeof(EnumJsonConverter<ThemeE>))]
        public ThemeE CurTheme
        {
            get => _curTheme;
            set
            {
                if (_curTheme != value)
                {
                    _curTheme = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurTheme)));
                }
            }
        }

        private double _appFontSize = 13.0;
        public double AppFontSize
        {
            get => _appFontSize <= 0 ? 13.0 : _appFontSize;
            set
            {
                if (Math.Abs(_appFontSize - value) > 0.01)
                {
                    _appFontSize = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppFontSize)));
                }
            }
        }
        public ObservableCollection<string> RecentFiles { get; set; } = new();
        public List<string> OpenedFolders { get; set; } = new();
        public ContextIDData LastContextID { get; set; } = new();
        public string TransformPointsJsonPath { get; set; } = string.Empty;
        public string UvarFilePath { get; set; } = string.Empty;
        public Dictionary<string, UvarPickerLocation> UvarPickerLocations { get; set; } = new();
        public bool BatchGroupSameNameObjects { get; set; } = true;
        public List<DropWeightSetting> DropWeights { get; set; } = new();
        public List<MontageIdPreset> MontageIdPresets { get; set; } = new();
        public Dictionary<string, MontageIdPoolSetting> MontageIdPools { get; set; } = new();
        public string LastMontageIdPreset { get; set; } = "";

        public void AddRecentFile(string path)
        {
            int recentIndex = RecentFiles.IndexOf(path);
            if (recentIndex >= 0)
            {
                RecentFiles.Move(recentIndex, 0);
            }
            else
            {
                RecentFiles.Insert(0, path);
            }
        }
    }

    public sealed class DropWeightSetting
    {
        public int ItemId { get; set; }
        public decimal Weight { get; set; } = 1;
    }

    public sealed class MontageIdPoolSetting
    {
        public bool IncludeEnumValues { get; set; } = true;
        public string CustomIds { get; set; } = "";
    }

    public sealed class MontageIdPreset
    {
        public string Name { get; set; } = "";
        public Dictionary<string, MontageIdPoolSetting> Pools { get; set; } = new();
        public override string ToString() => Name;
    }

    public sealed class UvarPickerLocation
    {
        public string GroupPath { get; set; } = "";
        public string Search { get; set; } = "";
        public string SelectedGuid { get; set; } = "";
        public bool UnusedOnly { get; set; }
    }


    public class ContextIDData : INotifyPropertyChanged
    {
        public int Group { get; set; }
        public int Index { get; set; }

        public string Text
        {
            get => $"Last ContextID Index: {Index} Group: {Group}";
            set { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
