namespace BIMChangeRadar.Core.Models;

public class ComparisonResult
{
    public required ModelSnapshot Previous { get; init; }
    public required ModelSnapshot Current { get; init; }
    public DateTime AnalyzedAt { get; init; } = DateTime.Now;
    /// <summary>Todos os elementos (inclui Unchanged).</summary>
    public required IReadOnlyList<ElementChange> Changes { get; init; }

    public IEnumerable<ElementChange> ActualChanges => Changes.Where(c => c.ChangeType != ChangeType.Unchanged);

    public int TotalElements => Changes.Count;
    public int Added => Count(ChangeType.Added);
    public int Removed => Count(ChangeType.Removed);
    public int Modified => Count(ChangeType.Modified);
    public int Unchanged => Count(ChangeType.Unchanged);
    public int TotalChanges => Added + Removed + Modified;
    public int HighImpact => ActualChanges.Count(c => c.Impact >= ImpactLevel.High);
    public int CountByImpact(ImpactLevel level) => ActualChanges.Count(c => c.Impact == level);

    private int Count(ChangeType t) => Changes.Count(c => c.ChangeType == t);
}
