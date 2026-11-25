using System.Text;
using BIMChangeRadar.Infrastructure.Json;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class ImportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("radar-import-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    private async Task<Exception> Fail(string json, string file = "t.json")
    {
        var path = Path.Combine(_dir, file);
        await File.WriteAllTextAsync(path, json);
        return await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSnapshotLoader().LoadAsync(path));
    }

    [Fact]
    public async Task Zero_byte_file_gives_friendly_message()
    {
        var path = Path.Combine(_dir, "vazio.json"); File.WriteAllBytes(path, Array.Empty<byte>());
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => new JsonSnapshotLoader().LoadAsync(path));
        Assert.Contains("vazio.json", ex.Message); Assert.Contains("vazio", ex.Message);
        Assert.DoesNotContain("System.", ex.Message);
    }

    [Fact] public async Task Malformed_json() =>
        Assert.Contains("não é um snapshot válido", (await Fail("{ \"projectName\": ")).Message);

    [Fact] public async Task Root_is_array_instead_of_object() =>
        Assert.Contains("não é um snapshot válido", (await Fail("[1,2,3]")).Message);

    [Fact] public async Task Empty_object_missing_projectName() =>
        Assert.Contains("projectName", (await Fail("{}")).Message);

    [Fact] public async Task Elements_empty_collection() =>
        Assert.Contains("nenhum elemento", (await Fail("""{"projectName":"P","version":"1","elements":[]}""")).Message);

    [Fact] public async Task Elements_null() =>
        Assert.Contains("nenhum elemento", (await Fail("""{"projectName":"P","elements":null}""")).Message);

    [Fact] public async Task Elements_property_absent() =>
        Assert.Contains("nenhum elemento", (await Fail("""{"projectName":"P"}""")).Message);

    [Fact] public async Task Null_item_inside_elements() =>
        Assert.Contains("item nulo", (await Fail("""{"projectName":"P","elements":[null]}""")).Message);

    [Fact]
    public async Task Incompatible_type_reports_the_field_path()
    {
        var ex = await Fail("""{"projectName":"P","elements":[{"id":"A","length":"abc"}]}""");
        Assert.Contains("tipo de dado incompatível", ex.Message);
        Assert.Contains("length", ex.Message);
        Assert.DoesNotContain("System.Nullable", ex.Message);
    }

    [Fact] public async Task Unexpected_structure_for_parameters() =>
        Assert.Contains("não é um snapshot válido", (await Fail("""{"projectName":"P","elements":[{"id":"A","parameters":[1,2]}]}""")).Message);

    [Fact]
    public async Task Duplicate_ids_are_rejected_at_load_with_the_id_in_the_message()
    {
        var ex = await Fail("""{"projectName":"P","elements":[{"id":"A","uniqueId":"u1"},{"id":"B","uniqueId":"u1"}]}""");
        Assert.Contains("duplicado", ex.Message); Assert.Contains("u1", ex.Message);
    }

    [Fact] public async Task Element_without_any_identifier() =>
        Assert.Contains("não possui 'id' nem 'uniqueId'", (await Fail("""{"projectName":"P","elements":[{"name":"x"}]}""")).Message);

    [Fact]
    public async Task Missing_optional_properties_are_fine_and_null_texts_are_normalized()
    {
        var path = Path.Combine(_dir, "min.json");
        await File.WriteAllTextAsync(path, """{"projectName":"P","elements":[{"id":"A"},{"id":"B","name":null,"category":null,"parameters":null}]}""");
        var s = await new JsonSnapshotLoader().LoadAsync(path);
        Assert.Equal(2, s.Elements.Count);
        Assert.All(s.Elements, e => { Assert.NotNull(e.Name); Assert.NotNull(e.Category); Assert.NotNull(e.Parameters); });
        Assert.Null(s.Elements[0].Length);
    }

    [Fact]
    public async Task File_not_found_is_reported()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => new JsonSnapshotLoader().LoadAsync(Path.Combine(_dir, "nao-existe.json")));
    }

    [Fact]
    public async Task Accents_and_unicode_survive_loading()
    {
        var path = Path.Combine(_dir, "acentos.json");
        await File.WriteAllTextAsync(path, """{"projectName":"Edifício Ação","elements":[{"id":"A","name":"Pilar São João – 40×40","parameters":{"Comentário":"água"}}]}""", new UTF8Encoding(false));
        var s = await new JsonSnapshotLoader().LoadAsync(path);
        Assert.Equal("Edifício Ação", s.ProjectName);
        Assert.Equal("Pilar São João – 40×40", s.Elements[0].Name);
        Assert.Equal("água", s.Elements[0].Parameters["Comentário"]);
    }

    [Fact]
    public async Task Large_file_with_20000_elements_loads()
    {
        var sb = new StringBuilder("""{"projectName":"Big","version":"1","elements":[""");
        for (int i = 0; i < 20000; i++) { if (i > 0) sb.Append(','); sb.Append("{\"id\":\"E").Append(i).Append("\",\"uniqueId\":\"u").Append(i).Append("\",\"name\":\"N").Append(i).Append("\",\"category\":\"Wall\",\"length\":").Append(i % 9 + 1).Append(".5,\"parameters\":{\"k\":\"v\"}").Append("}"); }
        sb.Append("]}");
        var path = Path.Combine(_dir, "big.json"); await File.WriteAllTextAsync(path, sb.ToString());
        var s = await new JsonSnapshotLoader().LoadAsync(path);
        Assert.Equal(20000, s.Elements.Count);
    }
}
