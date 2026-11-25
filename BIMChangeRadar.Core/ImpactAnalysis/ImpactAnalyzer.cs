using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Rules;
using BIMChangeRadar.Core.Services;

namespace BIMChangeRadar.Core.ImpactAnalysis;

/// <summary>
/// Aplica um conjunto de regras independentes e explicáveis. O impacto do elemento
/// é o maior impacto entre suas regras (princípio conservador). Para adicionar uma
/// regra nova basta implementar a interface e registrá-la no construtor.
/// </summary>
public class ImpactAnalyzer : IImpactAnalyzer
{
    private readonly IReadOnlyList<IPropertyImpactRule> _propertyRules;
    private readonly IReadOnlyList<IElementImpactRule> _elementRules;

    public ImpactAnalyzer() : this(DefaultPropertyRules(), DefaultElementRules()) { }

    public ImpactAnalyzer(IEnumerable<IPropertyImpactRule> propertyRules, IEnumerable<IElementImpactRule> elementRules)
    {
        _propertyRules = propertyRules.ToList();
        _elementRules = elementRules.ToList();
    }

    public static IEnumerable<IPropertyImpactRule> DefaultPropertyRules() => new IPropertyImpactRule[]
    {
        new TextChangeRule(), new MaterialChangeRule(), new DimensionChangeRule(),
        new LevelChangeRule(), new ClassificationChangeRule(), new ParameterChangeRule()
    };

    public static IEnumerable<IElementImpactRule> DefaultElementRules() => new IElementImpactRule[]
    {
        new ElementAddedRule(), new ElementRemovedRule()
    };

    public void Analyze(ElementChange change)
    {
        switch (change.ChangeType)
        {
            case ChangeType.Unchanged:
                change.Impact = ImpactLevel.None;
                return;

            case ChangeType.Added or ChangeType.Removed:
                var assessments = _elementRules.Select(r => r.Evaluate(change)).OfType<ImpactAssessment>().ToList();
                Apply(change, assessments);
                change.Summary = change.ChangeType == ChangeType.Added
                    ? $"Elemento adicionado ({Pt.Category(change.Category)})"
                    : $"Elemento removido ({Pt.Category(change.Category)})";
                break;

            case ChangeType.Modified:
                var enriched = new List<PropertyChange>();
                var all = new List<ImpactAssessment>();
                foreach (var pc in change.PropertyChanges)
                {
                    var best = _propertyRules.Select(r => r.Evaluate(change, pc)).OfType<ImpactAssessment>()
                                             .OrderByDescending(a => a.Level).FirstOrDefault();
                    enriched.Add(best is null ? pc : pc with { Impact = best.Level, ImpactReason = best.Reason });
                    if (best is not null) all.Add(best);
                }
                change.PropertyChanges = enriched;
                change.Quantity = QuantityEstimator.Estimate(change.Before, change.After);
                Apply(change, all);
                change.Summary = string.Join("; ", enriched.Take(3).Select(p => $"{Pt.Property(p.PropertyName)}: {p.OldValue ?? "—"} → {p.NewValue ?? "—"}"))
                                 + (enriched.Count > 3 ? $" (+{enriched.Count - 3})" : "");
                break;
        }
    }

    private static void Apply(ElementChange change, List<ImpactAssessment> assessments)
    {
        change.Impact = assessments.Count == 0 ? ImpactLevel.Low : assessments.Max(a => a.Level);
        change.ImpactReasons = assessments.Select(a => a.Reason).Distinct().ToList();
    }
}
