using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using RszTool.App.Common;
using RszTool.App.ViewModels;

namespace RszTool.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            SaveData? saveData = null;
            if (File.Exists(SaveData.JsonPath))
            {
                try
                {
                    using FileStream fileStream = File.OpenRead(SaveData.JsonPath);
                    saveData = JsonSerializer.Deserialize<SaveData>(fileStream);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to load SaveData.json: {ex}");
                    try
                    {
                        File.Copy(SaveData.JsonPath, SaveData.JsonPath + ".corrupted.bak", true);
                    }
                    catch { }
                }
            }
            SaveData = saveData ?? new();
        }

        public static App Instance => (App)Current;
        public SaveData SaveData { get; }
        public UvarWorkspace UvarWorkspace { get; } = new();

        public void SaveSettings()
        {
            string path = Path.GetFullPath(SaveData.JsonPath);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                JsonUtils.DumpJson(temporary, SaveData);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ThemeManager.Init(SaveData.CurTheme);
            SaveData.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(SaveData.CurTheme))
                {
                    ThemeManager.Instance.CurrentTheme = SaveData.CurTheme;
                    SaveSettings();
                }
            };
            // 添加全局异常处理程序
            SetupUnhandledExceptionHandling();

            if (e.Args.Length > 0)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    if (MainWindow?.DataContext is MainWindowModel model)
                        foreach (var path in e.Args.Where(File.Exists)) model.TryOpenFile(Path.GetFullPath(path));
                }));
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            SaveSettings();
        }

        private void SetupUnhandledExceptionHandling()
        {
            // Catch exceptions from all threads in the AppDomain.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                ShowUnhandledException((Exception)args.ExceptionObject, "AppDomain.CurrentDomain.UnhandledException", false);

            // Catch exceptions from each AppDomain that uses a task scheduler for async operations.
            TaskScheduler.UnobservedTaskException += (sender, args) =>
                ShowUnhandledException(args.Exception, "TaskScheduler.UnobservedTaskException", false);

            // Catch exceptions from a single specific UI dispatcher thread.
            Dispatcher.UnhandledException += (sender, args) =>
            {
                // If we are debugging, let Visual Studio handle the exception and take us to the code that threw it.
                if (!Debugger.IsAttached)
                {
                    args.Handled = true;
                    ShowUnhandledException(args.Exception, "Dispatcher.UnhandledException", true);
                }
            };

            // Catch exceptions from the main UI dispatcher thread.
            // Typically we only need to catch this OR the Dispatcher.UnhandledException.
            // Handling both can result in the exception getting handled twice.
            // Application.Current.DispatcherUnhandledException += (sender, args) =>
            // {
            //     // If we are debugging, let Visual Studio handle the exception and take us to the code that threw it.
            //     if (!Debugger.IsAttached)
            //     {
            //         args.Handled = true;
            //         ShowUnhandledException(args.Exception, "Application.Current.DispatcherUnhandledException", true);
            //     }
            // };
        }

        public static void ShowUnhandledException(Exception e, string unhandledExceptionType, bool promptUserForShutdown = false)
        {

            try { File.WriteAllText("crash.log", $"{unhandledExceptionType}\n{e}"); } catch {}
            var messageBoxTitle = $"Unexpected Error Occurred: {unhandledExceptionType}";
            var messageBoxMessage = $"The following exception occurred:\n\n{e}";
            var messageBoxButtons = MessageBoxButton.OK;

            if (promptUserForShutdown)
            {
                messageBoxMessage += "\n\nShould we close it?";
                messageBoxButtons = MessageBoxButton.YesNo;
            }

            // Let the user decide if the app should die or not (if applicable).
            if (Views.ModernMessageBoxWindow.Show(messageBoxMessage, messageBoxTitle, messageBoxButtons, MessageBoxImage.Error, Application.Current?.MainWindow) == MessageBoxResult.Yes &&
                promptUserForShutdown)
            {
                Application.Current?.Shutdown();
            }
        }
    }
}
