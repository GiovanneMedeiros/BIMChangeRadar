using System.Text;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Infrastructure.Export;
using BIMChangeRadar.Infrastructure.Json;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class InfrastructureTests
{
    [Fact]
    public async Task Loads_sample_json()
    {
        var s = await new JsonSnapshotLoader().LoadAsync(SamplePath("project-v1.json"));
        Assert.Equal("Edifício Alpha", s.ProjectName);
        Assert.Equal("1.0", s.Version);
        Assert.True(s.Elements.Count > 100);
        var w = s.Elements.Single(e => e.Id == "W-104");
        Assert.Equal(3.2, w.Length);
        Assert.Equal("60min", w.Parameters["FireRating"]);
    }

    [Fact]
    public async Task Parameters_accept_numbers_and_booleans()
    {
        var json = """{"projectName":"P","version":"1","elements":[{"id":"A","name":"a","category":"Wall","parameters":{"N":3.5,"B":true,"S":"x"}}]}""";
        var s = await JsonSnapshotLoader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), "t");
        Assert.Equal("3.5", s.Elements[0].Parameters["N"]);
        Assert.Equal("true", s.Elements[0].Parameters["B"]);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"version":"1","elements":[{"id":"A"}]}""")]
    [InlineData("""{"projectName":"P","elements":[]}""")]
    public async Task Invalid_json_gives_clear_error(string json)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            JsonSnapshotLoader.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), "bad.json"));
    }

    [Fact]
    public async Task Demo_comparison_produces_all_change_types()
    {
        var l = new JsonSnapshotLoader();
        var r = new SnapshotComparer().Compare(await l.LoadAsync(SamplePath("project-v1.json")), await l.LoadAsync(SamplePath("project-v2.json")));
        Assert.Equal(8, r.Removed);
        Assert.Equal(12, r.Added);
        Assert.Equal(21, r.Modified);
        Assert.Equal(r.TotalElements, r.Added + r.Removed + r.Modified + r.Unchanged);
        Assert.True(r.HighImpact > 0);
        var w104 = r.Changes.Single(c => c.ElementId == "W-104");
        Assert.Equal(3, w104.PropertyChanges.Count(p => p.PropertyName is "Length" or "Material" or "Level"));
        Assert.Contains("4,48", w104.Quantity!.Message);
    }

    [Fact]
    public async Task Csv_export_has_header_rows_and_escapes()
    {
        var a = Wall(); var b = Wall(name: "=HYPERLINK(\"x\")", length: 5);
        var result = new SnapshotComparer().Compare(Snap("1", a), Snap("2", b));
        var path = Path.Combine(Path.GetTempPath(), $"radar-{Guid.NewGuid():N}.csv");
        try
        {
            await new CsvReportExporter().ExportAsync(result, path);
            var lines = (await File.ReadAllTextAsync(path)).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(string.Join(';', CsvReportExporter.Header), lines[0]);
            Assert.Equal(2, lines.Length);
            Assert.Contains("MODIFICADO", lines[1]);
            Assert.Contains("ALTO", lines[1]);
            Assert.Contains("Nome | Comprimento", lines[1]);
            Assert.Contains("\"'=HYPERLINK", lines[1]); // protegido contra CSV injection
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Html_export_encodes_content()
    {
        var result = new SnapshotComparer().Compare(Snap("1", Wall()), Snap("2", Wall(name: "<script>")));
        var html = HtmlReportExporter.Build(result);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }
}
