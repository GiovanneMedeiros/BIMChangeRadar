using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

/// <summary>Tabela de regras documentada no README, verificada regra a regra.</summary>
public class ImpactRulesTests
{
    private static ElementChange Modify(string category, Action<BimElement> edit)
    {
        var a = Wall("X-1", category: category); var b = Wall("X-1", category: category);
        edit(b);
        return new SnapshotComparer().Compare(Snap("1", a), Snap("2", b)).Changes.Single();
    }

    [Theory]
    [InlineData("Wall", "Name", ImpactLevel.Low)]
    [InlineData("Wall", "Mark", ImpactLevel.Low)]
    [InlineData("Wall", "Material", ImpactLevel.Medium)]
    [InlineData("Column", "Material", ImpactLevel.High)]
    [InlineData("Beam", "Material", ImpactLevel.High)]
    [InlineData("Wall", "Length", ImpactLevel.High)]
    [InlineData("Wall", "Height", ImpactLevel.High)]
    [InlineData("Wall", "Width", ImpactLevel.High)]
    [InlineData("Wall", "Area", ImpactLevel.High)]
    [InlineData("Wall", "Volume", ImpactLevel.High)]
    [InlineData("Column", "Height", ImpactLevel.Critical)]
    [InlineData("Floor", "Length", ImpactLevel.Critical)]
    [InlineData("Wall", "Level", ImpactLevel.High)]
    [InlineData("Wall", "Type", ImpactLevel.Medium)]
    [InlineData("Wall", "Category", ImpactLevel.High)]
    public void Property_rules(string category, string property, ImpactLevel expected)
    {
        var c = Modify(category, e =>
        {
            switch (property)
            {
                case "Name": e.Name = "Outro"; break;
                case "Mark": e.Mark = "Z"; break;
                case "Material": e.Material = "Aço"; break;
                case "Length": e.Length = 9; break;
                case "Height": e.Height = 9; break;
                case "Width": e.Width = 9; break;
                case "Area": e.Area = 9; break;
                case "Volume": e.Volume = 9; break;
                case "Level": e.Level = "Pavimento 9"; break;
                case "Type": e.Type = "Outro"; break;
                case "Category": e.Category = "Door"; break;
            }
        });
        Assert.Equal(expected, c.Impact);
        var pc = Assert.Single(c.PropertyChanges);
        Assert.Equal(expected, pc.Impact);
        Assert.False(string.IsNullOrWhiteSpace(pc.ImpactReason));
        Assert.NotEmpty(c.ImpactReasons);
    }

    [Theory]
    [InlineData("FireRating", ImpactLevel.High)]
    [InlineData("LoadBearing", ImpactLevel.High)]
    [InlineData("StructuralUsage", ImpactLevel.High)]
    [InlineData("AcousticRating", ImpactLevel.High)]
    [InlineData("FlowRate", ImpactLevel.High)]
    [InlineData("Comments", ImpactLevel.Low)]
    [InlineData("QualquerOutro", ImpactLevel.Low)]
    public void Parameter_rules(string parameter, ImpactLevel expected)
    {
        var c = Modify("Wall", e => e.Parameters[parameter] = "valor-novo");
        Assert.Equal(expected, c.Impact);
    }

    [Theory]
    [InlineData("Wall")] [InlineData("Door")] [InlineData("Window")] [InlineData("Pipe")] [InlineData("Furniture")] [InlineData("Room")]
    [InlineData("Column")] [InlineData("Beam")] [InlineData("Floor")]
    public void Added_is_always_medium(string category)
    {
        var r = new SnapshotComparer().Compare(Snap("1"), Snap("2", Wall("N", category: category)));
        Assert.Equal(ImpactLevel.Medium, r.Changes.Single().Impact);
    }

    [Theory]
    [InlineData("Wall", ImpactLevel.High)] [InlineData("Door", ImpactLevel.High)] [InlineData("Pipe", ImpactLevel.High)]
    [InlineData("Furniture", ImpactLevel.High)] [InlineData("Window", ImpactLevel.High)] [InlineData("Room", ImpactLevel.High)]
    [InlineData("Column", ImpactLevel.Critical)] [InlineData("Beam", ImpactLevel.Critical)]
    [InlineData("Floor", ImpactLevel.Critical)] [InlineData("Foundation", ImpactLevel.Critical)]
    public void Removed_depends_on_structural_category(string category, ImpactLevel expected)
    {
        var r = new SnapshotComparer().Compare(Snap("1", Wall("O", category: category)), Snap("2"));
        var c = r.Changes.Single();
        Assert.Equal(expected, c.Impact);
        Assert.NotEmpty(c.ImpactReasons);
    }

    [Fact]
    public void Structural_category_match_is_case_insensitive()
    {
        var r = new SnapshotComparer().Compare(Snap("1", Wall("O", category: "column")), Snap("2"));
        Assert.Equal(ImpactLevel.Critical, r.Changes.Single().Impact);
    }

    [Fact]
    public void Multiple_changes_take_the_maximum_and_keep_every_reason()
    {
        var c = Modify("Column", e => { e.Name = "N"; e.Material = "Aço"; e.Height = 9; });
        Assert.Equal(ImpactLevel.Critical, c.Impact);
        Assert.Equal(3, c.PropertyChanges.Count);
        Assert.Equal(3, c.ImpactReasons.Count);
        Assert.Contains(c.PropertyChanges, p => p.PropertyName == "Name" && p.Impact == ImpactLevel.Low);
    }

    [Fact]
    public void Reasons_state_they_are_heuristics_not_engineering_verdicts()
    {
        // As mensagens falam em "pode"/"exige revisão"; nenhuma afirma resultado de cálculo estrutural.
        var c = Modify("Wall", e => e.Material = "X");
        Assert.Contains("Pode", c.ImpactReasons[0]);
    }
}
