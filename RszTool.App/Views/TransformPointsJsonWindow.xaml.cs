using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App.Views
{
    public partial class TransformPointsJsonWindow : Wpf.Ui.Controls.FluentWindow, INotifyPropertyChanged
    {
        private string jsonPath = string.Empty;
        private TransformPointJsonGroup? selectedGroup;
        private string statusText = "Select a JSON file.";

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<TransformPointJsonGroup> Groups { get; } = new();
        public ObservableCollection<TransformPointJson> Points { get; } = new();
        public TransformPointJson? SelectedPoint { get; private set; }

        public string JsonPath
        {
            get => jsonPath;
            set
            {
                if (jsonPath == value) return;
                jsonPath = value;
                RszTool.App.App.Instance.SaveData.TransformPointsJsonPath = value;
                OnPropertyChanged(nameof(JsonPath));
            }
        }

        public TransformPointJsonGroup? SelectedGroup
        {
            get => selectedGroup;
            set
            {
                if (ReferenceEquals(selectedGroup, value)) return;
                selectedGroup = value;
                Points.Clear();
                if (value != null)
                {
                    foreach (TransformPointJson point in value.Points)
                    {
                        Points.Add(point);
                    }
                }
                OnPropertyChanged(nameof(SelectedGroup));
                OnPropertyChanged(nameof(Points));
            }
        }

        public string StatusText
        {
            get => statusText;
            private set
            {
                if (statusText == value) return;
                statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public TransformPointsJsonWindow()
        {
            InitializeComponent();
            DataContext = this;
            JsonPath = RszTool.App.App.Instance.SaveData.TransformPointsJsonPath;
            if (File.Exists(JsonPath))
            {
                LoadJson(false);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON files|*.json|All files|*.*",
                CheckFileExists = true,
            };
            string directory = Path.GetDirectoryName(JsonPath) ?? string.Empty;
            if (Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }

            if (dialog.ShowDialog(this) == true)
            {
                JsonPath = dialog.FileName;
                LoadJson(true);
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadJson(true);
        }

        private void LoadJson(bool showError)
        {
            string path = JsonPath.Trim();
            if (path.Length == 0)
            {
                StatusText = "JSON path is empty.";
                if (showError) MessageBoxUtils.Error(StatusText);
                return;
            }
            if (!File.Exists(path))
            {
                StatusText = "JSON file not found.";
                if (showError) MessageBoxUtils.Error($"JSON file not found: {path}");
                return;
            }

            try
            {
                TransformPointsJsonFile file = TransformPointsJsonReader.Load(path);
                Groups.Clear();
                foreach (TransformPointJsonGroup group in file.Groups)
                {
                    Groups.Add(group);
                }

                SelectedGroup = Groups.FirstOrDefault(group =>
                    !string.IsNullOrWhiteSpace(file.ActiveGroup) &&
                    string.Equals(group.Name, file.ActiveGroup, StringComparison.OrdinalIgnoreCase)) ??
                    Groups.FirstOrDefault();
                StatusText = $"Loaded {Points.Count} point(s) from {Path.GetFileName(path)}.";
            }
            catch (Exception ex)
            {
                Groups.Clear();
                SelectedGroup = null;
                StatusText = "Failed to load JSON.";
                if (showError) MessageBoxUtils.Error($"Failed to load JSON: {ex.Message}");
            }
        }

        private void PointList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PointList.SelectedItem is not TransformPointJson point)
            {
                return;
            }

            SelectedPoint = point;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
