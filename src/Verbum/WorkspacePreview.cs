using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Verbum;

internal static class WorkspacePreview
{
    internal static async Task WriteGraph(string path)
    {
        var notes = new[] { "Joel · estudo da passagem", "Memória e tradição", "Transmissão entre gerações", "Vocabulário hebraico", "Plano de aula", "Perguntas para pesquisar" }.Select((title, i) => new TabState { Kind = "notes", Id = "demo" + i, NoteTitle = title }).ToList();
        notes[0].Note = NoteLinks.Markdown("Memória", NoteLinks.Note("demo1")) + "\n" + NoteLinks.Markdown("Transmissão", NoteLinks.Note("demo2"));
        notes[1].Note = NoteLinks.Markdown("Joel", NoteLinks.Note("demo0"));
        notes[3].Note = NoteLinks.Markdown("Passagem", NoteLinks.Note("demo0"));
        notes[4].Note = NoteLinks.Markdown("Transmissão", NoteLinks.Note("demo2")) + "\n" + NoteLinks.Markdown("Vocabulário", NoteLinks.Note("demo3"));
        notes[0].Note += "\n#memória"; notes[1].Note += "\n#memória"; notes[3].Note += "\n#hebraico"; notes[4].Note += "\n#hebraico";
        var panel = new System.Windows.Controls.DockPanel { Margin = new(18), Background = Theme.Surface };
        var title = MainWindow.Label("Grafo de notas · exemplo de conexões", 22, MainWindow.Green); title.Margin = new(0, 0, 0, 10); System.Windows.Controls.DockPanel.SetDock(title, System.Windows.Controls.Dock.Top); panel.Children.Add(title); panel.Children.Add(new NoteGraphView(notes, _ => { }));
        await RenderPanel(path, panel, 1050, 780);
    }
    internal static async Task WriteTrash(string path, double width, bool active = false)
    {
        var notes = new List<TabState>
        {
            new() { NoteColor = "#BBDDF8", NoteTitle = "Gômer · Oséias 1:3", Note = "[#Gômer](verbum://word/demo) · Oséias 1:3 (ARA)" },
            new() { NoteTitle = "O chamado de Samuel", Note = "Comparar as três respostas ao chamado em 1 Samuel 3." },
            new() { NoteTitle = "Notas do estudo" }
        };
        var panel = new System.Windows.Controls.Border { Padding = new(16), Background = Theme.Surface,
            Child = new NotesCatalogView(() => notes, _ => { }, () => { }, _ => { }, () => notes, _ => { }, _ => { }, active ? "Notas" : "Lixeira") };
        await RenderPanel(path, panel, width, 480);
    }
    /// <summary>Renders the hover cards of a few real words (original and translation) for review.</summary>
    internal static async Task WriteTips(string libraryPath, string path)
    {
        var library = new BibleLibrary(libraryPath); var tips = new List<(string Caption, System.Windows.Controls.ToolTip Tip)>();
        var bhs = await library.LoadAsync("BHS-A-Strong");
        foreach (var (book, chapter, verseNumber, index) in new[] { (0, 1, 1, 0), (0, 1, 1, 2), (0, 12, 1, 9), (28, 3, 1, 5) })
        {
            var verse = BibleLibrary.ResolveVerse(bhs, book, chapter, verseNumber)!;
            tips.Add(("BHS · " + verse.Reference + " · " + verse.Words[index].Text, WordTip.ForOriginal(library, bhs, verse, verse.Words[index])));
        }
        if (library.AlignedTranslation() is { } id && await library.Alignment(id) is { } alignment)
        {
            var translation = await library.LoadAsync(id);
            foreach (var (book, chapter, verseNumber, word) in new[] { (0, 1, 1, "No"), (0, 1, 1, "criou"), (4, 6, 5, "coração"), (18, 23, 1, "pastor") })
            {
                var verse = BibleLibrary.ResolveVerse(translation, book, chapter, verseNumber)!; var token = SearchText.Tokenize(verse.Text).First(t => t.Original == word);
                var terms = alignment.Originals(verse, token.Start, token.Length);
                if (terms.Count > 0) tips.Add((id + " · " + verse.Reference + " · " + word, WordTip.ForTranslation(library, alignment, word, terms)));
            }
        }
        foreach (var (_, tip) in tips) tip.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.ToolTip.OpenedEvent));
        await library.Parsing; await library.Lexicons; await Task.Delay(2500);
        var column = new System.Windows.Controls.WrapPanel { Margin = new(14), Width = 1280 };
        foreach (var (caption, tip) in tips)
        {
            var content = (FrameworkElement)tip.Content; tip.Content = null;
            var card = new System.Windows.Controls.StackPanel { Margin = new(8), Width = 400 };
            card.Children.Add(MainWindow.Label(caption, 10, MainWindow.Muted));
            card.Children.Add(new System.Windows.Controls.Border { Child = content, Padding = new(11, 8, 11, 8), Background = MainWindow.Paper, BorderBrush = MainWindow.Line, BorderThickness = new(1), Margin = new(0, 3, 0, 0) });
            column.Children.Add(card);
        }
        await RenderPanel(path, new System.Windows.Controls.Border { Background = Theme.Surface, Child = column }, 1340, 760);
    }
    private static async Task RenderPanel(string path, FrameworkElement panel, double width, double height)
    {
        var window = new Window { Content = panel, Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 }; window.Show();
        try
        {
            await Task.Delay(100); window.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth), (int)Math.Ceiling(panel.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(panel);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); using var file = File.Create(path); png.Save(file);
        }
        finally { window.Close(); }
    }
    public static async Task Write(string library, string path, double width, int paneCount = 3, bool notesPreview = false)
    {
        var session = new StudySession { SidebarCollapsed = true, Width = width, Height = 900, Layout = new() { Kind = "split", Ratio = .34, Children = [
            new() { Tabs = [new() { Edition = "WLC-OSHB", Book = 28, Chapter = 1, Verse = 3 }] },
            new() { Kind = "split", Ratio = .52, Children = [new() { Tabs = [new() { Kind = "exegesis", Edition = "WLC-OSHB", Book = 28, Chapter = 1, Verse = 3, GuideEndVerse = 3, GuideReference = "Joel 1:3", GuideExpanded = ["words"] }] }, new() { Tabs = [new() { Edition = "ARA", Book = 28, Chapter = 1, Verse = 3 }] }] }
        ] } };
        if (paneCount == 2) session.Layout = new() { Kind = "split", Ratio = .5, Children = [new() { Tabs = [new() { Edition = "WLC-OSHB", Book = 28, Chapter = 1, Verse = 3 }] }, new() { Tabs = [new() { Edition = "ARA", Book = 28, Chapter = 1, Verse = 3 }] }] };
        if (paneCount == 1) session.Layout = new() { Tabs = [new() { Edition = "WLC-OSHB", Book = 28, Chapter = 1, Verse = 3 }] };
        int expectedReaders = paneCount == 3 ? 2 : paneCount, expectedGuides = paneCount == 3 ? 1 : 0;
        if (notesPreview)
        {
            session.Layout = new() { Kind = "split", Ratio = .64, Children = [
                new() { Tabs = [new() { Kind = "notes", Id = "demostudy", NoteColor = "#BBDDF8", NoteTitle = "Joel · estudo e comentários", NoteEditorMode = "visual", Note = "# Joel 1:1–3\n\n## Observações\n\n==Ouvir, recordar e transmitir== são os movimentos desta passagem.\n\n> A memória da comunidade atravessa gerações.\n\n- Observar os verbos e seus destinatários.\n- Relacionar a experiência dos pais com a dos filhos.\n\n## Vínculos do estudo\n\n" + NoteLinks.Markdown("Abrir Joel 1:1–3 na ARA", NoteLinks.Bible("ARA", "Joel 1:1–3")) + "\n\n" + NoteLinks.Markdown("Meu comentário sobre a passagem", NoteLinks.Note("democomment")) + "\n\n## Perguntas\n\n1. Quem recebe a palavra?\n2. Como o relato deve ser transmitido?" }, new() { Kind = "notes", Id = "democomment", NoteTitle = "Comentário · memória e transmissão", Note = "# Comentário pessoal\n\nExemplo de nota vinculada à passagem." }] },
                new() { Tabs = [new() { Edition = "WLC-OSHB", Book = 28, Chapter = 1, Verse = 3 }] }
            ] }; expectedReaders = 1; expectedGuides = 0;
        }
        var window = new MainWindow(library, session, true) { Width = Math.Clamp(width, 1050, 2000), Height = 900, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000 };
        window.Show();
        try
        {
            var until = DateTime.UtcNow.AddSeconds(40);
            while (DateTime.UtcNow < until)
            {
                window.UpdateLayout();
                var readers = Children<ReaderView>(window).ToList(); var guides = Children<ExegeticalGuideView>(window).ToList();
                if (readers.Count == expectedReaders && readers.All(v => v.PreviewReady) && guides.Count == expectedGuides && guides.All(g => g.PreviewReady)) break;
                await Task.Delay(75);
            }
            if (Children<ReaderView>(window).Any(v => !v.PreviewReady) || Children<ExegeticalGuideView>(window).Any(v => !v.PreviewReady)) throw new InvalidOperationException("A área não concluiu o carregamento para a prévia.");
            // Rebuilding the layout must keep the loaded views on screen instead of recreating them.
            var before = Children<ReaderView>(window).Cast<object>().Concat(Children<ExegeticalGuideView>(window)).ToList();
            window.RebuildLayout(); window.UpdateLayout(); await Task.Delay(150); window.UpdateLayout();
            var after = Children<ReaderView>(window).Cast<object>().Concat(Children<ExegeticalGuideView>(window)).ToList();
            if (after.Count != before.Count || before.Except(after).Any()) throw new InvalidOperationException($"Reconstrução do layout recriou as telas: {before.Count} antes, {after.Intersect(before).Count()} mantidas.");
            // Exercise a live theme round trip after every reader is loaded, without saving preferences.
            bool originalTheme = Theme.IsDark;
            Theme.Apply(!originalTheme); window.UpdateLayout();
            Theme.Apply(originalTheme); window.UpdateLayout();
            window.UpdateLayout(); var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); using var file = File.Create(path); png.Save(file);
        }
        finally { window.Close(); }
    }
    private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }
}
