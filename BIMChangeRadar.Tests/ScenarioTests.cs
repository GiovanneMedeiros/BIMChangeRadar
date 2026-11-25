using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

/// <summary>Cenários A–G da auditoria, com datasets controlados e resultados esperados calculados à mão.</summary>
public class ScenarioTests
{
    private readonly SnapshotComparer _cmp = new();

    [Fact] // A
    public void A_added_element_is_ADDED_with_medium_impact()
    {
        var r = _cmp.Compare(Snap("1", Wall("W-1")), Snap("2", Wall("W-1"), Wall("W-NEW", name: "Nova")));
        var c = r.Changes.Single(x => x.ElementId == "W-NEW");
        Assert.Equal(ChangeType.Added, c.ChangeType);
        Assert.Null(c.Before); Assert.NotNull(c.After);
        Assert.Equal(ImpactLevel.Medium, c.Impact);
        Assert.Empty(c.PropertyChanges);
        Assert.Null(c.Quantity);
    }

    [Fact] // B
    public void B_removed_element_is_REMOVED_with_high_impact_for_wall()
    {
        var r = _cmp.Compare(Snap("1", Wall("W-1"), Wall("W-OLD")), Snap("2", Wall("W-1")));
        var c = r.Changes.Single(x => x.ElementId == "W-OLD");
        Assert.Equal(ChangeType.Removed, c.ChangeType);
        Assert.NotNull(c.Before); Assert.Null(c.After);
        Assert.Equal(ImpactLevel.High, c.Impact);
    }

    [Fact] // C
    public void C_modified_element_lists_old_and_new_values()
    {
        var r = _cmp.Compare(Snap("1", Wall(material: "Concreto")), Snap("2", Wall(material: "Drywall")));
        var c = Assert.Single(r.Changes);
        Assert.Equal(ChangeType.Modified, c.ChangeType);
        var p = Assert.Single(c.PropertyChanges);
        Assert.Equal(("Material", "Concreto", "Drywall"), (p.PropertyName, p.OldValue, p.NewValue));
    }

    [Fact] // D
    public void D_unchanged_element_is_not_counted_as_change()
    {
        var r = _cmp.Compare(Snap("1", Wall("W-1"), Wall("W-2")), Snap("2", Wall("W-1"), Wall("W-2", length: 9)));
        Assert.Equal(ChangeType.Unchanged, r.Changes.Single(x => x.ElementId == "W-1").ChangeType);
        Assert.Equal(1, r.TotalChanges);
        Assert.DoesNotContain(r.ActualChanges, x => x.ElementId == "W-1");
    }

    [Fact] // E
    public void E_all_changed_properties_are_reported_none_lost()
    {
        var before = Wall(); before.Width = 0.2; before.Area = 8.96; before.Volume = 1.79; before.Mark = "M1"; before.Type = "A";
        var after = Wall(name: "Novo", length: 4.8, material: "Drywall", level: "Pavimento 2");
        after.Width = 0.3; after.Area = 13.44; after.Volume = 4.03; after.Mark = "M2"; after.Type = "B"; after.Height = 3.0;
        after.Parameters["FireRating"] = "90min"; after.Parameters["Novo"] = "x"; after.Parameters.Remove("Comments");
        var c = Assert.Single(_cmp.Compare(Snap("1", before), Snap("2", after)).Changes);
        var names = c.PropertyChanges.Select(p => p.PropertyName).OrderBy(x => x).ToArray();
        var expected = new[] { "Name", "Mark", "Type", "Level", "Material", "Width", "Height", "Length", "Area", "Volume",
                               "Parameters.FireRating", "Parameters.Novo", "Parameters.Comments" }.OrderBy(x => x).ToArray();
        Assert.Equal(expected, names);
    }

    [Fact] // F
    public void F_identical_projects_have_zero_changes()
    {
        var els = Enumerable.Range(1, 25).Select(i => Wall($"W-{i}", length: i)).ToArray();
        var copy = els.Select(Synth.Clone).ToArray();
        var r = _cmp.Compare(Snap("1", els), Snap("2", copy));
        Assert.Equal(25, r.TotalElements);
        Assert.Equal(25, r.Unchanged);
        Assert.Equal((0, 0, 0, 0, 0), (r.Added, r.Removed, r.Modified, r.TotalChanges, r.HighImpact));
        Assert.All(r.Changes, c => Assert.Equal(ChangeType.Unchanged, c.ChangeType));
        Assert.Empty(r.ActualChanges);
    }

    [Fact] // G (comparador): coleções vazias não quebram
    public void G_empty_collections_compare_without_crashing()
    {
        var r = _cmp.Compare(Snap("1"), Snap("2"));
        Assert.Equal(0, r.TotalElements);
        Assert.Equal(0, r.TotalChanges);
        var onlyNew = _cmp.Compare(Snap("1"), Snap("2", Wall("W-1")));
        Assert.Equal(1, onlyNew.Added);
        var onlyOld = _cmp.Compare(Snap("1", Wall("W-1")), Snap("2"));
        Assert.Equal(1, onlyOld.Removed);
    }

    [Fact]
    public void Matching_uses_UniqueId_so_a_changed_display_Id_alone_is_not_a_change()
    {
        var a = Wall("W-1"); var b = Wall("W-1"); b.Id = "W-RENUMBERED";
        var c = Assert.Single(_cmp.Compare(Snap("1", a), Snap("2", b)).Changes);
        Assert.Equal(ChangeType.Unchanged, c.ChangeType); // mesmo UniqueId => mesmo elemento; Id não é comparado
    }
}
