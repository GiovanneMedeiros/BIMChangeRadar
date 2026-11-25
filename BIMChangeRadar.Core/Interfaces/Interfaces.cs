using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Interfaces;

/// <summary>Fonte de snapshots. Hoje JSON; amanhã pode ser um exportador do Revit API.</summary>
public interface ISnapshotLoader
{
    Task<ModelSnapshot> LoadAsync(string path, CancellationToken ct = default);
}

public interface ISnapshotComparer
{
    ComparisonResult Compare(ModelSnapshot previous, ModelSnapshot current, DateTime? analyzedAt = null);
}

public interface IImpactAnalyzer
{
    void Analyze(ElementChange change);
}

public interface IReportExporter
{
    string Name { get; }
    string FileExtension { get; }
    Task ExportAsync(ComparisonResult result, string path, CancellationToken ct = default);
}

/// <summary>Regra de impacto para uma propriedade alterada de um elemento modificado.</summary>
public interface IPropertyImpactRule
{
    string Name { get; }
    /// <summary>Retorna null se a regra não se aplica a esta alteração.</summary>
    ImpactAssessment? Evaluate(ElementChange element, PropertyChange change);
}

/// <summary>Regra de impacto para elementos inteiros adicionados/removidos.</summary>
public interface IElementImpactRule
{
    string Name { get; }
    ImpactAssessment? Evaluate(ElementChange element);
}

public record ImpactAssessment(ImpactLevel Level, string Reason);
