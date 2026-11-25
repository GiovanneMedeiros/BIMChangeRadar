using System.Text;
using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Services;

namespace BIMChangeRadar.Infrastructure.Export;

/// <summary>
/// CSV com um registro por elemento alterado. Separador ';' (Excel pt-BR abre direto),
/// UTF-8 com BOM, números em cultura invariante e proteção contra CSV injection.
/// </summary>
public class CsvReportExporter : IReportExporter
{
    public const char Separator = ';';
    public string Name => "CSV";
    public string FileExtension => "csv";

    public static readonly string[] Header =
    {
        "Projeto", "SnapshotAnterior", "SnapshotAtual", "DataAnalise", "IdElemento", "Nome", "Categoria",
        "TipoAlteracao", "Impacto", "PropriedadesAlteradas", "ValoresAnteriores", "ValoresAtuais", "ExplicacaoImpacto"
    };

    public async Task ExportAsync(ComparisonResult result, string path, CancellationToken ct = default)
        => await File.WriteAllTextAsync(path, Build(result), new UTF8Encoding(true), ct);

    public static string Build(ComparisonResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(Separator, Header));
        var date = r.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var c in r.ActualChanges)
        {
            var props = c.PropertyChanges;
            var cells = new[]
            {
                r.Current.ProjectName, r.Previous.Label, r.Current.Label, date,
                c.ElementId, c.Name, Pt.Category(c.Category), Pt.ChangeType(c.ChangeType),
                Pt.Impact(c.Impact),
                string.Join(" | ", props.Select(p => Pt.Property(p.PropertyName))),
                string.Join(" | ", props.Select(p => p.OldValue ?? "")),
                string.Join(" | ", props.Select(p => p.NewValue ?? "")),
                string.Join(" ", c.ImpactReasons)
            };
            sb.AppendLine(string.Join(Separator, cells.Select(Escape)));
        }
        return sb.ToString();
    }

    public static string Escape(string? value)
    {
        value ??= "";
        // CSV injection: células iniciadas por = + @ (ou - não numérico) viram texto.
        if (value.Length > 0 && (value[0] is '=' or '+' or '@' || (value[0] == '-' && !(value.Length > 1 && char.IsDigit(value[1])))))
            value = "'" + value;
        return value.IndexOfAny(new[] { Separator, '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }
}
