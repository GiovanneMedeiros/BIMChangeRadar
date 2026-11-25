using System.Text;
using System.Text.RegularExpressions;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Infrastructure.Export;
using BIMChangeRadar.Infrastructure.FileSystem;
using BIMChangeRadar.Infrastructure.Json;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class ExportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("radar-export-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    private static async Task<ComparisonResult> DemoResult()
    {
        var l = new JsonSnapshotLoader();
        return new SnapshotComparer().Compare(await l.LoadAsync(SamplePath("project-v1.json")), await l.LoadAsync(SamplePath("project-v2.json")));
    }

    /// <summary>Parser CSV mínimo (RFC 4180, separador ';') para validar a estrutura do arquivo gerado.</summary>
    internal static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool q = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (q) { if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; } else cell.Append(c); }
            else if (c == '"') q = true;
            else if (c == ';') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\n') { row.Add(cell.ToString().TrimEnd('\r')); cell.Clear(); rows.Add(row); row = new(); }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }

    [Fact]
    public async Task Csv_file_has_utf8_bom_and_consistent_columns_and_all_changes()
    {
        var r = await DemoResult();
        var path = Path.Combine(_dir, "r.csv");
        await new CsvReportExporter().ExportAsync(r, path);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());   // BOM p/ Excel

        var rows = ParseCsv(new UTF8Encoding(false).GetString(bytes).TrimStart('﻿'));
        Assert.Equal(CsvReportExporter.Header.Length, rows[0].Count);
        Assert.Equal(r.TotalChanges + 1, rows.Count);                              // cabeçalho + 1 linha por alteração
        Assert.All(rows, row => Assert.Equal(CsvReportExporter.Header.Length, row.Count));
        Assert.DoesNotContain(rows.Skip(1), row => row[7] == "INALTERADO");         // inalterados fora do relatório
    }

    [Fact]
    public async Task Csv_contents_match_the_analysis_for_W104_and_a_removed_column()
    {
        var r = await DemoResult();
        var rows = ParseCsv(CsvReportExporter.Build(r));
        var w = rows.Single(x => x[4] == "W-104");
        Assert.Equal("Edifício Alpha", w[0]);
        Assert.Equal("Edifício Alpha v1.0", w[1]); Assert.Equal("Edifício Alpha v2.0", w[2]);
        Assert.Equal(("Parede", "MODIFICADO", "ALTO"), (w[6], w[7], w[8]));
        var props = w[9].Split(" | "); var olds = w[10].Split(" | "); var news = w[11].Split(" | ");
        Assert.Equal(props.Length, olds.Length); Assert.Equal(props.Length, news.Length);
        int i = Array.IndexOf(props, "Comprimento");
        Assert.Equal(("3.2", "4.8"), (olds[i], news[i]));
        int m = Array.IndexOf(props, "Material");
        Assert.Equal(("Concreto", "Drywall"), (olds[m], news[m]));
        var col = rows.Single(x => x[4] == "C-112");
        Assert.Equal(("Pilar", "REMOVIDO", "CRÍTICO"), (col[6], col[7], col[8]));
        Assert.Equal("", col[10]);   // removido não tem valores anterior/atual por propriedade
        Assert.Contains("estrutural", col[12]);
    }

    [Fact]
    public async Task Csv_preserves_accents_and_quotes_special_characters()
    {
        var a = Wall("W-1", name: "Parede; \"Ação\"\nlinha2"); var b = Wall("W-1", name: "Pilar São João", length: 9);
        var res = new SnapshotComparer().Compare(Snap("1", a), Snap("2", b));
        var path = Path.Combine(_dir, "e.csv"); await new CsvReportExporter().ExportAsync(res, path);
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains("São João", text);
        var row = ParseCsv(text.TrimStart('﻿'))[1];
        Assert.Contains("Parede; \"Ação\"\nlinha2", row[10]);   // valor anterior do Nome, íntegro após escape
    }

    [Fact]
    public async Task Csv_always_exports_the_full_analysis_not_the_filtered_view()
    {
        // Comportamento definido e documentado: exportação ignora filtros da tabela.
        var rows = ParseCsv(CsvReportExporter.Build(await DemoResult()));
        Assert.Equal(41 + 1, rows.Count);
    }

    [Fact]
    public async Task Html_is_self_contained_totals_match_and_content_is_escaped()
    {
        var r = await DemoResult();
        var html = HtmlReportExporter.Build(r);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        foreach (var (label, v) in new[] { ("Elementos", r.TotalElements), ("Adicionados", r.Added), ("Removidos", r.Removed),
                     ("Modificados", r.Modified), ("Inalterados", r.Unchanged), ("Alto impacto", r.HighImpact) })
            Assert.Contains($"<b>{v}</b><span>{label}</span>", html);
        Assert.Equal(r.TotalChanges, Regex.Matches(html, "<tr><td>").Count);               // 1 linha por alteração
        Assert.DoesNotContain("http://", html); Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("<script", html); Assert.DoesNotContain("<link", html); Assert.DoesNotContain("src=", html);
        Assert.Contains("Edifício Alpha", html);
        // tags balanceadas nas estruturas principais
        foreach (var tag in new[] { "table", "thead", "tbody", "main", "header", "body", "html", "style" })
            Assert.Equal(Regex.Matches(html, $"<{tag}[ >]").Count, Regex.Matches(html, $"</{tag}>").Count);
    }

    [Fact]
    public async Task Html_file_is_written_as_utf8()
    {
        var path = Path.Combine(_dir, "r.html");
        await new HtmlReportExporter().ExportAsync(await DemoResult(), path);
        Assert.Contains("Edifício", new UTF8Encoding(false, true).GetString(await File.ReadAllBytesAsync(path)));
    }
}

public class HistoryStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("radar-hist-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);
    private static HistoryRecord Rec(int i) => new("Proj", "1", "2", $"a{i}.json", $"b{i}.json", new DateTime(2026, 11, 25, 10, 0, i % 60), 10 + i, 1, 2, 3, 4, i);

    [Fact]
    public void Roundtrip_preserves_every_field_and_leaves_no_temp_file()
    {
        var store = new HistoryStore(Path.Combine(_dir, "sub", "h.json"));
        Assert.True(store.Save(new[] { Rec(1), Rec(2) }));
        Assert.Equal(new[] { Rec(1), Rec(2) }, store.Load());
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact] public void Missing_file_loads_empty() => Assert.Empty(new HistoryStore(Path.Combine(_dir, "x.json")).Load());

    [Fact]
    public void Corrupted_file_loads_empty_instead_of_crashing()
    {
        var p = Path.Combine(_dir, "bad.json"); File.WriteAllText(p, "{ isso não é json");
        Assert.Empty(new HistoryStore(p).Load());
    }

    [Fact]
    public void Keeps_only_the_most_recent_50()
    {
        var store = new HistoryStore(Path.Combine(_dir, "h.json"));
        store.Save(Enumerable.Range(0, 80).Select(Rec));
        var l = store.Load();
        Assert.Equal(HistoryStore.MaxEntries, l.Count);
        Assert.Equal(Rec(0), l[0]);
    }
}
