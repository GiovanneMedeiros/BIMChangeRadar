using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.ImpactAnalysis;

namespace BIMChangeRadar.Core.Comparison;

/// <summary>
/// Motor de comparação: casa elementos por UniqueId (chave estável) e classifica
/// cada um em Added / Removed / Modified / Unchanged. Complexidade O(n+m) via dicionário.
/// </summary>
public class SnapshotComparer : ISnapshotComparer
{
    private readonly IImpactAnalyzer _analyzer;

    public SnapshotComparer() : this(new ImpactAnalyzer()) { }
    public SnapshotComparer(IImpactAnalyzer analyzer) => _analyzer = analyzer;

    public ComparisonResult Compare(ModelSnapshot previous, ModelSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var prev = ToLookup(previous);
        var curr = ToLookup(current);
        var changes = new List<ElementChange>();

        foreach (var (key, after) in curr)
        {
            if (!prev.TryGetValue(key, out var before))
            {
                changes.Add(Build(ChangeType.Added, null, after));
                continue;
            }

            var diffs = PropertyDiffer.Diff(before, after);
            var change = Build(diffs.Count == 0 ? ChangeType.Unchanged : ChangeType.Modified, before, after);
            change.PropertyChanges = diffs;
            changes.Add(change);
        }

        foreach (var (key, before) in prev)
            if (!curr.ContainsKey(key))
                changes.Add(Build(ChangeType.Removed, before, null));

        foreach (var c in changes.Where(c => c.ChangeType != ChangeType.Unchanged))
            _analyzer.Analyze(c);

        return new ComparisonResult
        {
            Previous = previous,
            Current = current,
            Changes = changes
                .OrderBy(c => c.ChangeType == ChangeType.Unchanged)
                .ThenByDescending(c => c.Impact)
                .ThenBy(c => c.ElementId, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static Dictionary<string, BimElement> ToLookup(ModelSnapshot s)
    {
        var dict = new Dictionary<string, BimElement>(StringComparer.Ordinal);
        foreach (var e in s.Elements)
        {
            if (string.IsNullOrWhiteSpace(e.Key))
                throw new InvalidDataException($"Elemento '{e.Name}' em {s.Label} não possui Id nem UniqueId.");
            if (!dict.TryAdd(e.Key, e))
                throw new InvalidDataException($"Identificador duplicado '{e.Key}' em {s.Label}.");
        }
        return dict;
    }

    private static ElementChange Build(ChangeType type, BimElement? before, BimElement? after)
    {
        var reference = after ?? before!;
        return new ElementChange
        {
            ElementId = reference.Id,
            UniqueId = reference.Key,
            Name = reference.Name,
            Category = reference.Category,
            ChangeType = type,
            Before = before,
            After = after
        };
    }
}
