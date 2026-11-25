using System.Collections.ObjectModel;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
using BIMChangeRadar.Core.Services;
using BIMChangeRadar.Infrastructure.Export;
using BIMChangeRadar.Infrastructure.FileSystem;
using BIMChangeRadar.Infrastructure.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BIMChangeRadar.App.ViewModels;

/// <summary>Abstrai diálogos de arquivo para manter o ViewModel testável e livre de UI.</summary>
public interface IDialogService
{
    Task<string?> PickJsonAsync(string title);
    Task<string?> PickSaveAsync(string title, string suggestedName, string extension);
}

public partial class MainViewModel : ObservableObject
{
    private readonly ISnapshotLoader _loader;
    private readonly ISnapshotComparer _comparer;
    private readonly IReportExporter _csv = new CsvReportExporter();
    private readonly IReportExporter _html = new HtmlReportExporter();
    private IDialogService _dialogs = null!;

    private readonly HistoryStore _historyStore;
    private string? _previousFile, _currentFile;

    public MainViewModel() : this(new JsonSnapshotLoader(), new SnapshotComparer(), new HistoryStore()) { }
    public MainViewModel(ISnapshotLoader loader, ISnapshotComparer comparer, HistoryStore historyStore)
    {
        _loader = loader;
        _comparer = comparer;
        _historyStore = historyStore;
        foreach (var r in _historyStore.Load()) History.Add(new HistoryEntry { Record = r });
    }

    public void AttachDialogs(IDialogService dialogs) => _dialogs = dialogs;

    // ----- estado -----
    [ObservableProperty] private string _previousPath = "Nenhum arquivo selecionado";
    [ObservableProperty] private string _currentPath = "Nenhum arquivo selecionado";
    [ObservableProperty] private ModelSnapshot? _previousSnapshot;
    [ObservableProperty] private ModelSnapshot? _currentSnapshot;
    [ObservableProperty] private ComparisonResult? _result;
    [ObservableProperty] private string _statusMessage = "Carregue dois snapshots JSON ou use o projeto de demonstração.";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    // ----- navegação -----
    [ObservableProperty] private bool _showOverview = true;
    [ObservableProperty] private bool _showChanges;
    [ObservableProperty] private bool _showHistory;

    // ----- filtros -----
    public string[] ChangeTypeOptions { get; } = { "Todos", "Adicionado", "Removido", "Modificado", "Inalterado" };
    public string[] ImpactOptions { get; } = { "Todos", "Crítico", "Alto", "Médio", "Baixo" };
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedChangeType = "Todos";
    [ObservableProperty] private string _selectedImpact = "Todos";
    [ObservableProperty] private string _selectedCategory = "Todos";
    [ObservableProperty] private ObservableCollection<string> _categoryOptions = new() { "Todos" };

    // ----- resultados -----
    [ObservableProperty] private ObservableCollection<ChangeRow> _rows = new();
    [ObservableProperty] private ChangeRow? _selectedRow;
    [ObservableProperty] private PropertyRow? _selectedProperty;
    [ObservableProperty] private ObservableCollection<PropertyRow> _propertyRows = new();
    [ObservableProperty] private ObservableCollection<StatCard> _cards = new();
    [ObservableProperty] private ObservableCollection<BarItem> _impactBars = new();
    [ObservableProperty] private ObservableCollection<BarItem> _categoryBars = new();
    [ObservableProperty] private string _rowCountText = "";
    public ObservableCollection<HistoryEntry> History { get; } = new();
    [ObservableProperty] private HistoryEntry? _selectedHistory;

    public bool HasResult => Result is not null;
    public bool HasNoResult => Result is null;
    public bool CanCompare => PreviousSnapshot is not null && CurrentSnapshot is not null && !IsBusy;
    public bool HasSelection => SelectedRow is not null;
    public bool HasNoSelection => SelectedRow is null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasQuantity => SelectedRow?.Change.Quantity is not null;
    public string QuantityText => SelectedRow?.Change.Quantity?.Message ?? "";
    public string ImpactReasonsText => SelectedRow is null ? "" : string.Join("\n", SelectedRow.Change.ImpactReasons.Select(r => "• " + r));
    public string AnalysisInfo => Result is null ? "" :
        $"{Result.Current.ProjectName} · {Result.Previous.Label} ({Result.Previous.Date:dd/MM/yyyy}) → {Result.Current.Label} ({Result.Current.Date:dd/MM/yyyy}) · analisado em {Result.AnalyzedAt:dd/MM/yyyy HH:mm}";

