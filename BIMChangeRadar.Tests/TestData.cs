using BIMChangeRadar.Core.Models;

namespace BIMChangeRadar.Tests;

internal static class TestData
{
    public static BimElement Wall(string id = "W-1", double length = 3.2, double height = 2.8, string material = "Concrete",
        string name = "Wall A", string level = "Floor 1", string category = "Wall") => new()
    {
        Id = id, UniqueId = id.ToLowerInvariant(), Name = name, Category = category, Type = "Interior Wall",
        Level = level, Material = material, Length = length, Height = height,
        Parameters = new() { ["FireRating"] = "60min", ["Comments"] = "x" }
    };

    public static ModelSnapshot Snap(string version, params BimElement[] els) =>
        new() { ProjectName = "T", Version = version, Elements = els.ToList() };

    public static string SamplePath(string file) => Path.Combine(AppContext.BaseDirectory, "Samples", file);
}

internal static class Synth
{
    /// <summary>Gera snapshots sintéticos determinísticos: ~10% removidos, ~10% adicionados, ~20% modificados.</summary>
    public static (ModelSnapshot Prev, ModelSnapshot Curr, int Added, int Removed, int Modified) Pair(int n)
    {
        string[] cats = { "Wall", "Door", "Window", "Column", "Beam", "Pipe", "Furniture", "Floor", "Room" };
        var prev = new List<BimElement>(); var curr = new List<BimElement>();
        int added = 0, removed = 0, modified = 0;
        for (int i = 0; i < n; i++)
        {
            var e = new BimElement
            {
                Id = $"E-{i}", UniqueId = $"uid-{i}", Name = $"Elemento {i}", Category = cats[i % cats.Length],
                Type = "T" + i % 7, Level = "Pavimento " + (1 + i % 5), Material = "Concreto",
                Length = 3 + i % 5, Height = 2.8, Width = 0.2, Parameters = new() { ["Mark"] = $"M{i}", ["FireRating"] = "60min" }
            };
            prev.Add(e);
            switch (i % 10)
            {
                case 0: removed++; break;
                case 1: case 2:
                    modified++;
                    curr.Add(new BimElement { Id = e.Id, UniqueId = e.UniqueId, Name = e.Name, Category = e.Category, Type = e.Type,
                        Level = e.Level, Material = i % 2 == 0 ? "Aço" : "Concreto", Length = e.Length + (i % 2 == 0 ? 0 : 1), Height = e.Height, Width = e.Width,
                        Parameters = new(e.Parameters) });
                    break;
                default: curr.Add(Clone(e)); break;
            }
            if (i % 10 == 5)
            {
                added++;
                curr.Add(new BimElement { Id = $"N-{i}", UniqueId = $"new-{i}", Name = $"Novo {i}", Category = e.Category, Length = 2, Height = 2 });
            }
        }
        return (new ModelSnapshot { ProjectName = "Perf", Version = "1", Elements = prev },
                new ModelSnapshot { ProjectName = "Perf", Version = "2", Elements = curr }, added, removed, modified);
    }

    public static BimElement Clone(BimElement e) => new()
    {
        Id = e.Id, UniqueId = e.UniqueId, Name = e.Name, Category = e.Category, Type = e.Type, Level = e.Level, Material = e.Material,
        Length = e.Length, Height = e.Height, Width = e.Width, Area = e.Area, Volume = e.Volume, Mark = e.Mark,
        Parameters = new(e.Parameters)
    };
}
