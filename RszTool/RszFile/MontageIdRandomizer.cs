namespace RszTool;

public sealed class MontageIdSource(string key, EnumItem[] enumItems)
{
    public string Key { get; } = key;
    public EnumItem[] EnumItems { get; } = enumItems;
    public override string ToString() => $"{Key}（下拉枚举 {EnumItems.Length} 项）";
}

public static class MontageIdRandomizer
{
    private static bool IsMontageField(RszField field) => !field.array &&
        (field.type is RszFieldType.S8 or RszFieldType.U8 or RszFieldType.S16 or RszFieldType.U16 or
            RszFieldType.S32 or RszFieldType.U32 or RszFieldType.S64 or RszFieldType.U64) &&
        (field.name.EndsWith("MontageID", StringComparison.OrdinalIgnoreCase) || field.original_type.EndsWith(".MontageID", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<(RszInstance Instance, int Index, string PoolKey)> Fields(IEnumerable<IGameObjectData> objects, EnumDict enums)
    {
        foreach (var (_, instance) in Re4IdEditor.Walk(Re4IdEditor.GameObjectRoots(objects, true)))
            for (int i = 0; i < instance.Fields.Length; i++)
            {
                var field = instance.Fields[i];
                if (!IsMontageField(field)) continue;
                string key = field.original_type;
                if (enums[key] == null)
                {
                    // Raw _RandomMontageID fields use the same dropdown as the primary MontageID.
                    var primary = instance.Fields.Where(IsMontageField).Where(x => enums[x.original_type] != null)
                        .OrderByDescending(x => x.name == "_MontageID").FirstOrDefault();
                    key = primary?.original_type ?? $"{instance.RszClass.name}.{field.name}";
                }
                yield return (instance, i, key);
            }
    }

    public static List<MontageIdSource> Sources(IEnumerable<IGameObjectData> objects, EnumDict enums) =>
        Fields(objects, enums).Select(x => x.PoolKey).Distinct().Select(key => new MontageIdSource(key,
            enums[key]?.Items.Where(x => x.Value <= uint.MaxValue).ToArray() ?? [])).ToList();

    public static void Randomize(IGameObjectData gameObject, EnumDict enums, IReadOnlyDictionary<string, List<uint>> pools, Random random)
    {
        var changes = new List<(RszInstance Instance, int Index, object Value)>();
        foreach (var (instance, index, poolKey) in Fields([gameObject], enums))
        {
            if (!pools.TryGetValue(poolKey, out var choices) || choices.Count == 0) continue;
            var type = instance.Fields[index].type;
            var compatible = choices.Where(x => type switch
            {
                RszFieldType.S8 => x <= sbyte.MaxValue,
                RszFieldType.U8 => x <= byte.MaxValue,
                RszFieldType.S16 => x <= short.MaxValue,
                RszFieldType.U16 => x <= ushort.MaxValue,
                _ => true,
            }).ToList();
            if (compatible.Count == 0) throw new ArgumentException($"{poolKey} 没有可写入 {type} 的候选 ID。");
            uint id = compatible[random.Next(compatible.Count)];
            object value = type switch
            {
                RszFieldType.S8 => (object)(sbyte)id,
                RszFieldType.U8 => (byte)id,
                RszFieldType.S16 => (short)id,
                RszFieldType.U16 => (ushort)id,
                RszFieldType.S32 => unchecked((int)id),
                RszFieldType.U32 => id,
                RszFieldType.S64 => (long)id,
                RszFieldType.U64 => (ulong)id,
                _ => throw new InvalidOperationException("MontageID must be an integer field."),
            };
            changes.Add((instance, index, value));
        }
        foreach (var change in changes) change.Instance.SetFieldValue(change.Index, change.Value);
    }
}
