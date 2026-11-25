namespace BIMChangeRadar.Infrastructure.FileSystem;

/// <summary>Localiza os snapshots de demonstração (copiados para a pasta de saída do app).</summary>
public static class SampleLocator
{
    public const string V1 = "project-v1.json";
    public const string V2 = "project-v2.json";

    public static (string Previous, string Current) FindDemoFiles()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Samples"),
            Path.Combine(Directory.GetCurrentDirectory(), "Samples"),
        };
        foreach (var dir in candidates)
        {
            var a = Path.Combine(dir, V1);
            var b = Path.Combine(dir, V2);
            if (File.Exists(a) && File.Exists(b)) return (a, b);
        }
        throw new FileNotFoundException($"Arquivos de demonstração não encontrados em: {string.Join(", ", candidates)}");
    }
}
