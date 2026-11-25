using System.Text.Json;

namespace BIMChangeRadar.Infrastructure.FileSystem;

/// <summary>Registro resumido de uma análise (persistido). Os snapshots são reabertos pelos caminhos dos arquivos.</summary>
public record HistoryRecord(
    string Project, string PreviousVersion, string CurrentVersion,
    string PreviousPath, string CurrentPath, DateTime AnalyzedAt,
    int Total, int Added, int Removed, int Modified, int Unchanged, int HighImpact);

/// <summary>Persistência simples do histórico em JSON (pasta de dados do usuário). Tolerante a arquivo ausente/corrompido.</summary>
public class HistoryStore
{
    public const int MaxEntries = 50;
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };
    private readonly string _path;

    /// <summary>Variável de ambiente que redireciona a pasta de dados (útil para testes/QA).</summary>
    public const string DataDirEnv = "BIMCHANGERADAR_DATA_DIR";

    public HistoryStore(string? path = null) =>
        _path = path ?? Path.Combine(
            Environment.GetEnvironmentVariable(DataDirEnv) is { Length: > 0 } d ? d
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BIMChangeRadar"),
            "history.json");

    public string FilePath => _path;

    public List<HistoryRecord> Load()
    {
        try
        {
            if (!File.Exists(_path)) return new();
            return JsonSerializer.Deserialize<List<HistoryRecord>>(File.ReadAllText(_path), Opts) ?? new();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new(); // histórico corrompido/inacessível não deve impedir o app de abrir
        }
    }

    /// <summary>Grava de forma atômica (arquivo temporário + move). Retorna false se não foi possível salvar.</summary>
    public bool Save(IEnumerable<HistoryRecord> records)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(records.Take(MaxEntries).ToList(), Opts));
            File.Move(tmp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
