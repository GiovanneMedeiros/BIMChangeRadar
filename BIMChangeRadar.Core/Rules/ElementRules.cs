using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Rules;

public class ElementAddedRule : IElementImpactRule
{
    public string Name => "Elemento adicionado";
    public ImpactAssessment? Evaluate(ElementChange e) =>
        e.ChangeType == ChangeType.Added
            ? new(ImpactLevel.Medium, "Novo elemento pode precisar de revisão, documentação e atualização dos quantitativos.")
            : null;
}

public class ElementRemovedRule : IElementImpactRule
{
    public string Name => "Elemento removido";
    public ImpactAssessment? Evaluate(ElementChange e)
    {
        if (e.ChangeType != ChangeType.Removed) return null;
        return CategoryGroups.IsStructural(e.Category)
            ? new(ImpactLevel.Critical, "Remoção de elemento estrutural: exige revisão estrutural, de documentação, quantitativos e coordenação.")
            : new(ImpactLevel.High, "A remoção de elementos pode exigir revisão de documentação, quantitativos e coordenação.");
    }
}
