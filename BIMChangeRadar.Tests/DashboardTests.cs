using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Infrastructure.Json;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

/// <summary>Os números do dashboard são recalculados aqui de forma independente (a partir dos snapshots brutos).</summary>
public class DashboardTests
{
    private static async Task<(ModelSnapshot, ModelSnapshot)> Demo()
    {
        var l = new JsonSnapshotLoader();
        return (await l.LoadAsync(SamplePath("project-v1.json")), await l.LoadAsync(SamplePath("project-v2.json")));
    }

    [Fact]
    public async Task Counters_match_independent_recount_of_raw_snapshots()
    {
        var (a, b) = await Demo();
        var prevKeys = a.Elements.Select(e => e.UniqueId).ToHashSet();
        var currKeys = b.Elements.Select(e => e.UniqueId).ToHashSet();
        var r = new SnapshotComparer().Compare(a, b);

        Assert.Equal(prevKeys.Union(currKeys).Count(), r.TotalElements);
        Assert.Equal(currKeys.Except(prevKeys).Count(), r.Added);
        Assert.Equal(prevKeys.Except(currKeys).Count(), r.Removed);
        var common = prevKeys.Intersect(currKeys).ToList();
        var byIdA = a.Elements.ToDictionary(e => e.UniqueId); var byIdB = b.Elements.ToDictionary(e => e.UniqueId);
        var modifiedExpected = common.Count(k => System.Text.Json.JsonSerializer.Serialize(byIdA[k]) != System.Text.Json.JsonSerializer.Serialize(byIdB[k]));
        Assert.Equal(modifiedExpected, r.Modified);
        Assert.Equal(common.Count - modifiedExpected, r.Unchanged);
        Assert.Equal(r.Added + r.Removed + r.Modified, r.TotalChanges);
        Assert.Equal(r.TotalElements, r.Added + r.Removed + r.Modified + r.Unchanged);
    }

    [Fact]
    public async Task Impact_and_category_distributions_add_up_to_total_changes()
    {
        var (a, b) = await Demo();
        var r = new SnapshotComparer().Compare(a, b);
        var byImpact = new[] { ImpactLevel.Low, ImpactLevel.Medium, ImpactLevel.High, ImpactLevel.Critical }.Sum(l => r.CountByImpact(l));
        Assert.Equal(r.TotalChanges, byImpact);
        Assert.Equal(r.CountByImpact(ImpactLevel.High) + r.CountByImpact(ImpactLevel.Critical), r.HighImpact);
        var byCat = r.ActualChanges.GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(r.TotalChanges, byCat.Values.Sum());
        Assert.Equal(10, byCat["Wall"]);   // W-104,107,115,120,122 modificados + W-110,118 removidos + 3 adicionados
        Assert.DoesNotContain(r.ActualChanges, c => c.Impact == ImpactLevel.None);
    }

    [Fact]
    public async Task Demo_values_are_stable_documented_numbers()
    {
        var (a, b) = await Demo();
        var r = new SnapshotComparer().Compare(a, b);
        Assert.Equal((122, 12, 8, 21, 81, 41), (r.TotalElements, r.Added, r.Removed, r.Modified, r.Unchanged, r.TotalChanges));
        Assert.Equal(20, r.HighImpact);
        Assert.Equal(5, r.CountByImpact(ImpactLevel.Critical));
    }

    [Fact]
    public void Identical_projects_give_zeroed_counters()
    {
        var s = Snap("1", Wall("A"), Wall("B"));
        var r = new SnapshotComparer().Compare(s, Snap("2", Synth.Clone(s.Elements[0]), Synth.Clone(s.Elements[1])));
        Assert.Equal((2, 0, 0, 0, 2, 0, 0), (r.TotalElements, r.Added, r.Removed, r.Modified, r.Unchanged, r.TotalChanges, r.HighImpact));
    }
}
