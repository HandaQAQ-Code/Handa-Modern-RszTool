using System.Globalization;

namespace RszTool;

/// <summary>Traverses embedded RSZ references, including arrays, once; external userdata stays external.</summary>
public static class Re4IdEditor
{
    public static IEnumerable<(string Path, RszInstance Instance)> Walk(IEnumerable<(string Path, RszInstance Instance)> roots)
    {
        var visited = new HashSet<RszInstance>();
        var pending = new Stack<(string Path, RszInstance Instance)>(roots.Reverse());
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current.Instance) || current.Instance.RSZUserData != null) continue;
            yield return current;
            for (int i = current.Instance.Fields.Length - 1; i >= 0; i--)
            {
                var field = current.Instance.Fields[i];
                if (!field.IsReference) continue;
                if (current.Instance.Values[i] is RszInstance child)
                    pending.Push(($"{current.Path}/{field.name}", child));
                else if (current.Instance.Values[i] is List<object> array)
                    for (int j = array.Count - 1; j >= 0; j--)
                        if (array[j] is RszInstance element)
                            pending.Push(($"{current.Path}/{field.name}[{j}]", element));
            }
        }
    }

    public static IEnumerable<(string Path, RszInstance Instance)> GameObjectRoots(IGameObjectData root, bool includeRoot)
        => GameObjectRoots([root], includeRoot);

    public static IEnumerable<(string Path, IGameObjectData Object)> GameObjects(IEnumerable<IGameObjectData> roots, bool includeRoots = true)
    {
        var visited = new HashSet<IGameObjectData>();
        var source = roots.Distinct().ToList();
        var excluded = includeRoots ? new HashSet<IGameObjectData>() : new HashSet<IGameObjectData>(source);
        var pending = new Stack<(IGameObjectData Object, string Path)>();
        for (int i = source.Count - 1; i >= 0; i--) pending.Push((source[i], $"{source[i].Name ?? "GameObject"}[{i}]"));
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current.Object)) continue;
            if (!excluded.Contains(current.Object)) yield return (current.Path, current.Object);
            var children = current.Object.GetChildren().ToList();
            for (int i = children.Count - 1; i >= 0; i--)
                pending.Push((children[i], $"{current.Path}/{children[i].Name}[{i}]"));
        }
    }

    public static IEnumerable<(string Path, RszInstance Instance)> GameObjectRoots(IEnumerable<IGameObjectData> roots, bool includeRoots)
        => ExactObjectRoots(GameObjects(roots, includeRoots));

    public static IEnumerable<(string Path, RszInstance Instance)> ExactObjectRoots(IEnumerable<(string Path, IGameObjectData Object)> objects)
    {
        foreach (var (path, gameObject) in objects)
        {
            if (gameObject.Instance != null) yield return (path, gameObject.Instance);
            for (int i = 0; i < gameObject.Components.Count; i++)
                yield return ($"{path}/{gameObject.Components[i].RszClass.name}[{i}]", gameObject.Components[i]);
        }
    }

    public static string FieldType(RszInstance instance, RszField field) =>
        field.name == "value__" ? instance.RszClass.name : RszInstance.GetElementType(field.original_type);

    public static List<RszInstance> Contexts(IEnumerable<RszInstance> roots) =>
        Walk(roots.Select(x => (x.Name, x)))
            .Where(x => x.Instance.RszClass.name == "chainsaw.ContextID").Select(x => x.Instance).ToList();

    public static void SetContexts(IEnumerable<RszInstance> contexts, int group, int start, int step)
    {
        var edits = new List<IdFieldEdit>();
        int n = 0;
        foreach (var context in contexts.Distinct())
        {
            edits.Add(new(context.Name, context, context.RszClass.IndexOfField("_Group"), group));
            edits.Add(new(context.Name, context, context.RszClass.IndexOfField("_Index"), checked(start + checked(step * n++))));
        }
        Apply(edits);
    }

    public static List<IdFieldEdit> Plan(IGameObjectData root, bool includeRoot,
        int? group, int? start, int step, int? stage, int? item)
        => Plan([root], includeRoot, group, start, step, stage, item);

    public static List<IdFieldEdit> Plan(IEnumerable<IGameObjectData> roots, bool includeRoots,
        int? group, int? start, int step, int? stage, int? item)
        => PlanExact(GameObjects(roots, includeRoots), group, start, step, stage, item);

    /// <summary>Only the explicitly selected objects; descendants must be selected separately.</summary>
    public static List<IdFieldEdit> PlanExact(IEnumerable<(string Path, IGameObjectData Object)> objects,
        int? group, int? start, int step, int? stage, int? item,
        bool? shouldDropItem = null, bool? shouldDropItemAtRandom = null)
    {
        var edits = new List<IdFieldEdit>();
        int contextNumber = 0;
        foreach (var (path, instance) in Walk(ExactObjectRoots(objects)))
        {
            if (instance.RszClass.name == "chainsaw.ContextID")
            {
                if (group.HasValue) edits.Add(new(path, instance, instance.RszClass.IndexOfField("_Group"), group.Value));
                if (start.HasValue) edits.Add(new(path, instance, instance.RszClass.IndexOfField("_Index"), checked(start.Value + checked(step * contextNumber))));
                contextNumber++;
            }
            for (int i = 0; i < instance.Fields.Length; i++)
            {
                var field = instance.Fields[i];
                if (field.IsReference) continue;
                bool? dropFlag = field.name == "_ShouldDropItem" ? shouldDropItem :
                    field.name == "_ShouldDropItemAtRandom" ? shouldDropItemAtRandom : null;
                if (dropFlag.HasValue && !field.array && field.type == RszFieldType.Bool)
                    edits.Add(new(path, instance, i, dropFlag.Value));
                var type = FieldType(instance, field);
                int? value = type == "chainsaw.StageID" ? stage : field.name == "_DropItemID" ? item : null;
                if (!value.HasValue) continue;
                if (field.array && instance.Values[i] is List<object> values)
                    for (int j = 0; j < values.Count; j++) edits.Add(new(path, instance, i, value.Value, j));
                else edits.Add(new(path, instance, i, value.Value));
            }
        }
        return edits;
    }

    public static void Apply(IEnumerable<IdFieldEdit> edits)
    {
        // Convert every value before writing any field so validation failure cannot leave a half edit.
        var prepared = edits.Select(x => (Edit: x, Value: Convert.ChangeType(x.NewValue, x.OldValue.GetType(), CultureInfo.InvariantCulture))).ToList();
        foreach (var (edit, value) in prepared)
        {
            if (edit.ArrayIndex is int index) ((List<object>)edit.Instance.Values[edit.FieldIndex])[index] = value;
            else edit.Instance.SetFieldValue(edit.FieldIndex, value);
        }
    }
}

public sealed class IdFieldEdit
{
    public IdFieldEdit(string path, RszInstance instance, int fieldIndex, object value, int? arrayIndex = null)
    {
        if (fieldIndex < 0) throw new InvalidDataException($"Missing ContextID field in {instance.Name}");
        Path = path; Instance = instance; FieldIndex = fieldIndex; NewValue = value; ArrayIndex = arrayIndex;
        OldValue = arrayIndex is int index ? ((List<object>)instance.Values[fieldIndex])[index] : instance.Values[fieldIndex];
    }
    public string Path { get; }
    public RszInstance Instance { get; }
    public int FieldIndex { get; }
    public int? ArrayIndex { get; }
    public string Field => Instance.Fields[FieldIndex].name + (ArrayIndex is int i ? $"[{i}]" : "");
    public object OldValue { get; }
    public object NewValue { get; }
}
