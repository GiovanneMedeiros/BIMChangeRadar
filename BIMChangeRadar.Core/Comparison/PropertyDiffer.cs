using System.Globalization;
using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Comparison;

/// <summary>
/// Compara duas versões do mesmo elemento propriedade a propriedade.
/// Evita falsos positivos: números com tolerância, texto ignorando espaços nas pontas,
/// null e "" tratados como iguais.
/// </summary>
public static class PropertyDiffer
{
    public const double NumericTolerance = 1e-6;

    private sealed record Prop(string Name, PropertyKind Kind, Func<BimElement, object?> Get);

    private static readonly Prop[] Props =
    {
        new("Name", PropertyKind.Text, e => e.Name),
        new("Mark", PropertyKind.Text, e => e.Mark),
        new("Category", PropertyKind.Classification, e => e.Category),
        new("Type", PropertyKind.Classification, e => e.Type),
        new("Level", PropertyKind.Location, e => e.Level),
        new("Material", PropertyKind.Material, e => e.Material),
        new("Width", PropertyKind.Dimension, e => e.Width),
        new("Height", PropertyKind.Dimension, e => e.Height),
        new("Length", PropertyKind.Dimension, e => e.Length),
        new("Area", PropertyKind.Dimension, e => e.Area),
        new("Volume", PropertyKind.Dimension, e => e.Volume),
    };

    public static List<PropertyChange> Diff(BimElement before, BimElement after)
    {
        var result = new List<PropertyChange>();

        foreach (var p in Props)
        {
            var a = p.Get(before);
            var b = p.Get(after);
            if (!AreEqual(a, b))
                result.Add(new PropertyChange(p.Name, Format(a), Format(b), p.Kind));
        }

        // Parâmetros personalizados: união das chaves (chave ausente == valor nulo).
        var keys = before.Parameters.Keys.Union(after.Parameters.Keys, StringComparer.OrdinalIgnoreCase)
                                          .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            before.Parameters.TryGetValue(key, out var a);
            after.Parameters.TryGetValue(key, out var b);
            if (!AreEqual(a, b))
                result.Add(new PropertyChange($"Parameters.{key}", a, b, PropertyKind.Parameter));
        }
        return result;
    }

    private static bool AreEqual(object? a, object? b)
    {
        if (a is double da && b is double db) return Math.Abs(da - db) < NumericTolerance;
        if (a is double && b is null || a is null && b is double) return false;
        return string.Equals(Normalize(a as string ?? a?.ToString()), Normalize(b as string ?? b?.ToString()),
                             StringComparison.Ordinal);
    }

    private static string Normalize(string? s) => (s ?? "").Trim();

    private static string? Format(object? v) => v switch
    {
        null => null,
        double d => d.ToString("0.##", CultureInfo.InvariantCulture),
        _ => v.ToString()
    };
}
