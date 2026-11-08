using System.Collections.ObjectModel;
using System.Text;
using RszTool.Common;

namespace RszTool;

/// <summary>
/// UVAR v3 editor. Existing payloads and expression trees retain their original addresses.
/// Changed variable/hash tables are appended, with only the owning header redirected.
/// </summary>
public sealed class UvarFile(RszFileOption option, FileHandler handler) : BaseRszFile(option, handler)
{
    private byte[] original = [];
    public UvarGroup Root { get; private set; } = null!;
    public IEnumerable<UvarGroup> Groups => Enumerate(Root);
    public string? LastBackupPath { get; private set; }

    public override bool Save() => SaveAs(FileHandler.FilePath ?? throw new InvalidOperationException("No file path."));

    public override bool SaveAs(string path)
    {
        if (!Write()) return false;
        path = System.IO.Path.GetFullPath(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var target = System.IO.File.Create(temporary))
            {
                FileHandler.Stream.Position = 0;
                FileHandler.Stream.CopyTo(target);
                target.Flush(true);
            }
            if (System.IO.File.Exists(path))
            {
                LastBackupPath = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".bak";
                System.IO.File.Replace(temporary, path, LastBackupPath);
            }
            else System.IO.File.Move(temporary, path);
            FileHandler.Stream.Dispose();
            FileHandler.Open(path);
            return true;
        }
        finally { if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
    }
    private static IEnumerable<UvarGroup> Enumerate(UvarGroup node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in Enumerate(child)) yield return descendant;
    }

    protected override bool DoRead()
    {
        using var buffer = new MemoryStream();
        FileHandler.Stream.CopyTo(buffer);
        original = buffer.ToArray();
        Root = Parse(original);
        return true;
    }

    public static UvarGroup Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Encoding.Unicode);
        var visited = new HashSet<long>();
        void Range(long offset, long count)
        {
            if (offset < 0 || count < 0 || offset > bytes.LongLength - count)
                throw new InvalidDataException("UVAR contains an invalid offset/count.");
        }
        string Text(long offset)
        {
            Range(offset, 2);
            var text = new StringBuilder();
            stream.Position = offset;
            while (true)
            {
                Range(stream.Position, 2);
                char c = (char)reader.ReadUInt16();
                if (c == 0) return text.ToString();
                text.Append(c);
            }
        }
        UvarGroup Read(long offset, string path, int depth)
        {
            if (depth > 128 || !visited.Add(offset)) throw new InvalidDataException("Cyclic or overly deep UVAR hierarchy.");
            Range(offset, 48);
            stream.Position = offset;
            uint version = reader.ReadUInt32(), magic = reader.ReadUInt32();
            if (version != 3 || magic != 0x72617675) throw new InvalidDataException("Only UVAR version 3 is supported.");
            long strings = reader.ReadInt64(), data = reader.ReadInt64(), children = reader.ReadInt64(), hash = reader.ReadInt64();
            uint nameHash = reader.ReadUInt32();
            int count = reader.ReadUInt16(), childCount = reader.ReadUInt16();
            var group = new UvarGroup(offset, Text(checked(offset + strings)), path);
            group.Path = string.IsNullOrEmpty(path) ? group.Name : path + "/" + group.Name;
            Range(checked(offset + data), count * 48L);
            for (int i = 0; i < count; i++)
            {
                stream.Position = offset + data + i * 48L;
                byte[] record = reader.ReadBytes(48);
                var guid = new Guid(record.Take(16).ToArray());
                long name = BitConverter.ToInt64(record, 16), value = BitConverter.ToInt64(record, 24);
                uint typeBits = BitConverter.ToUInt32(record, 40);
                string variableName = Text(checked(offset + name));
                bool isFlag = (typeBits & 0xffffff) == 2 && (typeBits & 0x40000000) == 0;
                bool flagValue = false;
                if (isFlag && value != 0) { Range(checked(offset + value), 1); flagValue = bytes[offset + value] != 0; }
                group.Variables.Add(new UvarVariable(guid, variableName, typeBits, record, flagValue));
            }
            group.OriginalVariables = group.Variables.ToArray();
            if (count > 0)
            {
                Range(checked(offset + hash), 32);
                stream.Position = offset + hash;
                var tableOffsets = Enumerable.Range(0, 4).Select(_ => reader.ReadInt64()).ToArray();
                Range(checked(offset + tableOffsets[0]), count * 16L);
                for (int t = 1; t < 4; t++) Range(checked(offset + tableOffsets[t]), count * 4L);
                for (int i = 0; i < count; i++)
                {
                    stream.Position = offset + tableOffsets[1] + i * 4L;
                    uint gi = reader.ReadUInt32();
                    stream.Position = offset + tableOffsets[3] + i * 4L;
                    uint ni = reader.ReadUInt32();
                    if (gi >= count || ni >= count) throw new InvalidDataException("Invalid UVAR hash index.");
                    stream.Position = offset + tableOffsets[0] + i * 16L;
                    if (new Guid(reader.ReadBytes(16)) != group.Variables[(int)gi].Id) throw new InvalidDataException("UVAR GUID map mismatch.");
                    stream.Position = offset + tableOffsets[2] + i * 4L;
                    if (reader.ReadUInt32() != BitConverter.ToUInt32(group.Variables[(int)ni].Record!, 44)) throw new InvalidDataException("UVAR name hash map mismatch.");
                }
            }
            Range(checked(offset + children), childCount * 8L);
            for (int i = 0; i < childCount; i++)
            {
                stream.Position = offset + children + i * 8L;
                long child = reader.ReadInt64();
                group.Children.Add(Read(checked(offset + child), group.Path + $"[{i}]", depth + 1));
            }
            return group;
        }
        return Read(0, "", 0);
    }

    public UvarVariable AddFlag(UvarGroup group, string name, bool value = false)
        => AddFlags(group, [name], value)[0];

    public List<UvarVariable> AddFlags(UvarGroup group, IEnumerable<string> names, bool value = false)
    {
        if (!Groups.Contains(group)) throw new ArgumentException("Group does not belong to this file.");
        var list = names.Select(x => x.Trim()).ToList();
        if (list.Count > short.MaxValue - group.Variables.Count) throw new InvalidOperationException("UVAR group has too many variables.");
        var hashes = new HashSet<uint>(group.Variables.Select(x => MurMur3HashUtils.GetHash(x.Name)));
        foreach (string name in list)
        {
            if (name.Length == 0 || name.IndexOf('\0') >= 0) throw new ArgumentException("Flag name is empty or invalid.");
            if (!hashes.Add(MurMur3HashUtils.GetHash(name))) throw new ArgumentException($"名称或名称哈希冲突：{name}");
        }
        var entries = list.Select(name => new UvarVariable(Guid.NewGuid(), name, 2, null, value)).ToList();
        foreach (var entry in entries) group.Variables.Add(entry);
        return entries;
    }

    protected override bool DoWrite()
    {
        using var output = new MemoryStream();
        output.Write(original, 0, original.Length);
        using var writer = new BinaryWriter(output, Encoding.Unicode, true);
        void Align() { while (output.Position % 16 != 0) writer.Write((byte)0); }
        void Patch(long position, long value)
        {
            long end = output.Position; output.Position = position; writer.Write(value); output.Position = end;
        }
        foreach (var group in Groups)
        {
            if (!group.IsModified) continue;
            if (group.Variables.Count > short.MaxValue) throw new InvalidOperationException("Too many UVAR variables.");
            if (group.Variables.Select(x => x.Id).Distinct().Count() != group.Variables.Count) throw new InvalidDataException("Duplicate flag GUID.");
            var records = new List<byte[]>();
            foreach (var variable in group.Variables)
            {
                byte[] record = variable.Record == null ? new byte[48] : (byte[])variable.Record.Clone();
                if (variable.Record == null)
                {
                    variable.Id.ToByteArray().CopyTo(record, 0);
                    BitConverter.GetBytes(output.Position - group.Offset).CopyTo(record, 16);
                    writer.Write(Encoding.Unicode.GetBytes(variable.Name + "\0"));
                    BitConverter.GetBytes(variable.TypeBits).CopyTo(record, 40);
                    BitConverter.GetBytes(MurMur3HashUtils.GetHash(variable.Name)).CopyTo(record, 44);
                }
                if (variable.Record == null || variable.DefaultValue != variable.OriginalValue)
                {
                    Align();
                    BitConverter.GetBytes(output.Position - group.Offset).CopyTo(record, 24);
                    writer.Write(variable.DefaultValue ? 1u : 0u);
                }
                records.Add(record);
            }
            Align();
            Patch(group.Offset + 16, output.Position - group.Offset);
            foreach (var record in records) writer.Write(record);
            Align();
            long hashOffset = output.Position - group.Offset;
            Patch(group.Offset + 32, hashOffset);
            int count = records.Count;
            long guids = hashOffset + 32, guidMap = guids + count * 16L, hashes = guidMap + count * 4L, hashMap = hashes + count * 4L;
            writer.Write(guids); writer.Write(guidMap); writer.Write(hashes); writer.Write(hashMap);
            var byGuid = Enumerable.Range(0, count).OrderBy(i => group.Variables[i].Id).ToArray();
            var byHash = Enumerable.Range(0, count).OrderBy(i => BitConverter.ToUInt32(records[i], 44)).ToArray();
            foreach (int i in byGuid) writer.Write(group.Variables[i].Id.ToByteArray());
            foreach (int i in byGuid) writer.Write((uint)i);
            foreach (int i in byHash) writer.Write(BitConverter.ToUInt32(records[i], 44));
            foreach (int i in byHash) writer.Write((uint)i);
            long end = output.Position;
            output.Position = group.Offset + 44; writer.Write((ushort)count); output.Position = end;
        }
        byte[] result = output.ToArray();
        Parse(result); // Refuse to replace a file if the rewritten tables cannot be read back.
        FileHandler.Clear();
        FileHandler.Stream.Write(result, 0, result.Length);
        return true;
    }
}

public sealed class UvarGroup(long offset, string name, string path)
{
    public long Offset { get; } = offset;
    public string Name { get; } = name;
    public string Path { get; internal set; } = path;
    public ObservableCollection<UvarVariable> Variables { get; } = new();
    public List<UvarGroup> Children { get; } = new();
    internal UvarVariable[] OriginalVariables { get; set; } = [];
    internal bool IsModified => !Variables.SequenceEqual(OriginalVariables) || Variables.Any(x => x.DefaultValue != x.OriginalValue);
    public override string ToString() => Path;
}

public sealed class UvarVariable(Guid id, string name, uint typeBits, byte[]? record, bool value)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public uint TypeBits { get; } = typeBits;
    public bool IsFlag => (TypeBits & 0xffffff) == 2 && (TypeBits & 0x40000000) == 0;
    public bool DefaultValue { get; set; } = value;
    internal bool OriginalValue { get; } = value;
    internal byte[]? Record { get; } = record;
}
