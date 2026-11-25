using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Core.Services;

/// <summary>Rótulos em português para exibição (UI e relatórios). Os dados/identificadores internos permanecem em inglês.</summary>
public static class Pt
{
    public static string ChangeType(ChangeType t) => t switch
    {
        Models.ChangeType.Added => "ADICIONADO", Models.ChangeType.Removed => "REMOVIDO",
        Models.ChangeType.Modified => "MODIFICADO", _ => "INALTERADO"
    };

    public static string Impact(ImpactLevel l) => l switch
    {
        ImpactLevel.Low => "BAIXO", ImpactLevel.Medium => "MÉDIO", ImpactLevel.High => "ALTO",
        ImpactLevel.Critical => "CRÍTICO", _ => "—"
    };

    public static string Kind(PropertyKind k) => k switch
    {
        PropertyKind.Text => "Texto", PropertyKind.Classification => "Classificação", PropertyKind.Material => "Material",
        PropertyKind.Dimension => "Dimensão", PropertyKind.Location => "Localização", _ => "Parâmetro"
    };

    private static readonly Dictionary<string, string> Props = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Name"] = "Nome", ["Mark"] = "Marca", ["Category"] = "Categoria", ["Type"] = "Tipo", ["Level"] = "Nível",
        ["Material"] = "Material", ["Width"] = "Largura", ["Height"] = "Altura", ["Length"] = "Comprimento",
        ["Area"] = "Área", ["Volume"] = "Volume", ["FireRating"] = "Resist. ao fogo", ["Comments"] = "Comentários",
        ["LoadBearing"] = "Carga estrutural", ["StructuralUsage"] = "Uso estrutural", ["Hardware"] = "Ferragem",
        ["GlazingType"] = "Tipo de vidro", ["Occupancy"] = "Ocupação", ["Department"] = "Departamento",
        ["System"] = "Sistema", ["FlowRate"] = "Vazão"
    };

    public static string Property(string name)
    {
        var isParam = name.StartsWith("Parameters.");
        var key = isParam ? name["Parameters.".Length..] : name;
        var label = Props.TryGetValue(key, out var v) ? v : key;
        return isParam ? $"{label} (parâmetro)" : label;
    }

    private static readonly Dictionary<string, string> Cats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wall"] = "Parede", ["Door"] = "Porta", ["Window"] = "Janela", ["Room"] = "Ambiente", ["Floor"] = "Laje",
        ["Column"] = "Pilar", ["Beam"] = "Viga", ["Pipe"] = "Tubulação", ["Furniture"] = "Mobiliário"
    };

    public static string Category(string c) => Cats.TryGetValue(c, out var v) ? v : c;
}
