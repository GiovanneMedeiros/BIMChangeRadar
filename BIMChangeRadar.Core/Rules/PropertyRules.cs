using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Rules;

/// <summary>Nome/Mark: alteração textual, sem efeito geométrico.</summary>
public class TextChangeRule : IPropertyImpactRule
{
    public string Name => "Alteração textual";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c) =>
        c.Kind == PropertyKind.Text
            ? new(ImpactLevel.Low, "Alteração textual sem mudança geométrica identificada.")
            : null;
}

public class MaterialChangeRule : IPropertyImpactRule
{
    public string Name => "Mudança de material";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c)
    {
        if (c.Kind != PropertyKind.Material) return null;
        return CategoryGroups.IsStructural(element.Category)
            ? new(ImpactLevel.High, "Material de elemento estrutural alterado: pode afetar desempenho estrutural, especificações e custos.")
            : new(ImpactLevel.Medium, "Pode impactar especificações, quantitativos e custos.");
    }
}

public class DimensionChangeRule : IPropertyImpactRule
{
    public string Name => "Mudança de dimensão";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c)
    {
        if (c.Kind != PropertyKind.Dimension) return null;
        return CategoryGroups.IsStructural(element.Category)
            ? new(ImpactLevel.Critical, "Dimensão de elemento estrutural alterada: exige revisão de cálculo estrutural, coordenação e quantitativos.")
            : new(ImpactLevel.High, "Pode alterar quantitativos e afetar elementos ou documentação relacionada.");
    }
}

public class LevelChangeRule : IPropertyImpactRule
{
    public string Name => "Mudança de nível";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c) =>
        c.Kind == PropertyKind.Location
            ? new(ImpactLevel.High, "Elemento mudou de pavimento: afeta coordenação entre disciplinas, plantas e quantitativos por nível.")
            : null;
}

public class ClassificationChangeRule : IPropertyImpactRule
{
    public string Name => "Mudança de tipo/categoria";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c)
    {
        if (c.Kind != PropertyKind.Classification) return null;
        return c.PropertyName == "Category"
            ? new(ImpactLevel.High, "Mudança de categoria altera tabelas, filtros e regras de coordenação.")
            : new(ImpactLevel.Medium, "Mudança de tipo pode alterar especificação, desempenho e quantitativos.");
    }
}

/// <summary>Parâmetros críticos (resistência ao fogo, carga) são HIGH; os demais LOW.</summary>
public class ParameterChangeRule : IPropertyImpactRule
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
        { "FireRating", "LoadBearing", "StructuralUsage", "AcousticRating", "FlowRate" };

    public string Name => "Mudança de parâmetro";
    public ImpactAssessment? Evaluate(ElementChange element, PropertyChange c)
    {
        if (c.Kind != PropertyKind.Parameter) return null;
        var key = c.PropertyName.Replace("Parameters.", "");
        return Critical.Contains(key)
            ? new(ImpactLevel.High, $"O parâmetro '{key}' está ligado a requisitos de desempenho/normas e deve ser revisado.")
            : new(ImpactLevel.Low, $"Parâmetro informativo '{key}' alterado, sem efeito geométrico.");
    }
}
