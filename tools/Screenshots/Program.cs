// Ferramenta de desenvolvimento: renderiza a janela real do app em modo headless e salva PNGs (docs/screenshots).
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using BIMChangeRadar.App;
using BIMChangeRadar.App.ViewModels;
using BIMChangeRadar.App.Views;

public class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.Error.WriteLine("start"); var outDir = args.Length > 0 ? args[0] : "docs/screenshots";
        Directory.CreateDirectory(outDir);
        try { using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
        var done = new TaskCompletionSource();
        Func<Task> work = async () =>
        { try {
            var win = new MainWindow { Width = 1500, Height = 880 };
            var vm = (MainViewModel)win.DataContext!;
            win.Show();
            async Task Shot(string name)
            {
                for (int i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(60); }
                using var frame = win.CaptureRenderedFrame();
                frame?.Save(Path.Combine(outDir, name));
                Console.WriteLine("saved " + name);
            }
            await Shot("01-empty.png");
            var t = vm.LoadDemoCommand.ExecuteAsync(null);
            for (int i = 0; i < 400 && vm.Result is null && vm.ErrorMessage is null; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); }
            await Shot("02-dashboard.png");
            vm.ShowChanges = true;
            await Shot("03-changes.png");
            vm.SelectedRow = vm.Rows.FirstOrDefault(r => r.ElementId == "W-104") ?? vm.Rows.First();
            await Shot("04-detail.png");
            Console.WriteLine($"rows={vm.Rows.Count} err={vm.ErrorMessage}");
            done.TrySetResult(); } catch (Exception e) { Console.Error.WriteLine(e); done.TrySetResult(); } };
        _ = session.Dispatch(work, CancellationToken.None); await done.Task.WaitAsync(TimeSpan.FromSeconds(90)); } catch (Exception ex) { Console.Error.WriteLine(ex); }
    }
}
