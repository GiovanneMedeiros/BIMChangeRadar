using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Services;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class ComparisonTests
{
    private readonly SnapshotComparer _cmp = new();

    [Fact]
    public void Detects_added_elements()
    {
        var r = _cmp.Compare(Snap("1", Wall("W-1")), Snap("2", Wall("W-1"), Wall("W-2")));
        var added = Assert.Single(r.Changes, c => c.ChangeType == ChangeType.Added);
        Assert.Equal("W-2", added.ElementId);
        Assert.Equal(1, r.Added);
    }

    [Fact]
    public void Detects_removed_elements()
    {
        var r = _cmp.Compare(Snap("1", Wall("W-1"), Wall("W-2")), Snap("2", Wall("W-1")));
        var removed = Assert.Single(r.Changes, c => c.ChangeType == ChangeType.Removed);
        Assert.Equal("W-2", removed.ElementId);
    }

    [Fact]
    public void Detects_modified_elements_and_lists_exact_properties()
    {
        var r = _cmp.Compare(Snap("1", Wall()), Snap("2", Wall(length: 4.8, material: "Drywall", level: "Floor 2")));
        var m = Assert.Single(r.Changes);
        Assert.Equal(ChangeType.Modified, m.ChangeType);
        Assert.Equal(new[] { "Level", "Material", "Length" }.OrderBy(x => x),
                     m.PropertyChanges.Select(p => p.PropertyName).OrderBy(x => x));
        var len = m.PropertyChanges.Single(p => p.PropertyName == "Length");
        Assert.Equal("3.2", len.OldValue);
        Assert.Equal("4.8", len.NewValue);
    }

    [Fact]
    public void Unchanged_elements_are_not_reported_as_changes()
    {
        var r = _cmp.Compare(Snap("1", Wall()), Snap("2", Wall()));
        Assert.Equal(1, r.Unchanged);
        Assert.Equal(0, r.TotalChanges);
        Assert.Equal(ImpactLevel.None, r.Changes[0].Impact);
    }

    [Fact]
    public void Float_noise_and_whitespace_are_not_false_positives()
    {
        var after = Wall(length: 3.2 + 1e-9, name: "Wall A  ");
        var r = _cmp.Compare(Snap("1", Wall()), Snap("2", after));
        Assert.Equal(ChangeType.Unchanged, r.Changes[0].ChangeType);
    }

    [Fact]
    public void Compares_custom_parameters_including_added_and_removed_keys()
    {
        var a = Wall(); var b = Wall();
        b.Parameters["FireRating"] = "90min";
        b.Parameters.Remove("Comments");
        b.Parameters["Acoustic"] = "STC50";
        var changes = Assert.Single(_cmp.Compare(Snap("1", a), Snap("2", b)).Changes).PropertyChanges;
        Assert.Contains(changes, p => p.PropertyName == "Parameters.FireRating" && p.OldValue == "60min" && p.NewValue == "90min");
        Assert.Contains(changes, p => p.PropertyName == "Parameters.Comments" && p.NewValue is null);
        Assert.Contains(changes, p => p.PropertyName == "Parameters.Acoustic" && p.OldValue is null);
    }

    [Fact]
    public void Duplicate_ids_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => _cmp.Compare(Snap("1", Wall("W-1"), Wall("W-1")), Snap("2", Wall("W-1"))));
    }

    [Fact]
    public void Area_difference_matches_spec_example()
    {
        var q = QuantityEstimator.Estimate(Wall(length: 3.2), Wall(length: 4.8));
        Assert.NotNull(q);
        Assert.Equal(8.96, q!.AreaBefore!.Value, 2);
        Assert.Equal(13.44, q.AreaAfter!.Value, 2);
        Assert.Equal(4.48, q.AreaDelta!.Value, 2);
        Assert.Contains("aumentou aproximadamente 4,48 m²", q.Message);
    }

    [Fact]
    public void No_estimate_when_data_is_missing()
    {
        var a = Wall(); a.Length = null; a.Height = null;
        var b = Wall(); b.Length = null; b.Height = null;
        Assert.Null(QuantityEstimator.Estimate(a, b));
    }
}
