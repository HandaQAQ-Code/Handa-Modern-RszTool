using System.Collections.ObjectModel;
using RszTool.Common;

namespace RszTool.Test;

internal static class FeatureVerification
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
    public static int Uvar(string path)
    {
        byte[] source = File.ReadAllBytes(path);
        var option = new RszFileOption(GameName.re4, loadSchema: false);
        using var file = new UvarFile(option, new FileHandler(path));
        Check(file.Read(), "UVAR read failed");
        var groups = file.Groups.ToList();
        int variables = groups.Sum(x => x.Variables.Count), flags = groups.Sum(x => x.Variables.Count(v => v.IsFlag));
        Check(file.Write(), "Unedited UVAR write failed");
        Check(((MemoryStream)file.FileHandler.Stream).ToArray().SequenceEqual(source), "Unedited UVAR is not byte identical");
        var snapshot = groups.Select(g => g.Variables.Select(v => (v.Id, v.Name, v.TypeBits, v.DefaultValue)).ToArray()).ToArray();
        var target = groups.Skip(1).First(g => g.Variables.Count > 1 && g.Variables.Any(x => x.IsFlag));
        var removed = target.Variables.First(x => x.IsFlag);
        target.Variables.Remove(removed);
        var flag = file.AddFlag(target, "Retool_Verification_" + Guid.NewGuid().ToString("N"), true);
        var rootFlag = file.AddFlag(file.Root, "Retool_Root_Verification_" + Guid.NewGuid().ToString("N"));
        var toggled = groups.SelectMany(x => x.Variables).First(x => x.IsFlag && x != flag && x != rootFlag);
        bool originalDefault = toggled.DefaultValue;
        toggled.DefaultValue = !originalDefault;
        string outputDirectory = Path.Combine(Path.GetTempPath(), "RszTool-feature-verification", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);
        string output = Path.Combine(outputDirectory, "globalvariables.uvar.3");
        Check(file.SaveAs(output), "SaveAs failed");
        byte[] firstSave = File.ReadAllBytes(output);
        Check(file.Save(), "Repeated save failed");
        Check(File.ReadAllBytes(output).SequenceEqual(firstSave), "Repeated save keeps growing or changes data");
        Check(file.LastBackupPath != null && File.ReadAllBytes(file.LastBackupPath).SequenceEqual(firstSave), "Automatic backup failed");
        using var reopened = new UvarFile(option, new FileHandler(output));
        Check(reopened.Read(), "Edited UVAR reopen failed");
        var newGroups = reopened.Groups.ToList();
        Check(newGroups.Count == groups.Count, "Embedded groups changed");
        for (int i = 0; i < groups.Count; i++)
        {
            Check(groups[i].Offset == newGroups[i].Offset && groups[i].Name == newGroups[i].Name, "Embedded group moved");
            foreach (var expected in snapshot[i])
            {
                if (expected.Id == removed.Id) continue;
                var actual = newGroups[i].Variables.Single(x => x.Id == expected.Id);
                Check(actual.Name == expected.Name && actual.TypeBits == expected.TypeBits, "Unrelated variable changed");
                Check(actual.DefaultValue == (actual.Id == toggled.Id ? !expected.DefaultValue : expected.DefaultValue), "Default flag unexpectedly changed");
            }
        }
        Check(newGroups.SelectMany(x => x.Variables).All(x => x.Id != removed.Id), "Deleted flag survived");
        Check(newGroups.SelectMany(x => x.Variables).Single(x => x.Id == flag.Id).DefaultValue, "New flag default missing");
        // Payloads (including expressions) stay byte identical. Only table offsets/count in headers can change.
        var headerBytes = new HashSet<long>(groups.SelectMany(g => Enumerable.Range(16, 8).Concat(Enumerable.Range(32, 8)).Concat(Enumerable.Range(44, 2)).Select(i => g.Offset + i)));
        for (int i = 0; i < source.Length; i++) Check(headerBytes.Contains(i) || source[i] == firstSave[i], $"Original payload changed at {i:X}");
        var storePath = Path.Combine(outputDirectory, "usage.json");
        var store = new UvarUsageStore(storePath);
        store.SetMany(output, new[] { (flag.Id, true, "测试备注"), (rootFlag.Id, true, "Multi select") });
        var reloaded = new UvarUsageStore(storePath);
        Check(reloaded.Get(output, flag.Id).Used && reloaded.Get(output, rootFlag.Id).Used, "Multi-mark persistence failed");
        Check(reloaded.Get(output, flag.Id).Note == "测试备注", "Unicode note lost");
        string alias = Path.Combine(outputDirectory, "renamed.uvar.3");
        reloaded.CopyFileMarks(output, alias);
        Check(new UvarUsageStore(storePath).Get(alias, flag.Id).Used, "SaveAs marker migration failed");
        Check(!reloaded.Get(path, flag.Id).Used, "Markers leaked to another mod file");
        reloaded.SetMany(output, new[] { (flag.Id, false, "Cleared") });
        Check(!new UvarUsageStore(storePath).Get(output, flag.Id).Used, "Unmark persistence failed");
        Check(File.ReadAllBytes(path).SequenceEqual(source), "Source UVAR was modified");
        // Delete all entries in a group and ensure zero-variable hash tables are readable.
        var emptied = newGroups.First(x => x.Variables.Count > 0);
        emptied.Variables.Clear();
        Check(reopened.Write(), "Empty group could not serialize");
        Console.WriteLine($"PASS UVAR: groups={groups.Count}, variables={variables}, flags={flags}; byte-identical no-op; nested/root add/delete/default; hashes; payload preservation; backup; stable repeat-save; multi-mark/note/unmark/SaveAs persistence.\nVerification copy: {output}");
        return 0;
    }

    private static RszField Field(string name, RszFieldType type, string originalType = "", bool array = false) =>
        new() { name = name, type = type, original_type = originalType, array = array };
    private static RszInstance Instance(string name, RszField[] fields, params object[] values) =>
        new(new RszClass { name = name, fields = fields, typeId = 1, crc = 1 }, values: values);

    public static int Ids()
    {
        RszInstance Context(int index) => Instance("chainsaw.ContextID", [Field("_Category", RszFieldType.S8), Field("_Kind", RszFieldType.U8), Field("_Group", RszFieldType.S32), Field("_Index", RszFieldType.S32)], (sbyte)2, (byte)3, 4, index);
        var parentContext = Context(10); var first = Context(11); var second = Context(12);
        var nested = Instance("fixture.Nested", [Field("context", RszFieldType.Object), Field("stage", RszFieldType.S32, "chainsaw.StageID"), Field("_DropItemID", RszFieldType.S32, "chainsaw.ItemID", true)], first, 5, new List<object> { 1, 2 });
        var stageWrapper = Instance("chainsaw.StageID", [Field("value__", RszFieldType.S32)], 6);
        var itemWrapper = Instance("chainsaw.ItemID", [Field("value__", RszFieldType.S32)], 7);
        var component = Instance("fixture.Component", [Field("nested", RszFieldType.Object, array: true), Field("wrapper", RszFieldType.Object), Field("item", RszFieldType.Object), Field("other", RszFieldType.Object)], new List<object> { nested, nested, second }, stageWrapper, itemWrapper, RszInstance.NULL);
        var parent = new FixtureObject("parent", [parentContext]);
        var child = new FixtureObject("child", [component]);
        parent.Children.Add(child);
        var plan = Re4IdEditor.Plan(parent, false, 77, 100, 2, 88, 99);
        Check(plan.Count == 8, $"Expected 8 edits including drop item arrays; got {plan.Count}");
        Re4IdEditor.Apply(plan);
        Check((int)parentContext.GetFieldValue("_Index")! == 10, "Parent should be excluded");
        Check((int)first.GetFieldValue("_Index")! == 100 && (int)second.GetFieldValue("_Index")! == 102, "Nested/shared Context sequence wrong");
        Check((sbyte)first.GetFieldValue("_Category")! == 2 && (byte)first.GetFieldValue("_Kind")! == 3, "Unselected context fields changed");
        Check((int)nested.GetFieldValue("stage")! == 88 && (int)stageWrapper.GetFieldValue("value__")! == 88, "Stage scalar/wrapper not edited");
        Check(((List<object>)nested.GetFieldValue("_DropItemID")!).All(x => (int)x == 99) && (int)itemWrapper.GetFieldValue("value__")! == 7, "Drop item edit changed an unrelated ItemID wrapper");
        Re4IdEditor.Apply(Re4IdEditor.Plan(parent, true, 2, 200, 1, null, null));
        Check((int)parentContext.GetFieldValue("_Index")! == 200, "Include parent failed");
        var clone = (RszInstance)component.Clone();
        Re4IdEditor.SetContexts(Re4IdEditor.Contexts([clone]), 9, 500, 1);
        Check((int)first.GetFieldValue("_Index")! == 201, "Clone edit changed source");
        int prior = (int)first.GetFieldValue("_Group")!;
        try { Re4IdEditor.SetContexts([first, second], 999, int.MaxValue, 1); throw new Exception("Overflow should fail"); }
        catch (OverflowException) { }
        Check((int)first.GetFieldValue("_Group")! == prior, "Overflow caused partial edit");
        // A cyclic reference must not hang and a shared reference must only appear once.
        component.Values[3] = component;
        Check(Re4IdEditor.Contexts([component]).Count == 2, "Cycle/shared reference traversal failed");
        Console.WriteLine("PASS ID editor: recursive children; parent toggle; embedded/array ContextID; shared refs/cycles; enum scalars/arrays/wrappers; source isolation; overflow rollback.");
        return 0;
    }

    public static int DuplicateContexts(string path)
    {
        string source = Path.GetFullPath(path);
        string runtime = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "RszTool.App", "bin", "Release", "net8.0-windows"));
        Directory.SetCurrentDirectory(runtime);
        var option = new RszFileOption(GameName.re4);
        using var file = new UserFile(option, new FileHandler(source));
        Check(file.Read(), "USER read failed");
        var rsz = file.RSZ!;
        var array = Re4IdEditor.Walk(rsz.ObjectList.Select(x => (x.Name, x)))
            .SelectMany(x => x.Instance.Values.OfType<List<object>>())
            .FirstOrDefault(x => x.OfType<RszInstance>().Any(v => Re4IdEditor.Contexts([v]).Count > 0));
        if (array == null) throw new InvalidDataException("No ContextID-bearing array found in this USER file.");
        var sourceObject = array.OfType<RszInstance>().First(x => Re4IdEditor.Contexts([x]).Count > 0);
        var sourceIds = Re4IdEditor.Contexts([sourceObject]);
        var originalIds = sourceIds.Select(x => (int)x.GetFieldValue("_Index")!).ToArray();
        var copies = Enumerable.Range(0, 3).Select(_ => rsz.ArrayInsertInstance(array, sourceObject)).ToList();
        var contexts = Re4IdEditor.Contexts(copies);
        Re4IdEditor.SetContexts(contexts, 900, 40000, 2);
        Check(sourceIds.Select(x => (int)x.GetFieldValue("_Index")!).SequenceEqual(originalIds), "Source IDs changed");
        Check(contexts.Select(x => (int)x.GetFieldValue("_Index")!).SequenceEqual(Enumerable.Range(0, contexts.Count).Select(i => 40000 + 2 * i)), "Duplicate ID sequence failed");
        using var output = new FileHandler();
        Check(file.WriteTo(output, false), "USER roundtrip write failed");
        using var reopened = new UserFile(option, output.AsMemory());
        Check(reopened.Read(), "Duplicated USER could not reopen");
        Check(Re4IdEditor.Contexts(reopened.RSZ!.ObjectList).Count(x => (int)x.GetFieldValue("_Group")! == 900) >= contexts.Count, "Duplicate contexts did not persist");
        Console.WriteLine($"PASS real USER duplicate: source={sourceObject.RszClass.name}, copies=3, newContexts={contexts.Count}, sequential IDs, source unchanged, save/reopen.");
        return 0;
    }

    private sealed class FixtureObject(string name, IEnumerable<RszInstance> components) : IGameObjectData
    {
        public string? Name { get; } = name;
        public RszInstance? Instance => null;
        public ObservableCollection<RszInstance> Components { get; } = new(components);
        public List<IGameObjectData> Children { get; } = new();
        public IEnumerable<IGameObjectData> GetChildren() => Children;
    }
}
