using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class ImpactTests
{
    private static ElementChange One(BimElement? a, BimElement? b)
    {
        var before = a is null ? Snap("1", Wall("OTHER")) : Snap("1", a, Wall("OTHER"));
        var after = b is null ? Snap("2", Wall("OTHER")) : Snap("2", b, Wall("OTHER"));
        return new SnapshotComparer().Compare(before, after).Changes.Single(c => c.ElementId != "OTHER");
    }

    [Fact] public void Name_change_is_low() =>
        Assert.Equal(ImpactLevel.Low, One(Wall(), Wall(name: "Renamed")).Impact);

    [Fact] public void Material_change_is_medium() =>
        Assert.Equal(ImpactLevel.Medium, One(Wall(), Wall(material: "Brick")).Impact);

    [Fact] public void Dimension_change_is_high() =>
        Assert.Equal(ImpactLevel.High, One(Wall(), Wall(length: 5)).Impact);

    [Fact] public void Structural_dimension_change_is_critical() =>
        Assert.Equal(ImpactLevel.Critical, One(Wall("C-1", category: "Column"), Wall("C-1", category: "Column", height: 4)).Impact);

    [Fact] public void Added_is_medium() =>
        Assert.Equal(ImpactLevel.Medium, One(null, Wall("W-9")).Impact);

    [Fact] public void Removed_wall_is_high_but_removed_column_is_critical()
    {
        Assert.Equal(ImpactLevel.High, One(Wall("W-9"), null).Impact);
        Assert.Equal(ImpactLevel.Critical, One(Wall("C-9", category: "Column"), null).Impact);
    }

    [Fact] public void Element_impact_is_the_highest_among_its_changes()
    {
        var c = One(Wall(), Wall(name: "X", length: 9));
        Assert.Equal(ImpactLevel.High, c.Impact);
        Assert.Equal(2, c.PropertyChanges.Count);
        Assert.All(c.PropertyChanges, p => Assert.False(string.IsNullOrEmpty(p.ImpactReason)));
    }

    [Fact] public void Critical_parameter_is_high_informative_is_low()
    {
        var b1 = Wall(); b1.Parameters["FireRating"] = "120min";
        var b2 = Wall(); b2.Parameters["Comments"] = "changed";
        Assert.Equal(ImpactLevel.High, One(Wall(), b1).Impact);
        Assert.Equal(ImpactLevel.Low, One(Wall(), b2).Impact);
    }
}
