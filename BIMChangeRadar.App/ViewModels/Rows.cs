using Avalonia.Media;
using BIMChangeRadar.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMChangeRadar.App.ViewModels;

public static class Palette
{
    public static IBrush Added = Brush("#12713A"), AddedBg = Brush("#DCF5E6");
    public static IBrush Removed = Brush("#A12020"), RemovedBg = Brush("#FDE3E3");
    public static IBrush Modified = Brush("#8A5A00"), ModifiedBg = Brush("#FFF0D2");
    public static IBrush Neutral = Brush("#41507A"), NeutralBg = Brush("#E8EDF6");

    public static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    public static (IBrush Fg, IBrush Bg) For(ChangeType t) => t switch
    {
        ChangeType.Added => (Added, AddedBg),
        ChangeType.Removed => (Removed, RemovedBg),
        ChangeType.Modified => (Modified, ModifiedBg),
        _ => (Neutral, NeutralBg)
    };

    public static (IBrush Fg, IBrush Bg) For(ImpactLevel l) => l switch
    {
        ImpactLevel.Low => (Brush("#2C5AA8"), Brush("#E3ECFB")),
        ImpactLevel.Medium => (Brush("#8A5A00"), Brush("#FFF0D2")),
        ImpactLevel.High => (Brush("#B24700"), Brush("#FFE0CC")),
        ImpactLevel.Critical => (Brush("#FFFFFF"), Brush("#C62828")),
        _ => (Neutral, NeutralBg)
    };

    public static IBrush Solid(ImpactLevel l) => l switch
    {
        ImpactLevel.Low => Brush("#5B8DEF"), ImpactLevel.Medium => Brush("#F2A81D"),
        ImpactLevel.High => Brush("#F26B1D"), ImpactLevel.Critical => Brush("#C62828"), _ => Brush("#9AA7C2")
    };
}

/// <summary>Linha da tabela: adapta ElementChange para exibição (cores, textos, ordenação).</summary>
public class ChangeRow
{
    public ChangeRow(ElementChange c)
    {
        Change = c;
        (ChangeFg, ChangeBg) = Palette.For(c.ChangeType);
        (ImpactFg, ImpactBg) = Palette.For(c.Impact);
    }

    public ElementChange Change { get; }
    public string ElementId => Change.ElementId;
    public string Name => Change.Name;
    public string Category => Change.Category;
    public string ChangeTypeText => Change.ChangeType.ToString().ToUpperInvariant();
    public string ImpactText => Change.Impact == ImpactLevel.None ? "—" : Change.Impact.ToString().ToUpperInvariant();
    public int ImpactOrder => (int)Change.Impact;
    public string ChangedProperties => Change.PropertyChanges.Count == 0 ? "—" : string.Join(", ", Change.PropertyChanges.Select(p => p.PropertyName.Replace("Parameters.", "")));
    public string Summary => Change.Summary;
    public IBrush ChangeFg { get; }
    public IBrush ChangeBg { get; }
    public IBrush ImpactFg { get; }
    public IBrush ImpactBg { get; }
}

public class PropertyRow
{
    public PropertyRow(PropertyChange p)
    {
        Change = p;
        (Fg, Bg) = Palette.For(p.Impact);
    }
    public PropertyChange Change { get; }
    public string Name => Change.PropertyName;
    public string Old => Change.OldValue ?? "—";
    public string New => Change.NewValue ?? "—";
    public string Kind => Change.Kind.ToString();
    public string ImpactText => Change.Impact.ToString().ToUpperInvariant();
    public IBrush Fg { get; }
    public IBrush Bg { get; }
}

public record StatCard(string Label, int Value, IBrush Accent, string Hint);
public record BarItem(string Label, int Value, double Fraction, IBrush Color);

public partial class HistoryEntry : ObservableObject
{
    public required Core.Models.ComparisonResult Result { get; init; }
    public string Title => $"{Result.Current.ProjectName}: v{Result.Previous.Version} → v{Result.Current.Version}";
    public string When => Result.AnalyzedAt.ToString("dd/MM/yyyy HH:mm:ss");
    public string Counts => $"{Result.Added} adicionados · {Result.Removed} removidos · {Result.Modified} modificados · {Result.HighImpact} alto impacto";
}
