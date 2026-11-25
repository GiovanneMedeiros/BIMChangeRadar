namespace BIMChangeRadar.Core.Models;

/// <summary>Resultado completo da análise de um elemento.</summary>
public class ElementChange
{
    public required string ElementId { get; init; }
    public required string UniqueId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required ChangeType ChangeType { get; init; }

    /// <summary>Elemento na versão anterior (null se Added).</summary>
    public BimElement? Before { get; init; }
    /// <summary>Elemento na versão atual (null se Removed).</summary>
    public BimElement? After { get; init; }

    public IReadOnlyList<PropertyChange> PropertyChanges { get; set; } = Array.Empty<PropertyChange>();

    public ImpactLevel Impact { get; set; } = ImpactLevel.None;
    /// <summary>Motivos explicáveis, um por regra disparada.</summary>
    public IReadOnlyList<string> ImpactReasons { get; set; } = Array.Empty<string>();
    public QuantityEstimate? Quantity { get; set; }
    public string Summary { get; set; } = "";
}