    partial void OnResultChanged(ComparisonResult? value)
    {
        OnPropertyChanged(nameof(HasResult)); OnPropertyChanged(nameof(HasNoResult)); OnPropertyChanged(nameof(AnalysisInfo));
        ExportCsvCommand.NotifyCanExecuteChanged(); ExportHtmlCommand.NotifyCanExecuteChanged();
    }
    partial void OnPreviousSnapshotChanged(ModelSnapshot? value) { OnPropertyChanged(nameof(CanCompare)); CompareCommand.NotifyCanExecuteChanged(); }
    partial void OnCurrentSnapshotChanged(ModelSnapshot? value) { OnPropertyChanged(nameof(CanCompare)); CompareCommand.NotifyCanExecuteChanged(); }
    partial void OnIsBusyChanged(bool value) { OnPropertyChanged(nameof(CanCompare)); CompareCommand.NotifyCanExecuteChanged(); }
    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedChangeTypeChanged(string value) => ApplyFilters();
    partial void OnSelectedImpactChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    partial void OnSelectedRowChanged(ChangeRow? value)
    {
        PropertyRows = new(value?.Change.PropertyChanges.Select(p => new PropertyRow(p)) ?? Enumerable.Empty<PropertyRow>());
        SelectedProperty = PropertyRows.FirstOrDefault();
        foreach (var n in new[] { nameof(HasSelection), nameof(HasNoSelection), nameof(HasQuantity), nameof(QuantityText), nameof(ImpactReasonsText) })
            OnPropertyChanged(n);
    }

    partial void OnSelectedHistoryChanged(HistoryEntry? value)
    {
        if (value is not null) _ = OpenHistoryEntryAsync(value);
    }

