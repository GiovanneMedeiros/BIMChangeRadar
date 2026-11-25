using BIMChangeRadar.App.ViewModels;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Infrastructure.FileSystem;
using BIMChangeRadar.Infrastructure.Json;
using static BIMChangeRadar.Tests.TestData;

namespace BIMChangeRadar.Tests;

public class FakeDialogs : IDialogService
{
    public Queue<string?> Open { get; } = new();
    public Queue<string?> Save { get; } = new();
    public Task<string?> PickJsonAsync(string title) => Task.FromResult(Open.Count > 0 ? Open.Dequeue() : null);
    public Task<string?> PickSaveAsync(string title, string suggestedName, string extension) => Task.FromResult(Save.Count > 0 ? Save.Dequeue() : null);
}

/// <summary>Testa a lógica da tela (estado, filtros, histórico, exportação) sem abrir janela.</summary>
public class ViewModelTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("radar-vm-").FullName;
    private readonly FakeDialogs _dialogs = new();
    public void Dispose() => Directory.Delete(_dir, true);

    private string HistoryPath => Path.Combine(_dir, "history.json");
    private MainViewModel NewVm()
    {
        var vm = new MainViewModel(new JsonSnapshotLoader(), new SnapshotComparer(), new HistoryStore(HistoryPath));
        vm.AttachDialogs(_dialogs);
        return vm;
    }
    private async Task<MainViewModel> DemoVm() { var vm = NewVm(); await vm.LoadDemoCommand.ExecuteAsync(null); return vm; }

    [Fact]
    public async Task Load_demo_compares_and_fills_dashboard_cards_from_real_data()
    {
        var vm = await DemoVm();
        Assert.Null(vm.ErrorMessage);
        Assert.True(vm.HasResult);
        var cards = vm.Cards.ToDictionary(c => c.Label, c => c.Value);
        Assert.Equal(122, cards["Total de elementos"]); Assert.Equal(12, cards["Adicionados"]); Assert.Equal(8, cards["Removidos"]);
        Assert.Equal(21, cards["Modificados"]); Assert.Equal(81, cards["Inalterados"]); Assert.Equal(41, cards["Total de alterações"]);
        Assert.Equal(20, cards["Alto impacto"]);
        Assert.Equal(41, vm.ImpactBars.Sum(b => b.Value));
        Assert.Equal(41, vm.CategoryBars.Sum(b => b.Value));
        Assert.Equal("41 de 41 alterações", vm.RowCountText);
    }

    [Fact]
    public async Task Default_table_hides_unchanged()
    {
        var vm = await DemoVm();
        Assert.Equal(41, vm.Rows.Count);
        Assert.DoesNotContain(vm.Rows, r => r.ChangeTypeText == "INALTERADO");
    }

    [Theory]
    [InlineData("Adicionado", 12)] [InlineData("Removido", 8)] [InlineData("Modificado", 21)] [InlineData("Inalterado", 81)]
    public async Task Change_type_filter(string option, int expected)
    {
        var vm = await DemoVm();
        vm.SelectedChangeType = option;
        Assert.Equal(expected, vm.Rows.Count);
        Assert.All(vm.Rows, r => Assert.Equal(option.ToUpperInvariant(), r.ChangeTypeText));
    }

    [Fact]
    public async Task Search_by_id_name_partial_case_insensitive_and_no_results()
    {
        var vm = await DemoVm();
        vm.SearchText = "w-104"; Assert.Equal("W-104", Assert.Single(vm.Rows).ElementId);
        vm.SearchText = "Parede A"; Assert.Contains(vm.Rows, r => r.ElementId == "W-104");
        vm.SearchText = "FUR-"; Assert.All(vm.Rows, r => Assert.StartsWith("FUR-", r.ElementId)); Assert.Equal(7, vm.Rows.Count);
        vm.SearchText = "  w-10  "; Assert.NotEmpty(vm.Rows);                              // espaços ignorados
        vm.SearchText = "zzz-inexistente"; Assert.Empty(vm.Rows);
        Assert.Equal("0 de 41 alterações", vm.RowCountText);
    }

    [Fact]
    public async Task Category_and_impact_filters_and_their_combination()
    {
        var vm = await DemoVm();
        vm.SelectedCategory = "Parede"; Assert.Equal(10, vm.Rows.Count); Assert.All(vm.Rows, r => Assert.Equal("Parede", r.Category));
        vm.SelectedCategory = "Todos";
        vm.SelectedImpact = "Crítico"; Assert.Equal(5, vm.Rows.Count); Assert.All(vm.Rows, r => Assert.Equal("CRÍTICO", r.ImpactText));
        vm.SelectedImpact = "Todos";

        vm.SelectedChangeType = "Removido"; vm.SelectedImpact = "Alto"; vm.SelectedCategory = "Parede";
        // paredes removidas: W-110, W-118 (HIGH)
        Assert.Equal(new[] { "W-110", "W-118" }, vm.Rows.Select(r => r.ElementId).OrderBy(x => x));
        Assert.Equal("2 de 41 alterações", vm.RowCountText);

        vm.SearchText = "W-118"; Assert.Equal("W-118", Assert.Single(vm.Rows).ElementId);
    }

    [Fact]
    public async Task Clear_filters_restores_all_changes()
    {
        var vm = await DemoVm();
        vm.SelectedChangeType = "Removido"; vm.SelectedImpact = "Crítico"; vm.SelectedCategory = "Pilar"; vm.SearchText = "x";
        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(("Todos", "Todos", "Todos", ""), (vm.SelectedChangeType, vm.SelectedImpact, vm.SelectedCategory, vm.SearchText));
        Assert.Equal(41, vm.Rows.Count);
    }

    [Fact]
    public async Task Null_combo_selection_is_treated_as_all()
    {
        var vm = await DemoVm();
        vm.SelectedCategory = null!; vm.SelectedImpact = null!; vm.SelectedChangeType = null!;   // ComboBox pode zerar ao trocar ItemsSource
        Assert.Equal(41, vm.Rows.Count);
    }

    [Fact]
    public async Task Details_for_modified_added_and_removed_rows()
    {
        var vm = await DemoVm();
        vm.SelectedRow = vm.Rows.Single(r => r.ElementId == "W-104");
        Assert.True(vm.HasSelection);
        Assert.Equal(new[] { "Altura", "Área", "Comprimento", "Material", "Nível", "Volume" }.Where(n => n != "Altura"),
                     vm.PropertyRows.Select(p => p.Name).Where(n => n != "Altura").OrderBy(x => x));
        var length = vm.PropertyRows.Single(p => p.Name == "Comprimento");
        Assert.Equal(("3.2", "4.8"), (length.Old, length.New));
        Assert.True(vm.HasQuantity); Assert.Contains("4,48", vm.QuantityText);
        Assert.Contains("•", vm.ImpactReasonsText);

        var added = vm.Rows.First(r => r.ChangeTypeText == "ADICIONADO");
        vm.SelectedRow = added;
        Assert.Empty(vm.PropertyRows); Assert.False(vm.HasQuantity); Assert.Null(vm.SelectedProperty);
        Assert.False(string.IsNullOrEmpty(vm.ImpactReasonsText));

        vm.SelectedRow = vm.Rows.First(r => r.ChangeTypeText == "REMOVIDO");
        Assert.Empty(vm.PropertyRows); Assert.False(vm.HasQuantity);

        vm.SelectedRow = null; Assert.True(vm.HasNoSelection);
    }

    [Fact]
    public async Task Details_show_a_missing_value_as_dash_for_added_parameter()
    {
        var vm = await DemoVm();
        vm.SelectedRow = vm.Rows.Single(r => r.ElementId == "B-104");   // StructuralUsage Girder→Secondary + material
        Assert.Contains(vm.PropertyRows, p => p.Name.StartsWith("Uso estrutural") && p.Old != "—" && p.New != "—");
    }

    [Fact]
    public async Task Identical_files_give_zero_changes_and_no_false_positives()
    {
        var vm = NewVm();
        var f = SamplePath("project-v1.json");
        _dialogs.Open.Enqueue(f); _dialogs.Open.Enqueue(f);
        await vm.SelectPreviousCommand.ExecuteAsync(null); await vm.SelectCurrentCommand.ExecuteAsync(null);
        Assert.True(vm.CanCompare);
        vm.CompareCommand.Execute(null);
        Assert.Equal(110, vm.Cards.Single(c => c.Label == "Inalterados").Value);
        Assert.Equal(0, vm.Cards.Single(c => c.Label == "Total de alterações").Value);
        Assert.Empty(vm.Rows); Assert.Equal("0 de 0 alterações", vm.RowCountText);
    }

    [Fact]
    public async Task Invalid_file_shows_friendly_error_and_keeps_previous_state()
    {
        var vm = await DemoVm();
        var bad = Path.Combine(_dir, "ruim.json"); await File.WriteAllTextAsync(bad, "{ quebrado");
        _dialogs.Open.Enqueue(bad);
        await vm.SelectCurrentCommand.ExecuteAsync(null);
        Assert.True(vm.HasError); Assert.Contains("ruim.json", vm.ErrorMessage);
        Assert.Equal("Edifício Alpha", vm.CurrentSnapshot!.ProjectName);   // snapshot anterior preservado
        Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task Cancelling_the_file_dialog_changes_nothing()
    {
        var vm = NewVm(); _dialogs.Open.Enqueue(null);
        await vm.SelectPreviousCommand.ExecuteAsync(null);
        Assert.Null(vm.PreviousSnapshot); Assert.False(vm.HasError); Assert.False(vm.CanCompare);
    }

    [Fact]
    public async Task Export_commands_write_files_and_ignore_cancel()
    {
        var vm = await DemoVm();
        var csv = Path.Combine(_dir, "o.csv"); var html = Path.Combine(_dir, "o.html");
        _dialogs.Save.Enqueue(csv); await vm.ExportCsvCommand.ExecuteAsync(null);
        _dialogs.Save.Enqueue(html); await vm.ExportHtmlCommand.ExecuteAsync(null);
        Assert.True(File.Exists(csv)); Assert.True(File.Exists(html));
        _dialogs.Save.Enqueue(null); await vm.ExportCsvCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.False(NewVm().ExportCsvCommand.CanExecute(null));   // sem resultado não exporta
    }

    [Fact]
    public async Task Export_to_unwritable_path_reports_error_instead_of_crashing()
    {
        var vm = await DemoVm();
        _dialogs.Save.Enqueue(Path.Combine(_dir, "pasta-que-nao-existe", "x.csv"));
        await vm.ExportCsvCommand.ExecuteAsync(null);
        Assert.True(vm.HasError); Assert.Contains("Falha ao exportar", vm.ErrorMessage);
    }

    [Fact]
    public async Task History_records_each_analysis_with_correct_numbers_and_distinguishes_them()
    {
        var vm = await DemoVm();
        Assert.Single(vm.History);
        var f1 = SamplePath("project-v1.json");
        _dialogs.Open.Enqueue(f1); _dialogs.Open.Enqueue(f1);
        await vm.SelectPreviousCommand.ExecuteAsync(null); await vm.SelectCurrentCommand.ExecuteAsync(null);
        vm.CompareCommand.Execute(null);
        Assert.Equal(2, vm.History.Count);
        Assert.Equal(0, vm.History[0].Record.Modified);               // mais recente primeiro (v1 x v1)
        var demo = vm.History[1].Record;
        Assert.Equal((122, 12, 8, 21, 81, 20), (demo.Total, demo.Added, demo.Removed, demo.Modified, demo.Unchanged, demo.HighImpact));
        Assert.NotEqual(vm.History[0].Title, vm.History[1].Title);
        Assert.Matches(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}:\d{2}$", vm.History[0].When);
    }

    [Fact]
    public async Task History_survives_restart_and_can_be_reopened()
    {
        var vm1 = await DemoVm();
        var when = vm1.History[0].Record.AnalyzedAt;

        var vm2 = NewVm();                                          // "reabrir o aplicativo"
        Assert.Single(vm2.History);
        Assert.Null(vm2.History[0].Result);                         // vem do disco: resumo apenas
        Assert.Equal(vm1.History[0].Record, vm2.History[0].Record);
        Assert.False(vm2.HasResult);

        await vm2.OpenHistoryEntryAsync(vm2.History[0]);            // recarrega arquivos e recompara
        Assert.True(vm2.HasResult); Assert.Null(vm2.ErrorMessage);
        Assert.Equal(21, vm2.Result!.Modified);
        Assert.Equal(when, vm2.Result.AnalyzedAt);
        Assert.Equal(41, vm2.Rows.Count);
    }

    [Fact]
    public async Task Reopening_history_whose_files_vanished_shows_error()
    {
        var a = Path.Combine(_dir, "a.json"); var b = Path.Combine(_dir, "b.json");
        File.Copy(SamplePath("project-v1.json"), a); File.Copy(SamplePath("project-v2.json"), b);
        var vm1 = NewVm(); _dialogs.Open.Enqueue(a); _dialogs.Open.Enqueue(b);
        await vm1.SelectPreviousCommand.ExecuteAsync(null); await vm1.SelectCurrentCommand.ExecuteAsync(null); vm1.CompareCommand.Execute(null);
        File.Delete(a);
        var vm2 = NewVm();
        await vm2.OpenHistoryEntryAsync(vm2.History[0]);
        Assert.True(vm2.HasError); Assert.Contains("Não foi possível reabrir", vm2.ErrorMessage);
        Assert.False(vm2.HasResult);
    }

    [Fact]
    public async Task Second_comparison_resets_filters_and_replaces_results()
    {
        var vm = await DemoVm();
        vm.SelectedChangeType = "Removido"; vm.SearchText = "W";
        await vm.LoadDemoCommand.ExecuteAsync(null);
        Assert.Equal(("Todos", ""), (vm.SelectedChangeType, vm.SearchText));
        Assert.Equal(41, vm.Rows.Count);
        Assert.Equal(2, vm.History.Count);
    }
}

public class ViewModelWarningTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("radar-vmw-").FullName;
    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public async Task Different_project_names_keep_the_warning_after_comparison_finishes()
    {
        var other = Path.Combine(_dir, "o.json");
        await File.WriteAllTextAsync(other, """{"projectName":"Outro Projeto","version":"1","elements":[{"id":"A","name":"x","category":"Wall"}]}""");
        var d = new FakeDialogs(); d.Open.Enqueue(SamplePath("project-v1.json")); d.Open.Enqueue(other);
        var vm = new MainViewModel(new JsonSnapshotLoader(), new SnapshotComparer(), new HistoryStore(Path.Combine(_dir, "h.json")));
        vm.AttachDialogs(d);
        await vm.SelectPreviousCommand.ExecuteAsync(null); await vm.SelectCurrentCommand.ExecuteAsync(null);
        vm.CompareCommand.Execute(null);
        Assert.Contains("projetos com nomes diferentes", vm.StatusMessage);
        Assert.Contains("Comparação concluída", vm.StatusMessage);
    }
}
