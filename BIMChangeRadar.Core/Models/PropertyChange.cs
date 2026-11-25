namespace BIMChangeRadar.Core.Models;

/// <summary>Uma propriedade alterada de um elemento modificado.</summary>
public record PropertyChange(
    string PropertyName,
    string? OldValue,
    string? NewValue,
    PropertyKind Kind)
{
    /// <summary>Preenchidos pelo ImpactAnalyzer.</summary>
    public ImpactLevel Impact { get; init; } = ImpactLevel.None;
    public string ImpactReason { get; init; } = "";
}
