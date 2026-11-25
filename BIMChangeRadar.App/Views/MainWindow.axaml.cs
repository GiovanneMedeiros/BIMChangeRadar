using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using BIMChangeRadar.App.ViewModels;

namespace BIMChangeRadar.App.Views;

public partial class MainWindow : Window, IDialogService
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        vm.AttachDialogs(this);
        DataContext = vm;
        PropertyChanged += (_, e) => { if (e.Property == ClientSizeProperty) ApplyResponsiveLayout(ClientSize.Width); };
        Opened += (_, _) => ApplyResponsiveLayout(ClientSize.Width);
    }

    private bool? _narrow;

    /// <summary>Em janelas estreitas o painel de detalhes passa para baixo da tabela (evita colunas espremidas/cortadas).</summary>
    private void ApplyResponsiveLayout(double width)
    {
        TitleBlock.IsVisible = ClientSize.Height >= 780;   // a marca já aparece na barra lateral; ganha espaço vertical
        var narrow = width < 1360;
        if (_narrow == narrow) return;
        _narrow = narrow;
        if (narrow)
        {
            ChangesLayout.ColumnDefinitions = new ColumnDefinitions("*");
            ChangesLayout.RowDefinitions = new RowDefinitions("Auto,*,Auto");
            Grid.SetColumnSpan(FilterCard, 1);
            Grid.SetRow(TablePanel, 1); Grid.SetColumn(TablePanel, 0); TablePanel.Margin = new Avalonia.Thickness(0, 0, 0, 12);
            Grid.SetRow(DetailPanel, 2); Grid.SetColumn(DetailPanel, 0); DetailPanel.Height = 210;
        }
        else
        {
            ChangesLayout.ColumnDefinitions = new ColumnDefinitions("*,360");
            ChangesLayout.RowDefinitions = new RowDefinitions("Auto,*");
            Grid.SetColumnSpan(FilterCard, 2);
            Grid.SetRow(TablePanel, 1); Grid.SetColumn(TablePanel, 0); TablePanel.Margin = new Avalonia.Thickness(0, 0, 12, 0);
            Grid.SetRow(DetailPanel, 1); Grid.SetColumn(DetailPanel, 1); DetailPanel.Height = double.NaN;
        }
    }

    public async Task<string?> PickJsonAsync(string title)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Snapshot BIM (JSON)") { Patterns = new[] { "*.json" } } }
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveAsync(string title, string suggestedName, string extension)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = new[] { "*." + extension } } }
        });
        return file?.TryGetLocalPath();
    }
}
