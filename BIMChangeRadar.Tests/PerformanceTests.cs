using System.Diagnostics;
using BIMChangeRadar.App.ViewModels;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Infrastructure.Export;
using BIMChangeRadar.Infrastructure.FileSystem;
using BIMChangeRadar.Infrastructure.Json;
using Xunit.Abstractions;

namespace BIMChangeRadar.Tests;

/// <summary>Limites generosos (evitam flakiness); os tempos reais são impressos e registrados no TEST_REPORT.</summary>
public class PerformanceTests
{
    private readonly ITestOutputHelper _out;
    public PerformanceTests(ITestOutputHelper o) => _out = o;

    [Theory]
    [InlineData(1000)] [InlineData(5000)] [InlineData(10000)] [InlineData(50000)]
    public void Compare_scales_and_stays_correct(int n)
    {
        var (a, b, added, removed, modified) = Synth.Pair(n);
        GC.Collect(); var mem0 = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();
        var r = new SnapshotComparer().Compare(a, b);
        sw.Stop();
        var mem = (GC.GetTotalMemory(false) - mem0) / 1024.0 / 1024.0;
        _out.WriteLine($"n={n}: comparação {sw.ElapsedMilliseconds} ms, +{mem:F1} MB retidos; +{r.Added} -{r.Removed} ~{r.Modified} ={r.Unchanged}");

        Assert.Equal(added, r.Added); Assert.Equal(removed, r.Removed); Assert.Equal(modified, r.Modified);
        Assert.Equal(a.Elements.Count + added, r.TotalElements);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(n >= 50000 ? 20 : 5), $"lento: {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Exports_of_10k_elements_are_fast()
    {
        var (a, b, _, _, _) = Synth.Pair(10000);
        var r = new SnapshotComparer().Compare(a, b);
        var sw = Stopwatch.StartNew(); var csv = CsvReportExporter.Build(r); var tCsv = sw.ElapsedMilliseconds;
        sw.Restart(); var html = HtmlReportExporter.Build(r); var tHtml = sw.ElapsedMilliseconds;
        _out.WriteLine($"10k: CSV {tCsv} ms ({csv.Length / 1024} KB), HTML {tHtml} ms ({html.Length / 1024} KB), {r.TotalChanges} alterações");
        Assert.True(tCsv < 3000 && tHtml < 3000);
    }

    [Fact]
    public async Task Viewmodel_filtering_and_searching_10k_is_responsive()
    {
        var dir = Directory.CreateTempSubdirectory("radar-perf-").FullName;
        try
        {
            var (a, b, _, _, _) = Synth.Pair(10000);
            var pa = Path.Combine(dir, "a.json"); var pb = Path.Combine(dir, "b.json");
            await File.WriteAllTextAsync(pa, System.Text.Json.JsonSerializer.Serialize(a));
            await File.WriteAllTextAsync(pb, System.Text.Json.JsonSerializer.Serialize(b));
            var dialogs = new FakeDialogs(); dialogs.Open.Enqueue(pa); dialogs.Open.Enqueue(pb);
            var vm = new MainViewModel(new JsonSnapshotLoader(), new SnapshotComparer(), new HistoryStore(Path.Combine(dir, "h.json")));
            vm.AttachDialogs(dialogs);

            var sw = Stopwatch.StartNew();
            await vm.SelectPreviousCommand.ExecuteAsync(null); await vm.SelectCurrentCommand.ExecuteAsync(null);
            var tLoad = sw.ElapsedMilliseconds; sw.Restart();
            vm.CompareCommand.Execute(null);
            var tCompare = sw.ElapsedMilliseconds; sw.Restart();
            vm.SearchText = "Elemento 77"; var tSearch = sw.ElapsedMilliseconds; sw.Restart();
            vm.SearchText = ""; vm.SelectedImpact = "Alto"; var tFilter = sw.ElapsedMilliseconds;
            _out.WriteLine($"10k via VM: carga {tLoad} ms, comparar+dashboard {tCompare} ms, busca {tSearch} ms, filtro {tFilter} ms; linhas={vm.Rows.Count}");
            Assert.True(tSearch < 500 && tFilter < 500, "busca/filtro devem responder em < 0,5 s");
            Assert.True(vm.Rows.All(r => r.ImpactText == "ALTO"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
