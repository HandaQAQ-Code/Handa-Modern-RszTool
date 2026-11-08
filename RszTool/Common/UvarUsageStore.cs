using System.Text.Json;

namespace RszTool.Common;

/// <summary>Local annotations, deliberately separate from game flag default values.</summary>
public sealed class UvarUsageStore
{
    public sealed class Usage
    {
        public bool Used { get; set; }
        public string Note { get; set; } = "";
    }
    private Dictionary<string, Usage> entries;
    public string StorePath { get; }
    public UvarUsageStore(string path)
    {
        StorePath = path;
        entries = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, Usage>>(File.ReadAllText(path)) ?? new() : new();
    }
    private static string Prefix(string path) => Path.GetFullPath(path).ToUpperInvariant() + "|";
    private static string Key(string path, Guid id) => Prefix(path) + id.ToString("D");
    public Usage Get(string path, Guid id) => entries.TryGetValue(Key(path, id), out var value)
        ? new Usage { Used = value.Used, Note = value.Note } : new();
    public void SetMany(string path, IEnumerable<(Guid Id, bool Used, string Note)> values)
    {
        var next = new Dictionary<string, Usage>(entries);
        foreach (var item in values) next[Key(path, item.Id)] = new Usage { Used = item.Used, Note = item.Note };
        Persist(next);
    }
    public void CopyFileMarks(string source, string destination)
    {
        string prefix = Prefix(source), target = Prefix(destination);
        if (prefix == target) return;
        var next = new Dictionary<string, Usage>(entries);
        foreach (var item in entries.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal)))
            next[target + item.Key.Substring(prefix.Length)] = item.Value;
        Persist(next);
    }
    private void Persist(Dictionary<string, Usage> next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(StorePath))!);
        string temporary = StorePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(next));
            if (File.Exists(StorePath)) File.Replace(temporary, StorePath, StorePath + ".bak");
            else File.Move(temporary, StorePath);
            entries = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
