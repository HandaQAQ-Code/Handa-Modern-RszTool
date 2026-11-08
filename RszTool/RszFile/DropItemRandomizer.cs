namespace RszTool;

public sealed class DropChoice(int itemId, decimal weight)
{
    public int ItemId { get; } = itemId;
    public decimal Weight { get; } = weight;
}

public sealed class DropAssignment(string path, int itemId, IReadOnlyList<IdFieldEdit> edits, int objectCount = 1)
{
    public string Path { get; } = path;
    public int ItemId { get; } = itemId;
    public IReadOnlyList<IdFieldEdit> Edits { get; } = edits;
    public int ObjectCount { get; } = objectCount;
}

public static class DropItemRandomizer
{
    public static List<IdFieldEdit> ItemFields((string Path, IGameObjectData Object) target)
        => Re4IdEditor.PlanExact([target], null, null, 0, null, 0);

    /// <summary>Largest remainder quotas preserve the requested ratios; Fisher-Yates randomizes placement.</summary>
    public static List<DropAssignment> Plan(IEnumerable<(string Path, IGameObjectData Object)> targets,
        IEnumerable<DropChoice> choices, int seed)
    {
        var weights = choices.ToList();
        if (weights.Count == 0 || weights.Any(x => x.Weight < 0) || weights.All(x => x.Weight == 0))
            throw new ArgumentException("请添加掉落物并设置至少一个大于 0 的比例；比例不能为负数。");
        var combined = weights.GroupBy(x => x.ItemId).Select(x => new DropChoice(x.Key, x.Sum(v => v.Weight))).Where(x => x.Weight > 0).ToList();
        decimal total = combined.Sum(x => x.Weight);
        var candidates = targets.Select(target => (target.Path, Fields: ItemFields(target))).Where(x => x.Fields.Count > 0).ToList();
        var parent = Enumerable.Range(0, candidates.Count).ToArray();
        int Find(int index)
        {
            while (parent[index] != index) { parent[index] = parent[parent[index]]; index = parent[index]; }
            return index;
        }
        var owners = new Dictionary<(RszInstance, int, int?), int>();
        for (int i = 0; i < candidates.Count; i++)
            foreach (var field in candidates[i].Fields)
            {
                var key = (field.Instance, field.FieldIndex, field.ArrayIndex);
                if (owners.TryGetValue(key, out int previous)) parent[Find(i)] = Find(previous);
                else owners[key] = i;
            }
        var eligible = Enumerable.Range(0, candidates.Count).GroupBy(Find).Select(group =>
        {
            var seen = new HashSet<(RszInstance, int, int?)>();
            return (Path: string.Join(" + ", group.Select(i => candidates[i].Path)), Count: group.Count(),
                Fields: group.SelectMany(i => candidates[i].Fields).Where(x => seen.Add((x.Instance, x.FieldIndex, x.ArrayIndex))).ToList());
        }).ToList();
        if (eligible.Count == 0) throw new InvalidOperationException("选中的物体没有 _DropItemID 字段，请选择包含敌人 SpawnParam 的物体或子对象。");
        int count = eligible.Count;
        var quotas = combined.Select((x, i) => new { Choice = x, Order = i, Exact = count * (x.Weight / total) }).ToList();
        var counts = quotas.Select(x => (int)decimal.Floor(x.Exact)).ToArray();
        int remainder = count - counts.Sum();
        foreach (var quota in quotas.OrderByDescending(x => x.Exact - decimal.Floor(x.Exact)).ThenBy(x => x.Order).Take(remainder)) counts[quota.Order]++;
        var ids = new List<int>();
        for (int i = 0; i < combined.Count; i++) ids.AddRange(Enumerable.Repeat(combined[i].ItemId, counts[i]));
        var random = new Random(seed);
        for (int i = ids.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }
        return eligible.Select((target, i) => new DropAssignment(target.Path, ids[i], target.Fields.Select(edit =>
            new IdFieldEdit(edit.Path, edit.Instance, edit.FieldIndex, ids[i], edit.ArrayIndex)).ToList(), target.Count)).ToList();
    }
}
