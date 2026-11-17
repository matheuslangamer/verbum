using System.IO;
using System.Windows;

namespace Verbum;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Theme.Load();
        if (e.Args.Contains("--dark")) Theme.Apply(true);
        if (e.Args.Contains("--light")) Theme.Apply(false);
        if (e.Args.Contains("--workspace-preview"))
        {
            string Argument(string name, string fallback) { int i = Array.IndexOf(e.Args, name); return i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : fallback; }
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { if (e.Args.Contains("--tips-preview")) await WorkspacePreview.WriteTips(FindLibrary(e.Args), Argument("--workspace-preview", "tips.png")); else if (e.Args.Contains("--trash-preview") || e.Args.Contains("--catalog-preview")) await WorkspacePreview.WriteTrash(Argument("--workspace-preview", "trash.png"), double.Parse(Argument("--width", "900")), e.Args.Contains("--catalog-preview")); else if (e.Args.Contains("--notes-graph-preview")) await WorkspacePreview.WriteGraph(Argument("--workspace-preview", "graph.png")); else await WorkspacePreview.Write(FindLibrary(e.Args), Argument("--workspace-preview", "workspace.png"), double.Parse(Argument("--width", "1440")), Math.Clamp(int.Parse(Argument("--panes", "3")), 1, 3), e.Args.Contains("--notes-preview")); Shutdown(0); }
                catch (Exception ex) { File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "docs", "workspace-preview-error.txt"), ex.ToString()); Shutdown(1); }
            })); return;
        }
        if (e.Args.Contains("--guide-preview"))
        {
            string Argument(string name, string fallback) { int i = Array.IndexOf(e.Args, name); return i >= 0 && i + 1 < e.Args.Length ? e.Args[i + 1] : fallback; }
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    var state = new TabState { Kind = "exegesis", Edition = Argument("--edition", "WLC-OSHB"), GuideReference = Argument("--reference", "Joel 1:1-3") };
                    string section = Argument("--section", ""); if (section.Length > 0) state.GuideExpanded = [section];
                    var guide = new ExegeticalGuideView(new BibleLibrary(FindLibrary(e.Args)), state, () => { }, _ => { }, (_, _) => { }, (_, _) => { });
                    await guide.WritePreview(Argument("--guide-preview", "guide-preview.png"), double.TryParse(Argument("--width", "1060"), out double previewWidth) ? Math.Clamp(previewWidth, 360, 2000) : 1060); Shutdown(0);
                }
                catch (Exception ex) { File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "docs", "guide-preview-error.txt"), ex.ToString()); Shutdown(1); }
            }));
            return;
        }
        if (e.Args.Contains("--import-documents"))
        {
            try { var catalog = new DocumentLibrary(FindLibrary(e.Args)); int i = Array.IndexOf(e.Args, "--import-documents") + 1; foreach (string path in e.Args.Skip(i).TakeWhile(a => !a.StartsWith("--"))) catalog.Import(path); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "docs", "document-import-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--perf-report"))
        {
            int i = Array.IndexOf(e.Args, "--perf-report"); string report = i + 1 < e.Args.Length ? e.Args[i + 1] : "perf.txt";
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                var lines = new List<string>(); var clock = System.Diagnostics.Stopwatch.StartNew();
                async Task Time(string name, Func<Task> action) { long before = clock.ElapsedMilliseconds; try { await action(); } catch (Exception ex) { name += " ERRO " + ex.Message; } lines.Add($"{clock.ElapsedMilliseconds - before,7} ms  {GC.GetTotalMemory(false) / 1048576,6} MB  {name}"); }
                try
                {
                    var library = new BibleLibrary(FindLibrary(e.Args));
                    await Time("ARA (texto)", () => library.LoadAsync("ARA"));
                    await Time("ARA (alinhamento)", () => library.Alignment("ARA"));
                    await Time("WLC-OSHB", () => library.LoadAsync("WLC-OSHB"));
                    await Time("BHS-A-Strong + ligação OSHB", () => library.LoadAsync("BHS-A-Strong"));
                    await Time("Parsing pessoal", () => library.Parsing);
                    await Time("Léxicos", () => library.Lexicons);
                    await Time("NAA (alinhamento)", () => library.Alignment("NAA"));
                }
                catch (Exception ex) { lines.Add(ex.ToString()); }
                File.WriteAllLines(report, lines); Shutdown(0);
            }));
            return;
        }
        if (e.Args.Contains("--build-alignment"))
        {
            int i = Array.IndexOf(e.Args, "--build-alignment");
            string translations = i + 1 < e.Args.Length && !e.Args[i + 1].StartsWith("--") ? e.Args[i + 1] : "ARA";
            Dispatcher.BeginInvoke(new Action(async () => { int code = await Task.Run(() => AlignmentBuilder.Run(FindLibrary(e.Args), translations)); Shutdown(code); }));
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            Dispatcher.BeginInvoke(new Action(async () => { int code = await Task.Run(() => SelfTests.Run(e.Args)); Shutdown(code); }));
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(SessionStore.DataDirectory, "errors.log"), DateTimeOffset.Now + " " + args.Exception + "\n"); } catch { }
            MessageBox.Show("Não foi possível concluir esta ação. Seus arquivos bíblicos não foram alterados.\n\n" + args.Exception.Message, "VERBUM", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        var window = new MainWindow(FindLibrary(e.Args));
        MainWindow = window;
        window.Show();
    }

    internal static string FindLibrary(string[] args)
    {
        int i = Array.IndexOf(args, "--library");
        if (i >= 0 && i + 1 < args.Length) return Path.GetFullPath(args[i + 1]);
        var remembered = SessionStore.Load()?.LibraryPath;
        if (remembered != null && Directory.Exists(remembered)) return remembered;
        foreach (string seed in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(seed);
            for (int n = 0; dir != null && n < 7; n++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "biblioteca", "biblias");
                if (Directory.Exists(candidate)) return candidate;
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "biblioteca", "biblias");
    }
}
