namespace Swarnakshi.Application.Masters;

/// <summary>One resolved specification value, flattened for identity/summary building.</summary>
public readonly record struct SpecPart(string Key, string Label, int SortOrder, bool PartOfIdentity, string Value);

/// <summary>
/// The single definition of what makes a material unique and how its specification reads.
/// Shared by <see cref="MaterialService"/> and the seeder so both produce byte-identical signatures.
/// </summary>
public static class MaterialIdentity
{
    /// <summary>
    /// Normalised duplicate key: name + brand + identity-bearing specs.
    /// Case-, whitespace- and order-insensitive, so "Polycab" and " polycab " collide as intended.
    /// </summary>
    public static string Signature(string name, string? brand, IEnumerable<SpecPart> specs)
    {
        var parts = specs.Where(s => s.PartOfIdentity)
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => $"{Norm(s.Key)}={Norm(s.Value)}");
        return $"{Norm(name)}|{Norm(brand ?? "")}" + string.Concat(parts.Select(p => "|" + p));
    }

    /// <summary>
    /// Human-readable specification digest — "600 × 1200 mm · Matt", "25 mm · Cold Water".
    /// Also what free-text search matches on, so phrase queries like "25 mm" work.
    /// </summary>
    public static string? Summary(IEnumerable<SpecPart> specs)
    {
        var list = specs.OrderBy(s => s.SortOrder).ToList();
        if (list.Count == 0) return null;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parts = new List<string>();

        string? Value(string key) => list.FirstOrDefault(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

        // Dimensions share one unit: length × width [× height] unit
        var dimUnit = Value("dimension_unit");
        var dims = new[] { "length", "width", "height" }
            .Select(k => (Key: k, Val: Value(k)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Val))
            .ToList();
        if (dims.Count >= 2)
        {
            parts.Add(string.Join(" × ", dims.Select(d => d.Val)) + (dimUnit is null ? "" : $" {dimUnit}"));
            foreach (var d in dims) used.Add(d.Key);
            used.Add("dimension_unit");
        }

        foreach (var s in list)
        {
            if (used.Contains(s.Key)) continue;
            if (s.Key.EndsWith("_unit", StringComparison.OrdinalIgnoreCase)) continue;

            // Pair a magnitude with its own unit field: diameter + diameter_unit -> "25 mm"
            var unit = Value(s.Key + "_unit");
            parts.Add(string.IsNullOrWhiteSpace(unit) ? s.Value : $"{s.Value} {unit}");
            used.Add(s.Key);
            used.Add(s.Key + "_unit");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string Norm(string s) => string.Join(' ',
        s.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
