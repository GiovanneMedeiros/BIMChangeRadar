// Ferramenta de desenvolvimento/QA: executa a janela REAL do app em modo headless (Avalonia.Headless + Skia),
// dirige a UI com cliques simulados, mede tempos e salva screenshots (docs/screenshots).
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    static int failures;
    static void Check(bool ok, string what) { Console.WriteLine($"[{(ok ? "OK " : "FALHA")}] {what}"); if (!ok) failures++; }

    static void Pump(int ms = 150) { for (int i = 0; i < ms / 25 + 1; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } }
    static void PumpUntil(Func<bool> cond, int timeoutMs = 20000)
    {
        var sw = Stopwatch.StartNew();
        while (!cond() && sw.ElapsedMilliseconds < timeoutMs) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    }

    static Control? FindByText(Window w, string text) =>
        w.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == text && t.IsEffectivelyVisible)
            ?.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => c is Button or RadioButton);

    static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseMove(p); w.MouseDown(p, MouseButton.Left); Pump(40); w.MouseUp(p, MouseButton.Left); Pump(120);
    }

    static void Shot(Window w, string dir, string name)
    {
        Pump(200);
        using var frame = w.CaptureRenderedFrame();
        frame?.Save(Path.Combine(dir, name));
    }

    public static async Task<int> Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "docs/screenshots";
        Directory.CreateDirectory(outDir);
        var histDir = Directory.CreateTempSubdirectory("radar-ui-").FullName;
        Environment.SetEnvironmentVariable("BIMCHANGERADAR_DATA_DIR", histDir);
        using var session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
        var done = new TaskCompletionSource();
        Func<Task> work = () =>
        {
            try
            {
                var win = new MainWindow { Width = 1500, Height = 880 };
                var vm = (MainViewModel)win.DataContext!;
                win.Show(); Pump();

                // ---- cliques reais ----
                Shot(win, outDir, "01-empty.png");
                Check(vm.HasNoResult, "estado inicial: sem resultado");
                Click(win, FindByText(win, "Carregar projeto demo")!);
                PumpUntil(() => vm.Result is not null);
                Check(vm.Result is { TotalChanges: 41 }, "clique em 'Carregar projeto demo' compara e gera 41 alterações");
                Shot(win, outDir, "02-dashboard.png");

                Click(win, FindByText(win, "Alterações")!);
                Check(vm.ShowChanges && !vm.ShowOverview, "navegação: Dashboard → Alterações");
                Shot(win, outDir, "03-changes.png");

                vm.SelectedRow = vm.Rows.First(r => r.ElementId == "W-104");
                Pump(); Shot(win, outDir, "04-detail.png");

                Click(win, FindByText(win, "Histórico")!);
                Check(vm.ShowHistory && !vm.ShowChanges, "navegação: Alterações → Histórico");
                Check(vm.History.Count == 1, "histórico contém a análise");
                Shot(win, outDir, "05-history.png");

                Click(win, FindByText(win, "Dashboard")!);
                Check(vm.ShowOverview && !vm.ShowHistory, "navegação: Histórico → Dashboard");
                Check(vm.History.Count == 1 && vm.Result is not null, "histórico e resultado permanecem após navegar");

                // ---- tamanhos de janela ----
                foreach (var (w, h) in new[] { (1100, 680), (1280, 720), (1920, 1080) })
                {
                    win.Width = w; win.Height = h; Pump(300);
                    vm.ShowOverview = true; Pump(); Shot(win, outDir, $"size-{w}x{h}-dashboard.png");
                    vm.ShowChanges = true; vm.SelectedRow = vm.Rows.First(r => r.ElementId == "W-104"); Pump(); Shot(win, outDir, $"size-{w}x{h}-changes.png");
                    vm.ShowHistory = true; Pump(); Shot(win, outDir, $"size-{w}x{h}-history.png");
                    Console.WriteLine($"   capturado {w}x{h}");
                }
                win.Width = 1500; win.Height = 880; Pump(300);

                // ---- UI com 10.000 elementos (tempo de render e interação) ----
                var big = Path.Combine(histDir, "big"); Directory.CreateDirectory(big);
                var sb1 = new System.Text.StringBuilder("{\"projectName\":\"Grande\",\"version\":\"1\",\"elements\":[");
                var sb2 = new System.Text.StringBuilder("{\"projectName\":\"Grande\",\"version\":\"2\",\"elements\":[");
                string[] cats = { "Wall", "Door", "Window", "Column", "Beam", "Pipe", "Furniture", "Floor", "Room" };
                for (int i = 0; i < 10000; i++)
                {
                    if (i > 0) { sb1.Append(','); sb2.Append(','); }
                    sb1.Append($"{{\"id\":\"E{i}\",\"uniqueId\":\"u{i}\",\"name\":\"Elemento {i}\",\"category\":\"{cats[i % 9]}\",\"length\":{3 + i % 5},\"height\":2.8,\"material\":\"Concreto\"}}");
                    sb2.Append($"{{\"id\":\"E{i}\",\"uniqueId\":\"u{i}\",\"name\":\"Elemento {i}\",\"category\":\"{cats[i % 9]}\",\"length\":{3 + i % 5 + (i % 3 == 0 ? 1 : 0)},\"height\":2.8,\"material\":\"{(i % 4 == 0 ? "Aço" : "Concreto")}\"}}");
                }
                File.WriteAllText(Path.Combine(big, "a.json"), sb1.Append("]}").ToString());
                File.WriteAllText(Path.Combine(big, "b.json"), sb2.Append("]}").ToString());
                var dlg = new QueueDialogs(); dlg.Queue.Enqueue(Path.Combine(big, "a.json")); dlg.Queue.Enqueue(Path.Combine(big, "b.json"));
                vm.AttachDialogs(dlg);
                var swUi = Stopwatch.StartNew();
                Click(win, FindByText(win, "Selecionar")!); PumpUntil(() => vm.PreviousPath.Contains("Grande"));
                var selects = win.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "Selecionar" && t.IsEffectivelyVisible)
                    .Select(t => t.GetVisualAncestors().OfType<Button>().First()).ToList();
                Click(win, selects[1]); PumpUntil(() => vm.CurrentPath.Contains("Grande"));
                var tLoad = swUi.ElapsedMilliseconds; swUi.Restart();
                Click(win, FindByText(win, "Comparar versões")!); PumpUntil(() => vm.Result is { TotalElements: 10000 });
                var tCmp = swUi.ElapsedMilliseconds; swUi.Restart();
                vm.ShowChanges = true; Pump(100); using (win.CaptureRenderedFrame()) { }
                var tRender = swUi.ElapsedMilliseconds; swUi.Restart();
                vm.SearchText = "Elemento 123"; Pump(100); using (win.CaptureRenderedFrame()) { }
                var tSearch = swUi.ElapsedMilliseconds; swUi.Restart();
                vm.SearchText = ""; vm.SelectedChangeType = "Modificado"; Pump(100); using (win.CaptureRenderedFrame()) { }
                var tFilter = swUi.ElapsedMilliseconds;
                Console.WriteLine($"   10k na UI: carga {tLoad} ms · comparar {tCmp} ms · renderizar tabela ({vm.Result!.TotalChanges} alterações) {tRender} ms · busca {tSearch} ms · filtro ({vm.Rows.Count} linhas) {tFilter} ms");
                Check(tRender < 3000 && tSearch < 1500 && tFilter < 3000, "UI com 10.000 elementos permanece responsiva (<3 s por operação, incluindo pausas de 100 ms)");
                Shot(win, outDir, "06-big-10k.png");
            }
            catch (Exception e) { Console.Error.WriteLine(e); failures++; }
            finally { done.TrySetResult(); }
            return Task.CompletedTask;
        };
        _ = session.Dispatch(work, CancellationToken.None);
        await done.Task.WaitAsync(TimeSpan.FromMinutes(5));
        Console.WriteLine(failures == 0 ? "UI CHECK: tudo OK" : $"UI CHECK: {failures} falha(s)");
        return failures == 0 ? 0 : 1;
    }

    class QueueDialogs : IDialogService
    {
        public Queue<string> Queue { get; } = new();
        public Task<string?> PickJsonAsync(string title) => Task.FromResult<string?>(Queue.Count > 0 ? Queue.Dequeue() : null);
        public Task<string?> PickSaveAsync(string title, string suggestedName, string extension) => Task.FromResult<string?>(null);
    }
}
