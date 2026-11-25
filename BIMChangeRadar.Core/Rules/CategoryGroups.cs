namespace BIMChangeRadar.Core.Rules;

/// <summary>Agrupamentos de categoria usados por várias regras.</summary>
public static class CategoryGroups
{
    public static readonly HashSet<string> Structural = new(StringComparer.OrdinalIgnoreCase)
        { "Column", "Beam", "Floor", "Foundation", "Structural Framing", "Structural Column" };

    public static bool IsStructural(string category) => Structural.Contains(category);
}