    /// <summary>Abre uma análise do histórico; se veio do disco, recarrega os arquivos e refaz a comparação.</summary>
    public async Task OpenHistoryEntryAsync(HistoryEntry entry)
    {
        try
        {
            ErrorMessage = null;
            if (entry.Result is null)
            {
                IsBusy = true;
                var a = await _loader.LoadAsync(entry.Record.PreviousPath);
                var b = await _loader.LoadAsync(entry.Record.CurrentPath);
                entry.Result = _comparer.Compare(a, b, entry.Record.AnalyzedAt);
            }
            if (ReferenceEquals(entry.Result, Result)) return;
            Result = entry.Result;
            PreviousSnapshot = entry.Result.Previous; CurrentSnapshot = entry.Result.Current;
            PreviousPath = $"{Path.GetFileName(entry.Record.PreviousPath)} · {entry.Result.Previous.Label}";
            CurrentPath = $"{Path.GetFileName(entry.Record.CurrentPath)} · {entry.Result.Current.Label}";
            Refresh();
            StatusMessage = $"Análise de {entry.When} aberta a partir do histórico.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Não foi possível reabrir esta análise: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    // ----- comandos -----
    [RelayCommand]
    private async Task SelectPreviousAsync()
    {
        var path = await _dialogs.PickJsonAsync("Selecionar versão anterior");
        if (path is null) return;
        await LoadSnapshotAsync(path, isPrevious: true);
    }

    [RelayCommand]
    private async Task SelectCurrentAsync()
    {
        var path = await _dialogs.PickJsonAsync("Selecionar versão atual");
        if (path is null) return;
        await LoadSnapshotAsync(path, isPrevious: false);
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            var (a, b) = SampleLocator.FindDemoFiles();
            await LoadSnapshotAsync(a, true);
            await LoadSnapshotAsync(b, false);
            if (CanCompare) Compare();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    private async Task LoadSnapshotAsync(string path, bool isPrevious)
    {
        IsBusy = true; ErrorMessage = null;
        try
        {
            var snap = await _loader.LoadAsync(path);
            if (isPrevious) { _previousFile = path; PreviousSnapshot = snap; PreviousPath = $"{Path.GetFileName(path)} · {snap.Label} · {snap.Elements.Count} elementos"; }
            else { _currentFile = path; CurrentSnapshot = snap; CurrentPath = $"{Path.GetFileName(path)} · {snap.Label} · {snap.Elements.Count} elementos"; }
            StatusMessage = $"Snapshot carregado: {snap.Label} ({snap.Elements.Count} elementos).";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; StatusMessage = "Falha ao carregar o arquivo."; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    private void Compare()
    {
        if (PreviousSnapshot is null || CurrentSnapshot is null) return;
        ErrorMessage = null;
        try
        {
            var sameProject = string.Equals(PreviousSnapshot.ProjectName, CurrentSnapshot.ProjectName, StringComparison.OrdinalIgnoreCase);
            var result = _comparer.Compare(PreviousSnapshot, CurrentSnapshot);
            Result = result;
            RecordHistory(result);
            Refresh();
            StatusMessage = $"Comparação concluída: {result.TotalChanges} alterações em {result.TotalElements} elementos."
                + (sameProject ? "" : " Atenção: os snapshots pertencem a projetos com nomes diferentes.");
            ShowOverview = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; StatusMessage = "Falha na comparação."; }
    }

    private void RecordHistory(ComparisonResult r)
    {
        var rec = new HistoryRecord(r.Current.ProjectName, r.Previous.Version, r.Current.Version,
            _previousFile ?? "", _currentFile ?? "", r.AnalyzedAt,
            r.TotalElements, r.Added, r.Removed, r.Modified, r.Unchanged, r.HighImpact);
        History.Insert(0, new HistoryEntry { Record = rec, Result = r });
        while (History.Count > HistoryStore.MaxEntries) History.RemoveAt(History.Count - 1);
        _historyStore.Save(History.Select(h => h.Record));
    }

    private bool CanExport() => Result is not null;

    [RelayCommand(CanExecute = nameof(CanExport))] private Task ExportCsvAsync() => ExportAsync(_csv);
    [RelayCommand(CanExecute = nameof(CanExport))] private Task ExportHtmlAsync() => ExportAsync(_html);

    private async Task ExportAsync(IReportExporter exporter)
    {
        if (Result is null) return;
        var name = $"BIMChangeRadar_{Result.Current.ProjectName.Replace(' ', '_')}_v{Result.Previous.Version}-v{Result.Current.Version}";
        var path = await _dialogs.PickSaveAsync($"Exportar relatório {exporter.Name}", name, exporter.FileExtension);
        if (path is null) return;
        try
        {
            await exporter.ExportAsync(Result, path);
            StatusMessage = $"Relatório {exporter.Name} salvo em {path}";
            ErrorMessage = null;
        }
        catch (Exception ex) { ErrorMessage = $"Falha ao exportar: {ex.Message}"; }
    }

    // ----- derivação dos números exibidos (tudo vem do resultado real) -----
    private void Refresh()
    {
        if (Result is null) return;
        var r = Result;
        Cards = new()
        {
            new("Total de elementos", r.TotalElements, Palette.Brush("#2563EB"), "analisados"),
            new("Adicionados", r.Added, Palette.Added, "novos"),
            new("Removidos", r.Removed, Palette.Removed, "ausentes"),
            new("Modificados", r.Modified, Palette.Brush("#E08A00"), "alterados"),
            new("Inalterados", r.Unchanged, Palette.Brush("#7A8AAE"), "sem diferenças"),
            new("Total de alterações", r.TotalChanges, Palette.Brush("#0F1F3D"), "adic. + rem. + modif."),
            new("Alto impacto", r.HighImpact, Palette.Brush("#C62828"), "alto ou crítico"),
        };
        var max = Math.Max(1, new[] { ImpactLevel.Critical, ImpactLevel.High, ImpactLevel.Medium, ImpactLevel.Low }.Max(l => r.CountByImpact(l)));
        ImpactBars = new(new[] { ImpactLevel.Critical, ImpactLevel.High, ImpactLevel.Medium, ImpactLevel.Low }
            .Select(l => new BarItem(Pt.Impact(l), r.CountByImpact(l), (double)r.CountByImpact(l) / max, Palette.Solid(l))));

        var byCat = r.ActualChanges.GroupBy(c => c.Category).Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Count).ToList();
        var maxCat = Math.Max(1, byCat.Count == 0 ? 1 : byCat.Max(x => x.Count));
        CategoryBars = new(byCat.Select(x => new BarItem(Pt.Category(x.Key), x.Count, (double)x.Count / maxCat, Palette.Brush("#2563EB"))));

        CategoryOptions = new(new[] { "Todos" }.Concat(r.Changes.Select(c => Pt.Category(c.Category)).Distinct().OrderBy(c => c)));
        SelectedCategory = "Todos"; SelectedChangeType = "Todos"; SelectedImpact = "Todos"; SearchText = "";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (Result is null) { Rows = new(); RowCountText = ""; return; }
        IEnumerable<ElementChange> q = Result.Changes;
        // ComboBox pode zerar a seleção ao trocar a lista de opções; null equivale a "Todos".
        var typeFilter = string.IsNullOrEmpty(SelectedChangeType) ? "Todos" : SelectedChangeType;
        var impactFilter = string.IsNullOrEmpty(SelectedImpact) ? "Todos" : SelectedImpact;
        var categoryFilter = string.IsNullOrEmpty(SelectedCategory) ? "Todos" : SelectedCategory;

        // "All" mostra apenas alterações reais; Unchanged precisa ser pedido explicitamente.
        q = typeFilter == "Todos" ? q.Where(c => c.ChangeType != ChangeType.Unchanged)
                                        : q.Where(c => Pt.ChangeType(c.ChangeType).Equals(typeFilter, StringComparison.OrdinalIgnoreCase));
        if (impactFilter != "Todos") q = q.Where(c => Pt.Impact(c.Impact).Equals(impactFilter, StringComparison.OrdinalIgnoreCase));
        if (categoryFilter != "Todos") q = q.Where(c => Pt.Category(c.Category) == categoryFilter);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(c => c.Name.Contains(s, StringComparison.OrdinalIgnoreCase) || c.ElementId.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        var list = q.Select(c => new ChangeRow(c)).ToList();
        Rows = new(list);
        RowCountText = $"{list.Count} de {Result.TotalChanges} alterações";
        SelectedRow = null;
    }

    [RelayCommand]
    private void ClearFilters() { SearchText = ""; SelectedChangeType = "Todos"; SelectedImpact = "Todos"; SelectedCategory = "Todos"; }

    [RelayCommand] private void GoToChanges() => ShowChanges = true;
}
