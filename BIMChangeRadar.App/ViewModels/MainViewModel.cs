using System.Collections.ObjectModel;
using BIMChangeRadar.Core.Comparison;
using BIMChangeRadar.Core.Interfaces;
using BIMChangeRadar.Core.Models;
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

    public MainViewModel() : this(new JsonSnapshotLoader(), new SnapshotComparer()) { }
    public MainViewModel(ISnapshotLoader loader, ISnapshotComparer comparer)
    {
        _loader = loader;
        _comparer = comparer;
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
    public string[] ChangeTypeOptions { get; } = { "All", "Added", "Removed", "Modified", "Unchanged" };
    public string[] ImpactOptions { get; } = { "All", "Critical", "High", "Medium", "Low" };
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedChangeType = "All";
    [ObservableProperty] private string _selectedImpact = "All";
    [ObservableProperty] private string _selectedCategory = "All";
    [ObservableProperty] private ObservableCollection<string> _categoryOptions = new() { "All" };

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

    partial void OnShowOverviewChanged(bool value) { }
    partial void OnSelectedHistoryChanged(HistoryEntry? value)
    {
        if (value is not null && !ReferenceEquals(value.Result, Result)) { Result = value.Result; PreviousSnapshot = value.Result.Previous; CurrentSnapshot = value.Result.Current; Refresh(); }
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
            if (isPrevious) { PreviousSnapshot = snap; PreviousPath = $"{Path.GetFileName(path)} · {snap.Label} · {snap.Elements.Count} elementos"; }
            else { CurrentSnapshot = snap; CurrentPath = $"{Path.GetFileName(path)} · {snap.Label} · {snap.Elements.Count} elementos"; }
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
            if (!string.Equals(PreviousSnapshot.ProjectName, CurrentSnapshot.ProjectName, StringComparison.OrdinalIgnoreCase))
                StatusMessage = "Atenção: os snapshots pertencem a projetos com nomes diferentes.";
            var result = _comparer.Compare(PreviousSnapshot, CurrentSnapshot);
            Result = result;
            History.Insert(0, new HistoryEntry { Result = result });
            Refresh();
            StatusMessage = $"Comparação concluída: {result.TotalChanges} alterações em {result.TotalElements} elementos.";
            ShowOverview = true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; StatusMessage = "Falha na comparação."; }
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
            new("Adicionados", r.Added, Palette.Added, "novos na versão atual"),
            new("Removidos", r.Removed, Palette.Removed, "ausentes na versão atual"),
            new("Modificados", r.Modified, Palette.Brush("#E08A00"), "propriedades alteradas"),
            new("Inalterados", r.Unchanged, Palette.Brush("#7A8AAE"), "sem diferenças"),
            new("Total de alterações", r.TotalChanges, Palette.Brush("#0F1F3D"), "add + rem + mod"),
            new("Alto impacto", r.HighImpact, Palette.Brush("#C62828"), "HIGH ou CRITICAL"),
        };
        var max = Math.Max(1, new[] { ImpactLevel.Critical, ImpactLevel.High, ImpactLevel.Medium, ImpactLevel.Low }.Max(l => r.CountByImpact(l)));
        ImpactBars = new(new[] { ImpactLevel.Critical, ImpactLevel.High, ImpactLevel.Medium, ImpactLevel.Low }
            .Select(l => new BarItem(l.ToString().ToUpperInvariant(), r.CountByImpact(l), (double)r.CountByImpact(l) / max, Palette.Solid(l))));

        var byCat = r.ActualChanges.GroupBy(c => c.Category).Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Count).ToList();
        var maxCat = Math.Max(1, byCat.Count == 0 ? 1 : byCat.Max(x => x.Count));
        CategoryBars = new(byCat.Select(x => new BarItem(x.Key, x.Count, (double)x.Count / maxCat, Palette.Brush("#2563EB"))));

        CategoryOptions = new(new[] { "All" }.Concat(r.Changes.Select(c => c.Category).Distinct().OrderBy(c => c)));
        SelectedCategory = "All"; SelectedChangeType = "All"; SelectedImpact = "All"; SearchText = "";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (Result is null) { Rows = new(); RowCountText = ""; return; }
        IEnumerable<ElementChange> q = Result.Changes;

        // "All" mostra apenas alterações reais; Unchanged precisa ser pedido explicitamente.
        q = SelectedChangeType == "All" ? q.Where(c => c.ChangeType != ChangeType.Unchanged)
                                        : q.Where(c => c.ChangeType.ToString() == SelectedChangeType);
        if (SelectedImpact != "All") q = q.Where(c => c.Impact.ToString() == SelectedImpact);
        if (SelectedCategory != "All") q = q.Where(c => c.Category == SelectedCategory);
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
    private void ClearFilters() { SearchText = ""; SelectedChangeType = "All"; SelectedImpact = "All"; SelectedCategory = "All"; }

    [RelayCommand] private void GoToChanges() => ShowChanges = true;
}
