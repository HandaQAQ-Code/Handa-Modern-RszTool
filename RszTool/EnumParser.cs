using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace RszTool
{
    /// <summary>
    /// Rsz json parser
    /// </summary>
    public class EnumParser
    {
        private static readonly Dictionary<string, EnumParser> instanceDict = new();

        public static EnumParser GetInstance(string jsonPath)
        {
            string resolvedPath = ResolveFilePath(jsonPath);
            string fullKey = Path.GetFullPath(resolvedPath);
            if (!instanceDict.TryGetValue(fullKey, out var rszParser))
            {
                rszParser = new EnumParser(resolvedPath);
                instanceDict[fullKey] = rszParser;
            }
            return rszParser;
        }

        private static string ResolveFilePath(string path)
        {
            if (File.Exists(path)) return path;
            string baseCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            if (File.Exists(baseCandidate)) return baseCandidate;
            return path;
        }

        public EnumDict EnumDict { get; }

        public EnumParser(string jsonPath)
        {
            string resolvedPath = ResolveFilePath(jsonPath);
            if (File.Exists(resolvedPath))
            {
                using FileStream fileStream = File.OpenRead(resolvedPath);
                EnumDict = new(JsonSerializer.Deserialize<Dictionary<string, EnumData>>(fileStream) ?? []);
            }
            else
            {
                EnumDict = new([]);
            }

            // Candidates to search for localized enum json
            var candidateExtensions = new List<string>();
            var cultures = new[]
            {
                CultureInfo.CurrentUICulture.Name,
                CultureInfo.CurrentCulture.Name,
                Thread.CurrentThread.CurrentCulture.Name
            };

            foreach (var c in cultures)
            {
                if (!string.IsNullOrEmpty(c))
                {
                    candidateExtensions.Add($".{c}.json");
                    int dash = c.IndexOf('-');
                    if (dash > 0)
                    {
                        candidateExtensions.Add($".{c.Substring(0, dash)}.json");
                    }
                }
            }

            // Chinese fallbacks & standard default
            candidateExtensions.Add(".zh-CN.json");
            candidateExtensions.Add(".zh-Hans.json");
            candidateExtensions.Add(".zh.json");

            string? foundCulturePath = null;
            foreach (var ext in candidateExtensions.Distinct())
            {
                string candidate = Path.ChangeExtension(resolvedPath, ext);
                if (File.Exists(candidate))
                {
                    foundCulturePath = candidate;
                    break;
                }
                string baseCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.ChangeExtension(jsonPath, ext));
                if (File.Exists(baseCandidate))
                {
                    foundCulturePath = baseCandidate;
                    break;
                }
            }

            if (foundCulturePath != null && File.Exists(foundCulturePath))
            {
                try
                {
                    using FileStream fileStream = File.OpenRead(foundCulturePath);
                    var cultureDict = JsonSerializer.Deserialize<Dictionary<string, EnumData>>(fileStream);
                    if (cultureDict != null && cultureDict.Count > 0)
                    {
                        foreach (var item in cultureDict)
                        {
                            EnumDict.Dict[item.Key] = item.Value;
                        }
                    }
                    Debug.WriteLine($"[EnumParser] Successfully loaded localized enums from: {foundCulturePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[EnumParser] Failed to load localized enums from {foundCulturePath}: {ex}");
                }
            }
        }
    }

    public record EnumDict(Dictionary<string, EnumData> Dict)
    {
        public EnumData? this[string name] => Dict.TryGetValue(name, out var data) ? data : null;
    }

    public record EnumData(EnumItem[] Items, bool Flags);
    public record EnumItem(string Name, ulong Value)
    {
        public string DisplayName => $"{Name} ({Value})";
    }
}
