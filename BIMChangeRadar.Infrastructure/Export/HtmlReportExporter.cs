using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text;
using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Services;

namespace BIMChangeRadar.Infrastructure.Export;

/// <summary>Relatório HTML autocontido (um arquivo, CSS inline) pronto para compartilhar ou imprimir em PDF.</summary>
public class HtmlReportExporter : IReportExporter
{
    public string Name => "HTML";
    public string FileExtension => "html";

    public async Task ExportAsync(ComparisonResult result, string path, CancellationToken ct = default)
        => await File.WriteAllTextAsync(path, Build(result), new UTF8Encoding(false), ct);

    // Escapa apenas caracteres perigosos (<, >, &, aspas); acentos permanecem legíveis no HTML (arquivo é UTF-8).
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Create(UnicodeRanges.All);
    private static string E(string? s) => Encoder.Encode(s ?? "");

    public static string Build(ComparisonResult r)
    {
        var sb = new StringBuilder();
        sb.Append("""
<!DOCTYPE html><html lang="pt-BR"><head><meta charset="utf-8"><title>BIM Change Radar — Relatório</title><style>
body{font-family:-apple-system,Segoe UI,Inter,sans-serif;margin:0;background:#f4f6fa;color:#1b2540}
header{background:#0f1f3d;color:#fff;padding:28px 40px}header h1{margin:0;font-size:24px}header p{margin:4px 0 0;color:#9fb3d9}
main{padding:28px 40px}.cards{display:flex;gap:14px;flex-wrap:wrap;margin-bottom:24px}
.card{background:#fff;border-radius:10px;padding:14px 20px;min-width:130px;box-shadow:0 1px 3px #0001}
.card b{display:block;font-size:26px}.card span{font-size:12px;color:#5b6b8c;text-transform:uppercase;letter-spacing:.04em}
table{width:100%;border-collapse:collapse;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 1px 3px #0001;font-size:13px}
th{background:#e8edf6;text-align:left;padding:10px 12px;font-size:12px;text-transform:uppercase;letter-spacing:.04em;color:#41507a}
td{padding:10px 12px;border-top:1px solid #edf0f6;vertical-align:top}
.b{display:inline-block;padding:2px 9px;border-radius:99px;font-size:11px;font-weight:700}
.ADDED{background:#dcf5e6;color:#12713a}.REMOVED{background:#fde3e3;color:#a12020}.MODIFIED{background:#fff0d2;color:#8a5a00}
.LOW{background:#e3ecfb;color:#2c5aa8}.MEDIUM{background:#fff0d2;color:#8a5a00}.HIGH{background:#ffe0cc;color:#b24700}.CRITICAL{background:#c62828;color:#fff}
.old{color:#a12020}.new{color:#12713a}footer{padding:20px 40px;color:#5b6b8c;font-size:12px}
</style></head><body>
""");
        sb.Append($"<header><h1>BIM Change Radar</h1><p>{E(r.Current.ProjectName)} · {E(r.Previous.Label)} → {E(r.Current.Label)} · análise em {r.AnalyzedAt:dd/MM/yyyy HH:mm}</p></header><main><div class=\"cards\">");
        void Card(string label, int v) => sb.Append($"<div class=\"card\"><b>{v}</b><span>{label}</span></div>");
        Card("Elementos", r.TotalElements); Card("Adicionados", r.Added); Card("Removidos", r.Removed);
        Card("Modificados", r.Modified); Card("Inalterados", r.Unchanged); Card("Alto impacto", r.HighImpact);
        sb.Append("</div><table><thead><tr><th>ID</th><th>Nome</th><th>Categoria</th><th>Alteração</th><th>Impacto</th><th>Detalhes</th></tr></thead><tbody>");

        foreach (var c in r.ActualChanges)
        {
            sb.Append($"<tr><td>{E(c.ElementId)}</td><td>{E(c.Name)}</td><td>{E(Pt.Category(c.Category))}</td>");
            sb.Append($"<td><span class=\"b {c.ChangeType.ToString().ToUpperInvariant()}\">{Pt.ChangeType(c.ChangeType)}</span></td>");
            sb.Append($"<td><span class=\"b {c.Impact.ToString().ToUpperInvariant()}\">{Pt.Impact(c.Impact)}</span></td><td>");
            foreach (var p in c.PropertyChanges)
                sb.Append($"<div><b>{E(Pt.Property(p.PropertyName))}</b>: <span class=\"old\">{E(p.OldValue ?? "—")}</span> → <span class=\"new\">{E(p.NewValue ?? "—")}</span></div>");
            foreach (var reason in c.ImpactReasons) sb.Append($"<div style=\"color:#5b6b8c;margin-top:4px\">{E(reason)}</div>");
            if (c.Quantity is not null) sb.Append($"<div style=\"margin-top:4px\"><i>{E(c.Quantity.Message)}</i></div>");
            sb.Append("</td></tr>");
        }
        sb.Append("</tbody></table></main><footer>Gerado por BIM Change Radar. Impactos são estimativas baseadas em regras e exigem validação de um profissional.</footer></body></html>");
        return sb.ToString();
    }
}
