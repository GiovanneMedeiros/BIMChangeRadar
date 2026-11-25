using System.Globalization;
using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Services;

/// <summary>
/// Estima a variação de área/volume somente quando os dados existem.
/// Prioridade: Area informada &gt; Length×Height &gt; Width×Height &gt; Length×Width.
/// Nunca inventa valores: sem dados suficientes devolve null.
/// </summary>
public static class QuantityEstimator
{
    private static readonly CultureInfo Pt = new("pt-BR");

    public static double? EstimateArea(BimElement e)
    {
        if (e.Area is > 0) return e.Area;
        if (e.Length is > 0 && e.Height is > 0) return e.Length * e.Height;
        if (e.Width is > 0 && e.Height is > 0) return e.Width * e.Height;
        if (e.Length is > 0 && e.Width is > 0) return e.Length * e.Width;
        return null;
    }

    public static QuantityEstimate? Estimate(BimElement? before, BimElement? after)
    {
        if (before is null || after is null) return null;

        var aBefore = EstimateArea(before);
        var aAfter = EstimateArea(after);
        double? vBefore = before.Volume is > 0 ? before.Volume : null;
        double? vAfter = after.Volume is > 0 ? after.Volume : null;

        var parts = new List<string>();
        if (aBefore.HasValue && aAfter.HasValue && Math.Abs(aAfter.Value - aBefore.Value) > 1e-6)
        {
            var d = aAfter.Value - aBefore.Value;
            parts.Add($"A alteração {(d > 0 ? "aumentou" : "reduziu")} aproximadamente " +
                      $"{Math.Abs(d).ToString("0.00", Pt)} m² da área estimada deste elemento " +
                      $"({aBefore.Value.ToString("0.00", Pt)} → {aAfter.Value.ToString("0.00", Pt)} m²). " +
                      "Os quantitativos associados podem precisar de revisão.");
        }
        if (vBefore.HasValue && vAfter.HasValue && Math.Abs(vAfter.Value - vBefore.Value) > 1e-6)
        {
            var d = vAfter.Value - vBefore.Value;
            parts.Add($"Volume {(d > 0 ? "+" : "-")}{Math.Abs(d).ToString("0.00", Pt)} m³ " +
                      $"({vBefore.Value.ToString("0.00", Pt)} → {vAfter.Value.ToString("0.00", Pt)} m³).");
        }
        if (parts.Count == 0) return null;

        parts.Add("Valores são estimativas calculadas a partir das dimensões do snapshot.");
        return new QuantityEstimate(aBefore, aAfter, vBefore, vAfter, string.Join(" ", parts));
    }
}
