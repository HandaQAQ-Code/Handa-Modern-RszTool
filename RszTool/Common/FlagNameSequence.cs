using System.Globalization;
using System.Text.RegularExpressions;

namespace RszTool.Common;

public sealed class FlagNameSequence
{
    private static readonly Regex pattern = new(@"^(?<prefix>.*?)(?<number>[0-9]+)(?<suffix>[^0-9]*)$", RegexOptions.Compiled);
    public string Prefix { get; }
    public string Suffix { get; }
    public int Digits { get; }
    public long Number { get; }
    public FlagNameSequence(string reference)
    {
        var match = pattern.Match(reference);
        if (!match.Success) throw new ArgumentException("参考名称必须包含编号，例如 Location47_000。");
        Prefix = match.Groups["prefix"].Value;
        Suffix = match.Groups["suffix"].Value;
        Digits = match.Groups["number"].Length;
        Number = long.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
    }
    public long Next(IEnumerable<string> existing)
    {
        long maximum = Number;
        foreach (string name in existing)
        {
            var match = pattern.Match(name);
            if (match.Success && match.Groups["prefix"].Value == Prefix && match.Groups["suffix"].Value == Suffix &&
                long.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
                maximum = Math.Max(maximum, number);
        }
        return checked(maximum + 1);
    }
    public List<string> Generate(long start, int count, long step)
    {
        if (start < 0 || count < 1 || count > short.MaxValue || step < 1)
            throw new ArgumentException("起始编号须大于等于 0，数量为 1 至 32767，步长须大于等于 1。");
        _ = checked(start + checked((count - 1) * step));
        return Enumerable.Range(0, count).Select(i => Prefix + checked(start + checked(i * step)).ToString("D" + Digits, CultureInfo.InvariantCulture) + Suffix).ToList();
    }
}
