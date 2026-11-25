using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Services;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class QuantityTests
{
    [Fact]
    public void Increase_is_exact_and_message_says_aumentou()
    {
        var q = QuantityEstimator.Estimate(Wall(length: 3.2, height: 2.8), Wall(length: 4.8, height: 2.8))!;
        Assert.Equal(8.96, q.AreaBefore!.Value, 6);
        Assert.Equal(13.44, q.AreaAfter!.Value, 6);
        Assert.Equal(4.48, q.AreaDelta!.Value, 6);
        Assert.Contains("aumentou aproximadamente 4,48 m²", q.Message);
    }

    [Fact]
    public void Decrease_is_negative_and_message_says_reduziu()
    {
        var q = QuantityEstimator.Estimate(Wall(length: 3.2, height: 2.8), Wall(length: 3.2, height: 2.0))!;
        Assert.Equal(-2.56, q.AreaDelta!.Value, 6);   // 6.4 - 8.96
        Assert.Contains("reduziu aproximadamente 2,56 m²", q.Message);
    }

    [Fact]
    public void Explicit_area_takes_precedence_over_dimensions()
    {
        var a = Wall(); a.Area = 10; var b = Wall(length: 99); b.Area = 12;
        var q = QuantityEstimator.Estimate(a, b)!;
        Assert.Equal(2.0, q.AreaDelta!.Value, 6);
    }

    [Fact]
    public void Volume_difference_is_reported_when_both_volumes_exist()
    {
        var a = Wall(); a.Volume = 1.5; var b = Wall(); b.Volume = 2.25;
        var q = QuantityEstimator.Estimate(a, b)!;
        Assert.Equal(0.75, q.VolumeDelta!.Value, 6);
        Assert.Contains("+0,75 m³", q.Message);
    }

    [Fact]
    public void Message_labels_result_as_estimate_not_official_revit_quantity()
    {
        var q = QuantityEstimator.Estimate(Wall(length: 3), Wall(length: 4))!;
        Assert.Contains("Estimativa", q.Message);
        Assert.Contains("não são quantitativos oficiais extraídos do Revit", q.Message);
    }

    public static IEnumerable<object?[]> NoEstimateCases()
    {
        BimElement W(double? l, double? h) { var e = Wall(); e.Length = l; e.Height = h; return e; }
        yield return new object?[] { "valores iguais", W(3, 2), W(3, 2) };
        yield return new object?[] { "sem dimensões", W(null, null), W(null, null) };
        yield return new object?[] { "só comprimento (altura ausente)", W(3, null), W(4, null) };
        yield return new object?[] { "zero no atual", W(3, 2), W(0, 2) };
        yield return new object?[] { "zero em ambos", W(0, 0), W(0, 0) };
        yield return new object?[] { "negativo no atual", W(3, 2), W(-4, 2) };
        yield return new object?[] { "dimensão apareceu só no atual", W(null, null), W(3, 2) };
    }

    [Theory]
    [MemberData(nameof(NoEstimateCases))]
    public void No_estimate_without_valid_data(string _, BimElement before, BimElement after)
    {
        Assert.Null(QuantityEstimator.Estimate(before, after));
    }

    [Fact]
    public void Added_and_removed_never_have_an_estimate()
    {
        Assert.Null(QuantityEstimator.Estimate(null, Wall()));
        Assert.Null(QuantityEstimator.Estimate(Wall(), null));
    }

    [Fact]
    public void Area_falls_back_in_documented_order()
    {
        Assert.Equal(6.0, QuantityEstimator.EstimateArea(new BimElement { Width = 2, Height = 3 })!.Value);
        Assert.Equal(6.0, QuantityEstimator.EstimateArea(new BimElement { Length = 2, Width = 3 })!.Value);
        Assert.Equal(12.0, QuantityEstimator.EstimateArea(new BimElement { Length = 2, Height = 3, Width = 99, Area = 12 })!.Value);
        Assert.Null(QuantityEstimator.EstimateArea(new BimElement()));
    }
}
