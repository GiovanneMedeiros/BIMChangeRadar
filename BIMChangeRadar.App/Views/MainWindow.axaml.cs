using Avalonia.Controls;
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
