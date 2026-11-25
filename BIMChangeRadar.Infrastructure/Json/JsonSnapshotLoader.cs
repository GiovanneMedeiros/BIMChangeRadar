using System.Text.Json;
using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Infrastructure.Json;

/// <summary>Carrega snapshots de modelos BIM a partir de arquivos JSON (System.Text.Json).</summary>
public class JsonSnapshotLoader : ISnapshotLoader
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new ParametersConverter() }
    };

    public async Task<ModelSnapshot> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Arquivo não encontrado: {path}", path);
        await using var stream = File.OpenRead(path);
        return await ReadAsync(stream, Path.GetFileName(path), ct);
    }

    public static async Task<ModelSnapshot> ReadAsync(Stream stream, string sourceName, CancellationToken ct = default)
    {
        ModelSnapshot? snapshot;
        try
        {
            snapshot = await JsonSerializer.DeserializeAsync<ModelSnapshot>(stream, Options, ct);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(Describe(ex, sourceName), ex);
        }
        if (snapshot is null) throw new InvalidDataException($"'{sourceName}' está vazio ou não contém um snapshot.");
        Validate(snapshot, sourceName);
        return snapshot;
    }

    /// <summary>Mensagem amigável em português (sem expor texto técnico do serializador).</summary>
    private static string Describe(JsonException ex, string source)
    {
        var where = ex.Path is { Length: > 0 } && ex.Path != "$" ? $" no campo '{ex.Path}'" : "";
        var line = ex.LineNumber is long l ? $" (linha {l + 1})" : "";
        return ex.Message.Contains("does not contain any JSON tokens", StringComparison.OrdinalIgnoreCase)
            ? $"'{source}' está vazio."
            : $"'{source}' não é um snapshot válido: JSON malformado ou com tipo de dado incompatível{where}{line}.";
    }

    private static void Validate(ModelSnapshot s, string source)
    {
        s.ProjectName = s.ProjectName?.Trim() ?? "";
        s.Version ??= "";
        if (s.ProjectName.Length == 0)
            throw new InvalidDataException($"'{source}': o campo 'projectName' é obrigatório.");
        s.Elements ??= new();
        if (s.Elements.Count == 0)
            throw new InvalidDataException($"'{source}': nenhum elemento encontrado em 'elements'.");
        if (s.Elements.Any(e => e is null))
            throw new InvalidDataException($"'{source}': a lista 'elements' contém um item nulo.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in s.Elements)
        {
            // Campos de texto explicitamente null viram vazio (evita NullReference em regras e exportação).
            e.Id ??= ""; e.UniqueId ??= ""; e.Name ??= ""; e.Category ??= "";
            e.Parameters ??= new();
            if (string.IsNullOrWhiteSpace(e.Key))
                throw new InvalidDataException($"'{source}': o elemento '{e.Name}' não possui 'id' nem 'uniqueId'.");
            if (!seen.Add(e.Key))
                throw new InvalidDataException($"'{source}': identificador duplicado '{e.Key}'. Cada elemento precisa de um id único.");
        }
    }
}
