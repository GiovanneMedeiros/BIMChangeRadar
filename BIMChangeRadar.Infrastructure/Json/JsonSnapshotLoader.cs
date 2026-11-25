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
            throw new InvalidDataException($"JSON inválido em '{sourceName}': {ex.Message}", ex);
        }
        if (snapshot is null) throw new InvalidDataException($"'{sourceName}' está vazio.");
        Validate(snapshot, sourceName);
        return snapshot;
    }

    private static void Validate(ModelSnapshot s, string source)
    {
        if (string.IsNullOrWhiteSpace(s.ProjectName))
            throw new InvalidDataException($"'{source}': campo 'projectName' é obrigatório.");
        if (s.Elements.Count == 0)
            throw new InvalidDataException($"'{source}': nenhum elemento encontrado em 'elements'.");
        var bad = s.Elements.FirstOrDefault(e => string.IsNullOrWhiteSpace(e.Key));
        if (bad is not null)
            throw new InvalidDataException($"'{source}': elemento '{bad.Name}' sem 'id'/'uniqueId'.");
    }
}
