namespace BIMChangeRadar.Core.Models;

/// <summary>
/// Elemento BIM de um snapshot. As propriedades de primeira classe cobrem o que
/// é comum a quase todo elemento; <see cref="Parameters"/> guarda parâmetros
/// personalizados (flexível, nem todo elemento tem todos).
/// </summary>
public class BimElement
{
    public string Id { get; set; } = "";
    /// <summary>Identificador estável usado para casar elementos entre versões.</summary>
    public string UniqueId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Type { get; set; }
    public string? Level { get; set; }
    public string? Material { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public double? Length { get; set; }
    public double? Area { get; set; }
    public double? Volume { get; set; }
    public string? Mark { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>Chave de correspondência: UniqueId; se ausente, cai para Id.</summary>
    public string Key => string.IsNullOrWhiteSpace(UniqueId) ? Id : UniqueId;
}
