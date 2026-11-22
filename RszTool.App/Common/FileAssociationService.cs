using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RszTool.App.Common
{
    public record ExtensionAssociationInfo(
        string Extension,
        string ProgId,
        string Description,
        string Category
    );

    public static class FileAssociationService
    {
        public static readonly ExtensionAssociationInfo[] SupportedExtensions = new[]
        {
            new ExtensionAssociationInfo(".scn", "RszTool.scn", "RE Engine 场景文件 (Scene File)", "场景与关卡"),
            new ExtensionAssociationInfo(".user", "RszTool.user", "RE Engine 用户数据文件 (User Data File)", "参数与属性"),
            new ExtensionAssociationInfo(".pfb", "RszTool.pfb", "RE Engine 预制件文件 (Prefab File)", "实体与预制件"),
            new ExtensionAssociationInfo(".uvar", "RszTool.uvar", "RE Engine UVAR 变量与标志库文件 (UVAR File)", "变量与Flag"),
        };

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static string GetExecutablePath()
        {
            string? exe = null;
            try
            {
                exe = Process.GetCurrentProcess().MainModule?.FileName;
            }
            catch { }

            if (string.IsNullOrEmpty(exe))
            {
                exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RszTool.App.exe");
            }

            if (exe != null && exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string candidate = exe.Substring(0, exe.Length - 4) + ".exe";
                if (File.Exists(candidate)) exe = candidate;
            }

            return Path.GetFullPath(exe ?? "RszTool.App.exe");
        }

        public static bool IsExtensionAssociated(string extension, string progId, string exePath)
        {
            try
            {
                using var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}");
                if (extKey == null) return false;
                var currentProgId = extKey.GetValue(null) as string;
                if (!string.Equals(currentProgId, progId, StringComparison.OrdinalIgnoreCase))
                {
                    using var openWithKey = extKey.OpenSubKey("OpenWithProgids");
                    if (openWithKey == null || openWithKey.GetValue(progId) == null)
                        return false;
                }

                using var cmdKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}\shell\open\command");
                if (cmdKey == null) return false;
                var command = cmdKey.GetValue(null) as string;
                return command != null && command.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool RegisterExtension(string extension, string progId, string description, string exePath)
        {
            try
            {
                using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
                {
                    progKey?.SetValue(null, description);
                }

                using (var iconKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\DefaultIcon"))
                {
                    iconKey?.SetValue(null, $"\"{exePath}\",0");
                }

                using (var cmdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\shell\open\command"))
                {
                    cmdKey?.SetValue(null, $"\"{exePath}\" \"%1\"");
                }

                using (var openKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}\shell\open"))
                {
                    openKey?.SetValue("FriendlyAppName", "Handa Modern RszTool");
                }

                using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
                {
                    extKey?.SetValue(null, progId);
                    using var openWith = extKey?.CreateSubKey("OpenWithProgids");
                    openWith?.SetValue(progId, string.Empty);
                }

                try
                {
                    using var fileExtKey = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids");
                    fileExtKey?.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register {extension}: {ex}");
                return false;
            }
        }

        public static bool UnregisterExtension(string extension, string progId)
        {
            try
            {
                using (var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}", true))
                {
                    if (extKey != null)
                    {
                        if (string.Equals(extKey.GetValue(null) as string, progId, StringComparison.OrdinalIgnoreCase))
                        {
                            extKey.DeleteValue(string.Empty, false);
                        }
                        using var openWith = extKey.OpenSubKey("OpenWithProgids", true);
                        openWith?.DeleteValue(progId, false);
                    }
                }

                try
                {
                    using var fileExtKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\OpenWithProgids", true);
                    fileExtKey?.DeleteValue(progId, false);
                }
                catch { }

                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{progId}", false);
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to unregister {extension}: {ex}");
                return false;
            }
        }

        public static bool IsContextMenuRegistered(string exePath)
        {
            try
            {
                using var cmdKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\*\shell\RszTool.Open\command");
                if (cmdKey == null) return false;
                var cmd = cmdKey.GetValue(null) as string;
                return cmd != null && cmd.IndexOf(exePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool RegisterContextMenu(string exePath, string menuText = "用 RszTool 打开")
        {
            try
            {
                using var shellKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\*\shell\RszTool.Open");
                if (shellKey == null) return false;
                shellKey.SetValue(null, menuText);
                shellKey.SetValue("Icon", $"\"{exePath}\",0");

                using var cmdKey = shellKey.CreateSubKey("command");
                cmdKey?.SetValue(null, $"\"{exePath}\" \"%1\"");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register context menu: {ex}");
                return false;
            }
        }

        public static bool UnregisterContextMenu()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\RszTool.Open", false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to unregister context menu: {ex}");
                return false;
            }
        }

        public static void NotifyShell()
        {
            try
            {
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }
    }
}
