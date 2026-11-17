using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Verbum;

public sealed class BookReaderView : UserControl
{
    private readonly DocumentLibrary library;
    private readonly TabState state;
    private readonly Action changed;
    private ReadingDocument? document;
    private TextMarkers markers => new(Path.Combine(SessionStore.DataDirectory, "document-markers.json"));
    private readonly List<(Paragraph Paragraph, VerseEntry Entry)> paragraphs = [];
    private readonly Action<TabState> openNote;
    private readonly ComboBox sections = new() { MinWidth = 130, MaxWidth = 380, Margin = new(5) };
    private readonly CheckBox textMode = new() { Content = "Texto pesquisável", VerticalAlignment = VerticalAlignment.Center, Margin = new(8) };
    private readonly TextBox find = new() { MinWidth = 130, Margin = new(4), ToolTip = "Pesquisar dentro deste livro, sem acentos ou sinais hebraicos" };
    private readonly TextBlock status = MainWindow.Label("Abrindo leitura…", 12, MainWindow.Muted);
    private readonly ContentControl content = new();
    private readonly FlowDocumentScrollViewer reading = new() { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Image page = new() { Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top };
    private readonly ScrollViewer imageScroll;
    private bool updating;
    private int generation;
    private double zoom = 1;
    public BookReaderView(DocumentLibrary library, TabState state, Action changed, Action<TabState> openNote)
    {
        this.library = library; this.state = state; this.changed = changed; this.openNote = openNote;
        imageScroll = new() { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = MainWindow.Paper };
        var root = new DockPanel { Margin = new(10) }; Content = root;
        var head = new StackPanel(); var heading = MainWindow.Label(state.DocumentTitle, 16, MainWindow.Green); heading.TextTrimming = TextTrimming.CharacterEllipsis; heading.TextWrapping = TextWrapping.NoWrap; heading.ToolTip = state.DocumentTitle; head.Children.Add(heading);
        Button Small(string text, Action action, string? tip = null) { var button = MainWindow.Button(text, action, tip); button.Height = 28; button.Margin = new(2, 0, 2, 0); button.Padding = new(7, 2, 7, 2); return button; }
        var navigation = new Grid { Margin = new(0, 5, 0, 3) }; navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); navigation.ColumnDefinitions.Add(new()); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        navigation.Children.Add(Small("‹", () => Move(-1), "Página / seção anterior")); sections.MinWidth = 35; sections.MaxWidth = double.PositiveInfinity; sections.Height = sections.MinHeight = 28; sections.Padding = new(5, 2, 5, 2); sections.Margin = new(2, 0, 2, 0); Grid.SetColumn(sections, 1); navigation.Children.Add(sections);
        var next = Small("›", () => Move(1), "Próxima página / seção"); Grid.SetColumn(next, 2); navigation.Children.Add(next);
        var search = new WrapPanel { Visibility = state.DocumentQuery.Length > 0 ? Visibility.Visible : Visibility.Collapsed }; find.Text = state.DocumentQuery; find.MinWidth = 80; find.Height = 28; find.Padding = new(5, 2, 5, 2); search.Children.Add(find); search.Children.Add(Small("Buscar", Search)); search.Children.Add(textMode);
        var colors = new ContextMenu(); foreach (var color in TextMarkers.Colors) { var item = new MenuItem { Header = color.Key }; item.Click += (_, _) => Mark(color.Value); colors.Items.Add(item); }
        var clear = new MenuItem { Header = "Remover destaque da seleção" }; clear.Click += (_, _) => Mark(null); colors.Items.Add(clear); MarkerMenu.AddFontColors(colors, color => Mark(color, "foreground"), () => Window.GetWindow(this)); reading.ContextMenu = colors;
        void Note()
        {
            if (document == null) return; string quote = content.Content == reading ? reading.Selection?.Text.Trim() ?? "" : "";
            openNote(new TabState { Kind = "notes", NoteCreated = DateTimeOffset.Now, DocumentId = document.Id, DocumentSection = state.DocumentSection, NoteTitle = document.Title + " · " + document.Sections[state.DocumentSection].Title,
                Note = "# " + document.Title + "\n\n" + document.Sections[state.DocumentSection].Title + "\n\n" + (quote.Length > 0 ? "> " + quote.Replace("\n", "\n> ") + "\n\n" : "") });
        }
        var menu = new ContextMenu(); void Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("Pesquisar / modo de texto…", () => { search.Visibility = search.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; if (search.Visibility == Visibility.Visible) find.Focus(); });
        Item("Aumentar fonte / zoom", () => { state.FontSize = Math.Min(34, state.FontSize + 2); zoom = Math.Min(2.5, zoom + .2); _ = Show(); changed(); });
        Item("Diminuir fonte / zoom", () => { state.FontSize = Math.Max(14, state.FontSize - 2); zoom = Math.Max(.5, zoom - .2); _ = Show(); changed(); });
        Item("Marcar seleção…", () => { colors.PlacementTarget = reading; colors.IsOpen = true; }); Item("Nota desta seção", Note);
        var more = Small("⋯", () => menu.IsOpen = true, "Pesquisar, modo de texto, zoom, marcações e notas"); menu.PlacementTarget = more; Grid.SetColumn(more, 3); navigation.Children.Add(more); head.Children.Add(navigation); head.Children.Add(search); head.Children.Add(status); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head); root.Children.Add(content);
        sections.SelectionChanged += (_, _) => { if (!updating && sections.SelectedIndex >= 0) { state.DocumentSection = sections.SelectedIndex; state.Scroll = 0; changed(); _ = Show(); } };
        textMode.Checked += (_, _) => _ = Show(); textMode.Unchecked += (_, _) => _ = Show(); find.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Search(); };
        Loaded += async (_, _) =>
        {
            try { document = await Task.Run(() => library.Find(state.DocumentId)); if (document == null) { status.Text = "Documento não encontrado na pasta livros desta biblioteca."; return; }
                updating = true; sections.ItemsSource = document.Sections; state.DocumentSection = Math.Clamp(state.DocumentSection, 0, document.Sections.Count - 1); sections.SelectedIndex = state.DocumentSection; updating = false;
                textMode.Visibility = document.Format == "PDF" ? Visibility.Visible : Visibility.Collapsed;
                if (document.Format == "PDF" && state.DocumentQuery.Length > 0) textMode.IsChecked = true;
                await Show(); }
            catch (Exception ex) { status.Text = "Não foi possível abrir: " + ex.Message; }
        };
    }
    private void Move(int delta) { if (document != null) sections.SelectedIndex = Math.Clamp(state.DocumentSection + delta, 0, document.Sections.Count - 1); }
    private void Search()
    {
        if (document == null || string.IsNullOrWhiteSpace(find.Text)) return;
        state.DocumentQuery = find.Text.Trim();
        var matches = document.Sections.Select((s, i) => (s, i)).Where(x => DocumentLibrary.Matches(x.s.PlainText, state.DocumentQuery, SearchMode.Intelligent).Count > 0).Select(x => x.i).ToList();
        if (matches.Count == 0) { status.Text = "Nenhum trecho encontrado no texto extraído. Páginas sem OCR não entram na busca."; return; }
        int index = matches.FirstOrDefault(i => i > state.DocumentSection, matches[0]); updating = true; state.DocumentSection = index; sections.SelectedIndex = index; updating = false; textMode.IsChecked = document.Format == "PDF"; _ = Show(); changed();
    }
    private async Task Show()
    {
        if (document == null) return; int token = ++generation, index = state.DocumentSection; var section = document.Sections[index];
        status.Text = $"{index + 1} de {document.Sections.Count} · {section.Title}";
        if (document.Format == "PDF" && textMode.IsChecked != true)
        {
            content.Content = imageScroll; imageScroll.ScrollToTop(); status.Text += " · página original";
            try { string path = library.OriginalPath(document); int width = (int)(1000 * zoom); var bitmap = await Task.Run(() => PdfDocumentReader.RenderPage(path, index, width)); if (token != generation) return; page.Source = bitmap; page.Width = bitmap.PixelWidth * .72; }
            catch (Exception ex) { if (token == generation) status.Text = "Não foi possível renderizar o PDF: " + ex.Message; }
            return;
        }
        paragraphs.Clear();
        var flow = new FlowDocument { FontFamily = new("Georgia, Times New Roman"), FontSize = state.FontSize, PagePadding = new(25), ColumnWidth = double.PositiveInfinity, Foreground = Theme.Get("#2D3730"), Background = Theme.Surface };
        flow.Blocks.Add(new Paragraph(new Run(section.Title)) { FontSize = 27, FontWeight = FontWeights.Bold, Margin = new(0, 0, 0, 20) });
        foreach (var block in section.Blocks)
        {
            if (block.Kind == "image")
            {
                try { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(library.AssetPath(block.Image)); bitmap.EndInit(); bitmap.Freeze(); var illustration = new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Center, Margin = new(5, 12, 5, 12), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = "Clique para ampliar a imagem" }; illustration.MouseLeftButtonDown += (_, _) => ShowImage(bitmap, block.Text); flow.Blocks.Add(new BlockUIContainer(illustration)); if (block.Text.Length > 0) flow.Blocks.Add(new Paragraph(new Run(block.Text)) { FontSize = 12, FontStyle = FontStyles.Italic }); }
                catch { flow.Blocks.Add(new Paragraph(new Run("Ilustração indisponível; consulte o arquivo original."))); }
            }
            else if (block.Kind == "table")
            {
                var table = new Table { CellSpacing = 0, Margin = new(0, 8, 0, 18) }; int columns = block.Rows.Select(r => r.Count).DefaultIfEmpty(0).Max();
                for (int c = 0; c < columns; c++) table.Columns.Add(new TableColumn()); var group = new TableRowGroup(); table.RowGroups.Add(group);
                for (int r = 0; r < block.Rows.Count; r++)
                {
                    var row = new TableRow { Background = r == 0 ? MainWindow.Line : r % 2 == 0 ? MainWindow.Paper : Theme.Surface }; group.Rows.Add(row);
                    foreach (string cell in block.Rows[r]) { var p = Paragraph(cell); if (r == 0) p.FontWeight = FontWeights.Bold; row.Cells.Add(new TableCell(p) { Padding = new(8), BorderThickness = new(.5), BorderBrush = MainWindow.Line }); }
                }
                flow.Blocks.Add(table);
            }
            else foreach (string line in block.Text.Replace("\r", "").Split('\n').Where(t => !string.IsNullOrWhiteSpace(t))) { var p = Paragraph(line); if (block.Kind == "heading") { p.FontWeight = FontWeights.Bold; p.FontSize = 23; p.Margin = new(0, 18, 0, 10); } flow.Blocks.Add(p); }
        }
        if (string.IsNullOrWhiteSpace(section.PlainText)) flow.Blocks.Add(new Paragraph(new Run("Esta página não tem texto pesquisável. Volte à página original para ler; a busca requer OCR.")));
        content.Content = reading; reading.Document = flow; reading.UpdateLayout();
        _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { if (token != generation) return; var match = flow.Blocks.OfType<Paragraph>().FirstOrDefault(p => p.Inlines.OfType<Run>().Any(r => r.FontWeight == FontWeights.Bold && r.Background == Brushes.Khaki)); if (match != null) match.BringIntoView(); else flow.ContentStart.Paragraph?.BringIntoView(); }));
        if (document.Format == "PDF") status.Text += " · texto extraído, diagramação simplificada";
    }
    private Paragraph Paragraph(string text)
    {
        var p = new Paragraph { Margin = new(0, 0, 0, 12), LineHeight = state.FontSize * 1.65 };
        bool hebrew = text.Count(c => c is >= '\u05D0' and <= '\u05EA') > text.Count(char.IsLetter) / 2;
        if (hebrew) { p.FlowDirection = FlowDirection.RightToLeft; p.FontFamily = new("SBL Hebrew, Times New Roman"); }
        var entry = new VerseEntry("document:" + state.DocumentId, 0, "", "", state.DocumentSection, paragraphs.Count, null, null, text, true);
        paragraphs.Add((p, entry));
        var hits = DocumentLibrary.Matches(text, state.DocumentQuery, SearchMode.Intelligent, false);
        var saved = markers.For(entry); var cuts = new SortedSet<int> { 0, text.Length };
        foreach (var h in hits) { cuts.Add(h.Start); cuts.Add(h.Start + h.Length); }
        foreach (var m in saved) { cuts.Add(m.Start); cuts.Add(m.Start + m.Length); }
        var positions = cuts.ToArray();
        for (int i = 0; i + 1 < positions.Length; i++)
        {
            int start = positions[i], end = positions[i + 1]; var run = new Run(text[start..end]);
            var mark = saved.FirstOrDefault(m => m.Kind == "background" && m.Start <= start && m.Start + m.Length >= end);
            if (mark != null) run.Background = (Brush)new BrushConverter().ConvertFromString(mark.Color)!;
            if (hits.Any(h => h.Start <= start && h.Start + h.Length >= end)) { run.FontWeight = FontWeights.Bold; if (mark == null) run.Background = Brushes.Khaki; }
            var ink = saved.FirstOrDefault(m => m.Kind == "foreground" && m.Start <= start && m.Start + m.Length >= end);
            if (ink != null) run.Foreground = Theme.Raw(ink.Color);
            p.Inlines.Add(run);
        }
        return p;
    }
    private void Mark(string? color, string kind = "background")
    {
        if (content.Content != reading || reading.Selection == null || reading.Selection.IsEmpty) { status.Text = "Selecione um trecho no texto pesquisável para marcar."; return; }
        var selection = reading.Selection; var ranges = new List<(VerseEntry Verse, int Start, int Length)>();
        foreach (var (paragraph, entry) in paragraphs)
        {
            var start = selection.Start.CompareTo(paragraph.ContentStart) > 0 ? selection.Start : paragraph.ContentStart;
            var end = selection.End.CompareTo(paragraph.ContentEnd) < 0 ? selection.End : paragraph.ContentEnd;
            if (start.CompareTo(end) >= 0) continue;
            int offset = new TextRange(paragraph.ContentStart, start).Text.Length;
            int length = new TextRange(start, end).Text.Length;
            if (length > 0 && offset + length <= entry.Text.Length) ranges.Add((entry, offset, length));
        }
        try { markers.Apply(ranges, color, kind); _ = Show(); }
        catch (Exception ex) { status.Text = "Não foi possível salvar a marcação: " + ex.Message; }
    }
    private void ShowImage(BitmapSource bitmap, string title)
    {
        var image = new Image { Source = bitmap, Width = bitmap.PixelWidth, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
        var root = new DockPanel(); var toolbar = new WrapPanel(); toolbar.Children.Add(MainWindow.Button("Diminuir", () => image.Width = Math.Max(200, image.Width / 1.2))); toolbar.Children.Add(MainWindow.Button("Ampliar", () => image.Width = Math.Min(5000, image.Width * 1.2))); toolbar.Children.Add(MainWindow.Button("Tamanho original", () => image.Width = bitmap.PixelWidth)); DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        root.Children.Add(new ScrollViewer { Content = image, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        new Window { Title = title.Length > 0 ? title : "Ilustração do documento", Owner = Window.GetWindow(this), Width = Math.Min(SystemParameters.WorkArea.Width - 50, 1200), Height = Math.Min(SystemParameters.WorkArea.Height - 50, 850), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = root }.ShowDialog();
    }
}
