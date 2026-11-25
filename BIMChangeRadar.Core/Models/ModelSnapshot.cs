namespace BIMChangeRadar.Core.Models;

/// <summary>Fotografia de um modelo BIM em um instante (versão do projeto).</summary>
public class ModelSnapshot
{
    public string ProjectName { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime? Date { get; set; }
    /// <summary>Origem dos dados, ex.: "json-sample" ou, no futuro, "revit-api".</summary>
    public string? Source { get; set; }
    public List<BimElement> Elements { get; set; } = new();

    public string Label => $"{ProjectName} v{Version}";
}
