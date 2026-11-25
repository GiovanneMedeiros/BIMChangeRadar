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
