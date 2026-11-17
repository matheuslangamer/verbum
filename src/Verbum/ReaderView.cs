using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Verbum;

public sealed class ReaderView : UserControl
{
    private readonly BibleLibrary library;
    private readonly TabState state;
    private readonly Action changed;
    private readonly Action<string> status;
    private readonly Action<TabState> navigate;
    private readonly Action<VerseEntry, OriginalWord> openWord;
    private readonly Action<VerseEntry> openVariants;
    private readonly Func<List<TabState>>? loadNotes;
    private readonly Action<TabState>? createNote, openNote;
    private ILookup<int, TabState> annotations = Array.Empty<TabState>().ToLookup(n => 0);
    private (VerseEntry Verse, int Start, int End)? clickedWord;
    internal void RefreshAnnotations() => Refresh(false);
    private List<int> bookIds = [];
    private readonly ComboBox editions = new() { Width = 108 }, books = new() { MinWidth = 115 }, chapters = new() { Width = 62 }, links = new() { Width = 74 };
    private readonly TextBox reference = new() { MinWidth = 115, FontSize = 14 };
    private readonly TextBlock title = new() { FontFamily = new("Georgia"), FontSize = 30, FontWeight = FontWeights.Bold }, subtitle = new() { Foreground = MainWindow.Muted, FontSize = 11, Margin = new(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock warning = new() { Foreground = MainWindow.Brush("#8B5D25"), Background = MainWindow.Brush("#FFF4DD"), Padding = new(10, 7, 10, 7), TextWrapping = TextWrapping.Wrap, FontSize = 11, Visibility = Visibility.Collapsed };
    private readonly FlowDocumentScrollViewer viewer = new() { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Theme.Surface };
    private readonly TextBlock footer = new() { FontSize = 10, Foreground = MainWindow.Muted, Margin = new(15, 5, 15, 7) };
    private readonly Dictionary<int, Paragraph> paragraphs = [];
    private readonly Dictionary<Run, (VerseEntry Verse, int Start)> sourceRuns = [];
    internal bool PreviewReady => edition != null && loading == null;
    private BibleEdition? edition;
    private TranslationAlignment? alignment;
    private Task? loading;
    private bool updating;
    private int generation;
    private SearchHit? currentHit;
    private List<VerseEntry> linkedTargets = [];

    public ReaderView(BibleLibrary library, TabState state, Action changed, Action<string> status, Action<TabState> navigate,
        Action<VerseEntry, OriginalWord> openWord, Action<VerseEntry> openVariants, Func<List<TabState>>? loadNotes = null, Action<TabState>? createNote = null, Action<TabState>? openNote = null)
    {
        this.loadNotes = loadNotes; this.createNote = createNote; this.openNote = openNote;
        this.library = library; this.state = state; this.changed = changed; this.status = status; this.navigate = navigate;
        this.openWord = openWord; this.openVariants = openVariants;
        var grid = new Grid();
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = grid;
        var toolbar = new Grid { Margin = new(6, 5, 6, 2), Name = "ReaderToolbar" };
        var nav = new Grid { Margin = new(0, 0, 4, 3) }; nav.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); nav.ColumnDefinitions.Add(new()); nav.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Button Small(string text, Action action, string tip)
        {
            var button = MainWindow.Button(text, action, tip); button.Padding = new(4, 2, 4, 2); button.Width = 29; button.Height = 28; button.Margin = new(1, 0, 1, 0);
            System.Windows.Automation.AutomationProperties.SetName(button, tip); ToolTipService.SetInitialShowDelay(button, 250); return button;
        }
        nav.Children.Add(Small("‹", () => ChangeChapter(-1), "Capítulo anterior"));
        reference.MinWidth = 35; reference.Height = 28; reference.MinHeight = 28; reference.Padding = new(6, 3, 6, 3); reference.Margin = new(2, 0, 2, 0);
        reference.ToolTip = "Digite uma referência e pressione Enter: João 3:16, Sl 23 ou Gn 1."; System.Windows.Automation.AutomationProperties.SetName(reference, "Referência bíblica");
        reference.KeyDown += (_, e) => { if (e.Key == Key.Enter) { JumpReference(); e.Handled = true; } }; Grid.SetColumn(reference, 1); nav.Children.Add(reference);
        var next = Small("›", () => ChangeChapter(1), "Próximo capítulo"); Grid.SetColumn(next, 2); nav.Children.Add(next);
        var selectors = new Grid { Margin = new(0, 0, 3, 3), Name = "ReaderSelectors" };
        editions.ItemsSource = library.Editions; editions.SelectedItem = library.Editions.FirstOrDefault(e => e.Id == state.Edition);
        links.ItemsSource = new[] { "Livre", "A", "B", "C" }; links.SelectedItem = new[] { "A", "B", "C" }.Contains(state.LinkGroup) ? state.LinkGroup : "Livre";
        var controls = new[] { editions, books, chapters, links }; string[] labels = ["Edição bíblica", "Livro", "Capítulo", "Sincronização: Livre ou grupo A, B, C"];
        for (int i = 0; i < controls.Length; i++)
        {
            var control = controls[i]; control.Width = double.NaN; control.MinWidth = 0; control.Height = control.MinHeight = 28; control.Padding = new(3, 2, 3, 2); control.Margin = new(2, 0, 2, 0); control.ToolTip = labels[i]; System.Windows.Automation.AutomationProperties.SetName(control, labels[i]); selectors.Children.Add(control);
        }
        bool? selectorsStacked = null;
        void ArrangeSelectors(double width)
        {
            bool stacked = width < 260; if (selectorsStacked == stacked) return; selectorsStacked = stacked; selectors.ColumnDefinitions.Clear(); selectors.RowDefinitions.Clear();
            if (stacked)
            {
                selectors.ColumnDefinitions.Add(new()); selectors.ColumnDefinitions.Add(new()); selectors.RowDefinitions.Add(new() { Height = GridLength.Auto }); selectors.RowDefinitions.Add(new() { Height = GridLength.Auto });
                for (int i = 0; i < controls.Length; i++) { Grid.SetColumn(controls[i], i % 2); Grid.SetRow(controls[i], i / 2); controls[i].Margin = new(2, 0, 2, i < 2 ? 3 : 0); }
            }
            else
            {
                selectors.ColumnDefinitions.Add(new() { Width = new(95) }); selectors.ColumnDefinitions.Add(new()); selectors.ColumnDefinitions.Add(new() { Width = new(47) }); selectors.ColumnDefinitions.Add(new() { Width = new(63) });
                for (int i = 0; i < controls.Length; i++) { Grid.SetColumn(controls[i], i); Grid.SetRow(controls[i], 0); controls[i].Margin = new(2, 0, 2, 0); }
            }
        }
        selectors.SizeChanged += (_, e) => ArrangeSelectors(e.NewSize.Width); ArrangeSelectors(300);
        var markerMenu = new ContextMenu();
        var annotate = new MenuItem { Header = "Criar nota nesta palavra ou seleção…" };
        annotate.Click += (_, _) => Annotate();
        markerMenu.Items.Add(annotate);
        var analyzeOriginal = new MenuItem { Header = "Analisar o termo original (hebraico)…" };
        analyzeOriginal.Click += (_, _) => AnalyzeOriginal();
        markerMenu.Items.Add(analyzeOriginal);
        markerMenu.Opened += (_, _) => analyzeOriginal.Visibility = alignment != null && clickedWord is { } c && alignment.Originals(c.Verse, c.Start, c.End - c.Start).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        viewer.PreviewMouseRightButtonDown += (_, e) =>
        {
            clickedWord = null;
            if (e.OriginalSource is Run run && sourceRuns.TryGetValue(run, out var source))
                clickedWord = WordAt(source.Verse, source.Start);
        };
        var copySelection = new MenuItem { Header = "Copiar seleção" }; copySelection.Click += (_, _) => { if (viewer.Selection is { IsEmpty: false } selected) Clipboard.SetText(selected.Text); }; markerMenu.Items.Add(copySelection); markerMenu.Items.Add(new Separator());
        foreach (var color in TextMarkers.Colors)
        {
            var item = new MenuItem { Header = "Marcar · " + color.Key, Icon = new Border { Width = 15, Height = 15, Background = Theme.Raw(color.Value) } }; item.Click += (_, _) => MarkSelection(color.Value); markerMenu.Items.Add(item);
        }
        markerMenu.Items.Add(new Separator()); var clearMarker = new MenuItem { Header = "Remover destaque da seleção" }; clearMarker.Click += (_, _) => MarkSelection(null); markerMenu.Items.Add(clearMarker); MarkerMenu.AddFontColors(markerMenu, (color) => MarkSelection(color, "foreground"), () => Window.GetWindow(this)); viewer.ContextMenu = markerMenu;
        var tools = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new(2, 0, 0, 3), Name = "ReaderActions", HorizontalAlignment = HorizontalAlignment.Right };
        tools.Children.Add(Small("A−", () => ResizeText(-1), "Diminuir fonte")); tools.Children.Add(Small("A+", () => ResizeText(1), "Aumentar fonte"));
        var interlinear = Small("≡", () => { state.ShowInterlinear = !state.ShowInterlinear; Refresh(false); changed(); }, "Alternar interlinear lexical"); tools.Children.Add(interlinear);
        tools.Children.Add(Small("V", ShowVariants, "Variantes / notas da fonte"));
        tools.Children.Add(Small("▰", () => { markerMenu.PlacementTarget = tools; markerMenu.IsOpen = true; }, "Marcar seleção: escolher cor"));
        tools.Children.Add(Small("ⓘ", () =>
        {
            string details = subtitle.Text + (warning.Text.Length > 0 ? "\n\n" + warning.Text : "");
            if (edition?.Resource is { } resource) details += "\n\n" + resource.Title + "\n\n" + resource.Attribution + "\n\n" + resource.License + "\n\n" + resource.SourceUrl + "\nRevisão: " + resource.Revision;
            else details += "\n\nTexto da biblioteca JSON local. A identificação e a licença dependem da edição do arquivo.";
            MessageBox.Show(Window.GetWindow(this), details, "Edição, avisos e créditos", MessageBoxButton.OK, MessageBoxImage.Information);
        }, "Edição, avisos e créditos"));
        var noteToggle = Small("✎", () => { state.HideNoteMarkers = !state.HideNoteMarkers; Refresh(false); changed(); }, "Mostrar ou ocultar anotações pessoais");
        void UpdateToggle() { noteToggle.Opacity = state.HideNoteMarkers ? .55 : 1; noteToggle.ToolTip = state.HideNoteMarkers ? "Mostrar anotações pessoais" : "Ocultar anotações pessoais"; }
        noteToggle.Click += (_, _) => UpdateToggle(); UpdateToggle(); tools.Children.Add(noteToggle);
        toolbar.Children.Add(nav); toolbar.Children.Add(selectors); toolbar.Children.Add(tools);
        ReaderToolbarArrangement? arrangement = null;
        void ArrangeToolbar(double width)
        {
            var nextLayout = ReaderToolbarLayout.ForWidth(width); if (arrangement == nextLayout) return; arrangement = nextLayout;
            toolbar.ColumnDefinitions.Clear(); toolbar.RowDefinitions.Clear();
            foreach (var group in new FrameworkElement[] { nav, selectors, tools }) { Grid.SetColumn(group, 0); Grid.SetRow(group, 0); Grid.SetColumnSpan(group, 1); }
            if (nextLayout == ReaderToolbarArrangement.Wide)
            {
                toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width = new(310) }); toolbar.ColumnDefinitions.Add(new() { Width = new(222) }); Grid.SetColumn(selectors, 1); Grid.SetColumn(tools, 2);
            }
            else if (nextLayout == ReaderToolbarArrangement.Medium)
            {
                toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width = new(300) }); toolbar.RowDefinitions.Add(new() { Height = GridLength.Auto }); toolbar.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetColumn(selectors, 1); Grid.SetRow(tools, 1); Grid.SetColumnSpan(tools, 2);
            }
            else if (nextLayout == ReaderToolbarArrangement.Compact)
            {
                toolbar.ColumnDefinitions.Add(new()); toolbar.ColumnDefinitions.Add(new() { Width = new(222) }); toolbar.RowDefinitions.Add(new() { Height = GridLength.Auto }); toolbar.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetColumnSpan(selectors, 2); Grid.SetRow(nav, 1); Grid.SetRow(tools, 1); Grid.SetColumn(tools, 1);
            }
            else
            {
                toolbar.ColumnDefinitions.Add(new()); for (int i = 0; i < 3; i++) toolbar.RowDefinitions.Add(new() { Height = GridLength.Auto }); Grid.SetRow(nav, 1); Grid.SetRow(tools, 2);
            }
            interlinear.Background = state.ShowInterlinear ? MainWindow.Brush("#CEE1D2") : MainWindow.Brush("#EBF0E8");
        }
        interlinear.Click += (_, _) => interlinear.Background = state.ShowInterlinear ? MainWindow.Brush("#CEE1D2") : MainWindow.Brush("#EBF0E8");
        toolbar.SizeChanged += (_, e) => ArrangeToolbar(e.NewSize.Width); ArrangeToolbar(600); grid.Children.Add(toolbar);
        Grid.SetRow(viewer, 4); grid.Children.Add(viewer); Grid.SetRow(footer, 5); grid.Children.Add(footer);
        viewer.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, e) => { if (!updating && e.VerticalChange != 0) { state.Scroll = e.VerticalOffset; changed(); } }));
        editions.SelectionChanged += async (_, _) =>
        {
            if (updating || editions.SelectedItem is not EditionInfo info || info.Id == state.Edition) return;
            state.Edition = info.Id; currentHit = null; state.EntryIndex = null; state.Scroll = 0;
            await Load(); changed();
        };
        books.SelectionChanged += (_, _) => { if (!updating && books.SelectedIndex >= 0 && books.SelectedIndex < bookIds.Count) { state.Book = bookIds[books.SelectedIndex]; state.Chapter = 1; state.Verse = null; state.EntryIndex = null; UpdateChapters(); Refresh(true); } };
        chapters.SelectionChanged += (_, _) => { if (!updating && chapters.SelectedItem is int c) { state.Chapter = c; state.Verse = null; state.EntryIndex = null; Refresh(true); } };
        links.SelectionChanged += (_, _) => { if (!updating) { state.LinkGroup = links.SelectedItem as string ?? "Livre"; Refresh(false); changed(); navigate(state); } };
        Loaded += async (_, _) => { if (edition == null && loading == null) await Load(); };
    }

    private async Task Load()
    {
        int epoch = ++generation;
        title.Text = "Abrindo texto…";
        if (!library.IsLoaded(state.Edition))
            viewer.Document = new FlowDocument(new Paragraph(new Run("Abrindo " + state.Edition + "…" + (state.Edition is "BHS-A-Strong" or "WLC-OSHB" ? "\nOs textos originais anotados são grandes; a primeira abertura leva alguns segundos." : "")) { Foreground = MainWindow.Muted })) { FontFamily = new("Segoe UI"), FontSize = 14, PagePadding = new(24), Background = Theme.Surface };
        try
        {
            var task = library.LoadAsync(state.Edition); loading = task;
            var loaded = await task;
            if (epoch != generation) return;
            edition = loaded; alignment = null;
            if (loaded.Resource == null)
                _ = library.Alignment(state.Edition).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (epoch != generation || t.IsFaulted || t.Result == null) return;
                    alignment = t.Result;
                    // Redraw only when the open chapter has linked words (Old Testament).
                    if (edition != null && edition.Chapters.TryGetValue((state.Book, state.Chapter), out var open) && open.Any(alignment.Covers)) Refresh(false);
                })));
            if (state.Edition == "BHS-A-Strong" && state.ReferenceSystem != "MT" && edition.Resource?.ReferenceSystem == "MT")
            {
                var migrated = edition.Entries.FirstOrDefault(e => e.KjvReferences.Any(r => r.Book == state.Book && r.Chapter == state.Chapter && r.Verse == (state.Verse ?? 1)));
                if (migrated != null) { state.Book = migrated.Book; state.Chapter = migrated.Chapter; state.Verse = state.Verse.HasValue ? migrated.VerseStart : null; state.EntryIndex = null; state.Scroll = 0; }
                state.ReferenceSystem = "MT"; changed();
            }
            bookIds = Enumerable.Range(0, edition.Books.Count).Where(b => edition.Books[b].Chapters.Count > 0).ToList();
            if (bookIds.Count == 0) throw new InvalidOperationException("Esta edição não contém livros disponíveis.");
            if (!bookIds.Contains(state.Book)) state.Book = bookIds[0];
            state.Chapter = Math.Clamp(state.Chapter, 1, Math.Max(1, edition.Books[state.Book].Chapters.Count));
            updating = true; books.ItemsSource = bookIds.Select(b => edition.Books[b].Name).ToList(); books.SelectedIndex = bookIds.IndexOf(state.Book);
            editions.SelectedItem = library.Editions.FirstOrDefault(e => e.Id == state.Edition); editions.ToolTip = "Edição: " + state.Edition; updating = false;
            UpdateChapters(); Refresh(false);
        }
        catch (Exception ex)
        {
            if (epoch != generation) return;
            title.Text = "Texto indisponível"; subtitle.Text = ex.Message; footer.Text = "Texto indisponível: " + ex.Message;
            viewer.Document = new FlowDocument(new Paragraph(new Run("Escolha outra versão acima ou selecione a pasta da biblioteca."))) { FontSize = 17, PagePadding = new(24) };
        }
        finally { if (epoch == generation) { loading = null; updating = false; } }
    }
    private void UpdateChapters()
    {
        if (edition == null) return;
        updating = true;
        int count = edition.Books[state.Book].Chapters.Count;
        state.Chapter = Math.Clamp(state.Chapter, 1, Math.Max(1, count));
        chapters.ItemsSource = Enumerable.Range(1, count).ToList(); chapters.SelectedItem = state.Chapter; updating = false;
    }
    private void Refresh(bool broadcast)
    {
        if (edition == null || !edition.Chapters.TryGetValue((state.Book, state.Chapter), out var entries)) return;
        if (broadcast) { state.Scroll = 0; currentHit = null; linkedTargets = []; }
        bool valid = entries.All(e => e.ChapterValid);
        bool hebrew = edition.Resource?.Language == "he" || state.Edition is "BHS-A-Strong" or "WLC-OSHB";
        // Keep viewer and document in the same bidi coordinate system. Otherwise
        // WPF mirrors the RTL document inside its LTR host, reversing alignment.
        viewer.FlowDirection = hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        title.TextAlignment = subtitle.TextAlignment = hebrew ? TextAlignment.Right : TextAlignment.Left;
        title.Text = edition.Books[state.Book].Name + " " + state.Chapter;
        subtitle.Text = state.Edition + "   /   " + (state.LinkGroup == "Livre" ? "Leitura independente" : "Grupo " + state.LinkGroup) + "   /   " + entries.Count + " entradas";
        reference.Text = edition.Books[state.Book].Name + " " + state.Chapter + (state.Verse.HasValue ? ":" + state.Verse : "");
        var messages = new List<string>();
        if (!valid) messages.Add("Numeração de capítulos divergente neste livro. A referência abaixo é a posição no arquivo; sincronização suspensa.");
        if (entries.Any(e => !e.VerseStart.HasValue)) messages.Add("Entradas sem número explícito usam §. Não são versículos numerados automaticamente.");
        if (entries.Any(e => e.VerseEnd > e.VerseStart)) messages.Add("Esta tradução reúne versículos em intervalos. Os intervalos originais são preservados.");
        if (edition.Resource?.ReferenceSystem == "MT") messages.Add("Numeração hebraica preservada; acompanhamento por mapa de referências. Superscrições em itálico.");
        warning.Text = string.Join(" ", messages); warning.Visibility = messages.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        clickedWord = null;
        annotations = (state.HideNoteMarkers ? [] : loadNotes?.Invoke() ?? []).Where(n => n.WordAnchor is { } a &&
            a.Edition == state.Edition && a.Book == state.Book && a.Chapter == state.Chapter).ToLookup(n => n.WordAnchor!.Entry);
        paragraphs.Clear(); sourceRuns.Clear();
        var document = new FlowDocument { FontFamily = new("Georgia, Times New Roman"), FontSize = Math.Clamp(state.FontSize, 14, 34), PagePadding = new(23, 6, 23, 24), ColumnWidth = double.PositiveInfinity, Background = Theme.Surface, Foreground = MainWindow.Brush("#2D3730"), LineHeight = state.FontSize * 1.7 };
        document.FlowDirection = hebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        document.TextAlignment = TextAlignment.Left;
        foreach (var entry in entries)
        {
            var p = new Paragraph { Margin = new(0, 0, 0, 10), KeepTogether = true, TextAlignment = TextAlignment.Left };
            if (entry.IsSuperscription) p.FontStyle = FontStyles.Italic;
            if (SearchText.HasHebrew(entry.Text) && entry.Text.Count(c => c is >= '\u05D0' and <= '\u05EA') > entry.Text.Count(char.IsLetter) / 3)
            { p.FlowDirection = FlowDirection.RightToLeft; p.TextAlignment = TextAlignment.Left; p.FontFamily = MainWindow.HebrewReadingFont; p.FontSize = state.FontSize + 3; }
            var number = new Hyperlink(new Run(entry.Number + "  ") { FontSize = 11, FontFamily = new("Segoe UI"), Foreground = MainWindow.Green }) { TextDecorations = null, ToolTip = "Marcar esta referência e acompanhar nos leitores do mesmo grupo" };
            number.Click += (_, _) =>
            {
                state.Verse = entry.VerseStart; state.EntryIndex = entry.EntryIndex;
                foreach (var paragraph in paragraphs.Values) paragraph.Background = Brushes.Transparent;
                p.Background = MainWindow.Brush("#F2F1DD");
                reference.Text = entry.Reference; changed();
                if (entry.IsMapped) navigate(state); else status("Esta entrada ainda não tem uma correspondência bíblica validada.");
            };
            p.Inlines.Add(number);
            if (entry.Words.Count > 0) AddOriginalWords(p, entry);
            else AddTextRuns(p.Inlines, entry, 0, entry.Text.Length);
            if (state.EntryIndex == entry.EntryIndex || linkedTargets.Any(v => v.Book == entry.Book && v.Chapter == entry.Chapter && v.EntryIndex == entry.EntryIndex) || (state.EntryIndex == null && state.Verse.HasValue && entry.VerseStart <= state.Verse && entry.VerseEnd >= state.Verse)) p.Background = MainWindow.Brush("#F2F1DD");
            paragraphs[entry.EntryIndex] = p; document.Blocks.Add(p);
        }
        updating = true; viewer.Document = document;
        footer.Text = state.Edition + " · " + (state.LinkGroup == "Livre" ? "Livre" : "Grupo " + state.LinkGroup) + (messages.Count > 0 ? " · ⓘ avisos" : ""); footer.ToolTip = subtitle.Text + "\n" + warning.Text + "\nClique em uma palavra para analisar. Ferramentas na barra; marcações também pelo botão direito.";
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (state.EntryIndex.HasValue && paragraphs.TryGetValue(state.EntryIndex.Value, out var exact)) exact.BringIntoView();
            else if (state.Verse.HasValue)
            {
                var entry = entries.FirstOrDefault(e => e.VerseStart <= state.Verse && e.VerseEnd >= state.Verse);
                if (entry != null && paragraphs.TryGetValue(entry.EntryIndex, out var selected)) selected.BringIntoView();
            }
            else FindScroll(viewer)?.ScrollToVerticalOffset(state.Scroll);
            updating = false;
        }));
        if (broadcast) { changed(); navigate(state); }
    }
    private void JumpReference()
    {
        if (edition == null) return;
        if (!BibleLibrary.TryReference(edition, reference.Text, out int book, out int chapter, out int? verse)) { status("Referência não encontrada ou não mapeada. Use, por exemplo, João 3:16 ou Gn 1."); return; }
        state.Book = book; state.Chapter = chapter; state.Verse = verse; state.EntryIndex = null;
        updating = true; books.SelectedIndex = bookIds.IndexOf(book); updating = false; UpdateChapters(); Refresh(true);
    }
    private void ChangeChapter(int delta)
    {
        if (edition == null) return;
        int book = state.Book, chapter = state.Chapter + delta;
        if (chapter < 1) { int index = bookIds.IndexOf(book); if (index <= 0) return; book = bookIds[index - 1]; chapter = edition.Books[book].Chapters.Count; }
        if (chapter > edition.Books[book].Chapters.Count) { int index = bookIds.IndexOf(book); if (index + 1 >= bookIds.Count) return; book = bookIds[index + 1]; chapter = 1; }
        state.Book = book; state.Chapter = chapter; state.Verse = null; state.EntryIndex = null;
        updating = true; books.SelectedIndex = bookIds.IndexOf(book); updating = false; UpdateChapters(); Refresh(true);
    }
    private void ResizeText(int change) { state.FontSize = Math.Clamp(state.FontSize + change, 14, 34); Refresh(false); changed(); }
    public async Task NavigateLinked(int book, int chapter, int? verse, BibleEdition? source = null)
    {
        if (edition == null) { if (loading != null) await loading; else await Load(); }
        if (edition == null) return;
        string? mappingMessage = null;
        linkedTargets = [];
        if (source != null)
        {
            var from = verse.HasValue ? BibleLibrary.ResolveVerse(source, book, chapter, verse.Value) : source.Chapters.GetValueOrDefault((book, chapter))?.FirstOrDefault(v => !v.IsSuperscription);
            if (from == null) { status("Referência de origem sem correspondência validada."); return; }
            var link = BibleLibrary.ResolveLinks(source, from, edition);
            if (link.Targets.Count == 0) { status(link.Description); return; }
            linkedTargets = link.Targets; var first = link.Targets[0]; book = first.Book; chapter = first.Chapter; verse = first.VerseStart;
            mappingMessage = link.Description;
        }
        if (!edition.Chapters.TryGetValue((book, chapter), out var entries) || !entries.All(e => e.ChapterValid))
        { status(state.Edition + ": esta passagem não pode ser sincronizada antes da revisão da numeração."); return; }
        if (verse.HasValue && BibleLibrary.ResolveVerse(edition, book, chapter, verse.Value) == null)
        { status(state.Edition + $": versículo {verse} sem correspondência explícita. Leitura mantida na posição anterior."); return; }
        state.Book = book; state.Chapter = chapter; state.Verse = verse; state.EntryIndex = null; state.Scroll = 0; currentHit = null;
        updating = true; books.SelectedIndex = bookIds.IndexOf(book); updating = false; UpdateChapters(); Refresh(false); changed();
        if (mappingMessage != null) status(mappingMessage);
    }
    public async Task ShowHit(SearchHit hit)
    {
        if (edition == null) { if (loading != null) await loading; else await Load(); }
        currentHit = hit; Refresh(false);
    }
    private void ShowVariants()
    {
        if (edition == null || !edition.Chapters.TryGetValue((state.Book, state.Chapter), out var entries)) return;
        var selected = entries.FirstOrDefault(e => e.EntryIndex == state.EntryIndex)
            ?? entries.FirstOrDefault(e => state.Verse.HasValue && e.VerseStart <= state.Verse && e.VerseEnd >= state.Verse)
            ?? entries.FirstOrDefault(e => e.Apparatus.Count > 0) ?? entries.FirstOrDefault();
        if (selected != null) openVariants(selected);
    }
    private void AddOriginalWords(Paragraph paragraph, VerseEntry verse)
    {
        for (int i = 0; i < verse.Words.Count; i++)
        {
            var word = verse.Words[i];
            bool highlighted = currentHit?.Verse.EntryIndex == verse.EntryIndex && currentHit.Verse.Book == verse.Book && currentHit.Verse.Chapter == verse.Chapter && currentHit.Highlights.Any(h => h.Start < word.Start + word.Text.Length && h.Start + h.Length > word.Start);
            if (state.ShowInterlinear)
            {
                var box = new StackPanel { Margin = new(3, 3, 3, 9), FlowDirection = FlowDirection.LeftToRight, Background = MainWindow.Paper };
                var button = MainWindow.Button(word.Text, () => SelectWord(verse, word)); button.ToolTip = " "; button.ToolTipOpening += (_, _) => { if (button.ToolTip is not System.Windows.Controls.ToolTip) button.ToolTip = ParsingTip(verse, word); }; ToolTipService.SetInitialShowDelay(button, 300); ToolTipService.SetShowDuration(button, 30000); button.FontFamily = paragraph.FontFamily; button.FontSize = paragraph.FontSize; var marker = library.Markers.For(verse).FirstOrDefault(m => m.Kind == "background" && m.Start < word.Start + word.Text.Length && m.Start + m.Length > word.Start); button.Background = marker != null ? Theme.Raw(marker.Color) : highlighted ? MainWindow.Brush("#F6E4A1") : Brushes.Transparent; var ink = library.Markers.For(verse).FirstOrDefault(m => m.Kind == "foreground" && m.Start < word.Start + word.Text.Length && m.Start + m.Length > word.Start); if (ink != null) button.Foreground = Theme.Raw(ink.Color); button.Padding = new(4, 2, 4, 2); box.Children.Add(button);
                button.ContextMenu = new ContextMenu();
                var annotate = new MenuItem { Header = "Criar nota nesta palavra…" };
                annotate.Click += (_, _) => createNote?.Invoke(WordAnnotation.Create(verse, word.Start, word.Start + word.Text.Length));
                button.ContextMenu.Items.Add(annotate);
                foreach (var note in annotations[verse.EntryIndex].Where(n => n.WordAnchor!.Matches(verse) && n.WordAnchor.Start + n.WordAnchor.Text.Length > word.Start && n.WordAnchor.Start + n.WordAnchor.Text.Length <= word.Start + word.Text.Length))
                    box.Children.Add(MainWindow.Button("✎", () => openNote?.Invoke(note), note.NoteTitle));
                box.Children.Add(new TextBlock { Text = word.Lemma, FontSize = 15, TextAlignment = TextAlignment.Center });
                box.Children.Add(new TextBlock { Text = string.Join(" + ", word.Strongs), FontSize = 10, Foreground = MainWindow.Green, TextAlignment = TextAlignment.Center });
                if (word.Morphology.Length > 0) box.Children.Add(new TextBlock { Text = word.Morphology, FontSize = 9, Foreground = MainWindow.Muted, TextAlignment = TextAlignment.Center });
                paragraph.Inlines.Add(new InlineUIContainer(box) { BaselineAlignment = BaselineAlignment.Center });
            }
            else
            {
                AddTextRuns(paragraph.Inlines, verse, word.Start, word.Text.Length, word);
                int end = word.Start + word.Text.Length, next = i + 1 < verse.Words.Count ? verse.Words[i + 1].Start : verse.Text.Length;
                if (next > end) AddTextRuns(paragraph.Inlines, verse, end, next - end);
            }
        }
    }
    private ToolTip ParsingTip(VerseEntry verse, OriginalWord word) => WordTip.ForOriginal(library, edition, verse, word);
    private async void AnalyzeOriginal()
    {
        try
        {
            if (alignment == null || clickedWord is not { } clicked) { status("Clique em uma palavra com termo original ligado. Esta tradução pode não ter alinhamento gerado."); return; }
            var term = alignment.Originals(clicked.Verse, clicked.Start, clicked.End - clicked.Start).FirstOrDefault();
            if (term == null) { status("Sem termo original ligado a esta palavra pelo alinhamento automático."); return; }
            status("Abrindo " + alignment.File.Source + "…");
            var source = await library.LoadAsync(alignment.File.Source);
            var verse = BibleLibrary.ResolveVerse(source, term.Verse.B, term.Verse.C, term.Verse.V);
            if (verse == null || term.WordIndex >= verse.Words.Count || verse.Words[term.WordIndex].Text != term.Word.Text) { status("O texto original local difere do alinhamento; gere o alinhamento novamente."); return; }
            status(clicked.Verse.Text[clicked.Start..clicked.End] + " → " + term.Word.Text + " · " + verse.Reference + " · " + TranslationAlignment.ConfidenceLabel(term.Confidence) + " (alinhamento automático)");
            openWord(verse, verse.Words[term.WordIndex]);
        }
        catch (Exception ex) { status("Não foi possível abrir o termo original: " + ex.Message); }
    }
    private void AddTextRuns(InlineCollection inlines, VerseEntry verse, int start, int length, OriginalWord? word = null)
    {
        int end = start + length; var markers = library.Markers.For(verse);
        var hits = currentHit?.Verse == verse ? currentHit.Highlights : [];
        var cuts = new SortedSet<int> { start, end };
        foreach (var m in markers) { if (m.Start > start && m.Start < end) cuts.Add(m.Start); if (m.Start + m.Length > start && m.Start + m.Length < end) cuts.Add(m.Start + m.Length); }
        foreach (var h in hits) { if (h.Start > start && h.Start < end) cuts.Add(h.Start); if (h.Start + h.Length > start && h.Start + h.Length < end) cuts.Add(h.Start + h.Length); }
        var tokens = SearchText.Tokenize(verse.Text.Substring(start, length));
        foreach (var t in tokens) { cuts.Add(start + t.Start); cuts.Add(start + t.Start + t.Length); }
        var byEnd = annotations[verse.EntryIndex].Where(n => n.WordAnchor!.Matches(verse)).ToLookup(n => n.WordAnchor!.Start + n.WordAnchor.Text.Length);
        foreach (var group in byEnd) if (group.Key > start && group.Key < end) cuts.Add(group.Key);
        var offsets = cuts.ToArray();
        Hyperlink? wordLink = null;
        for (int i = 0; i + 1 < offsets.Length; i++)
        {
            int a = offsets[i], b = offsets[i + 1]; bool hit = hits.Any(h => h.Start < b && h.Start + h.Length > a);
            var marker = markers.FirstOrDefault(m => m.Kind == "background" && m.Start < b && m.Start + m.Length > a);
            var run = new Run(verse.Text[a..b]) { Background = marker != null ? Theme.Raw(marker.Color) : hit ? MainWindow.Brush("#F6E4A1") : Brushes.Transparent, FontWeight = hit ? FontWeights.Bold : FontWeights.Normal, FontStyle = a < verse.SuperscriptionPrefixLength ? FontStyles.Italic : FontStyles.Normal };
            var ink = markers.FirstOrDefault(m => m.Kind == "foreground" && m.Start < b && m.Start + m.Length > a);
            if (ink != null) run.Foreground = Theme.Raw(ink.Color);
            if (word == null) inlines.Add(run);
            else
            {
                if (wordLink == null)
                {
                    // The card is built on first hover; a chapter has hundreds of words.
                    var link = wordLink = new Hyperlink { Foreground = MainWindow.Brush("#2D3730"), TextDecorations = null, ToolTip = " " };
                    link.ToolTipOpening += (_, _) => { if (link.ToolTip is not System.Windows.Controls.ToolTip) link.ToolTip = ParsingTip(verse, word); };
                    ToolTipService.SetInitialShowDelay(wordLink, 300); ToolTipService.SetShowDuration(wordLink, 30000);
                    wordLink.Click += (_, _) => SelectWord(verse, word);
                    inlines.Add(wordLink);
                }
                wordLink.Inlines.Add(run);
            }
            sourceRuns[run] = (verse, a);
            if (alignment is { } linked && verse.Words.Count == 0 && run.Text.Any(char.IsLetterOrDigit) && linked.Originals(verse, a, b - a).Count > 0)
            {
                // Built on first hover: a chapter has hundreds of linked words.
                int from = a; run.ToolTip = " "; ToolTipService.SetInitialShowDelay(run, 350); ToolTipService.SetShowDuration(run, 30000);
                run.ToolTipOpening += (_, _) =>
                {
                    if (run.ToolTip is ToolTip || WordAt(verse, from) is not { } token) return;
                    run.ToolTip = WordTip.ForTranslation(library, linked, verse.Text[token.Start..token.End], linked.Originals(verse, token.Start, token.End - token.Start));
                };
            }
            if (verse.Words.Count == 0 && run.Text.Any(char.IsLetterOrDigit))
                run.MouseLeftButtonUp += (_, e) =>
                {
                    if (viewer.Selection?.IsEmpty == false) return;
                    clickedWord = WordAt(verse, a);
                    if (clickedWord == null) return;
                    viewer.ContextMenu!.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    viewer.ContextMenu.IsOpen = true; e.Handled = true;
                };
            foreach (var note in byEnd[b])
            {
                var markerLink = new Hyperlink(new Run("✎")) { FontSize = 11, FontFamily = new("Segoe UI Symbol"), BaselineAlignment = BaselineAlignment.Superscript, ToolTip = note.NoteTitle, Foreground = MainWindow.Green };
                markerLink.Click += (_, e) => { e.Handled = true; openNote?.Invoke(note); };
                inlines.Add(markerLink); wordLink = null;
            }
        }
    }
    private static (VerseEntry Verse, int Start, int End)? WordAt(VerseEntry verse, int offset)
    {
        var token = SearchText.Tokenize(verse.Text).FirstOrDefault(t => t.Start <= offset && offset < t.Start + t.Length);
        return token == null ? null : (verse, token.Start, token.Start + token.Length);
    }

    private void Annotate()
    {
        var target = clickedWord;
        if (viewer.Selection is { IsEmpty: false } selection)
        {
            var ranges = SelectedRanges(selection);
            if (ranges.Count == 0 || ranges.Any(r => r.Verse != ranges[0].Verse)) { status("Selecione palavras de um único versículo."); return; }
            var verse = ranges[0].Verse;
            int first = ranges.Min(r => r.Start), last = ranges.Max(r => r.Start + r.Length);
            var words = SearchText.Tokenize(verse.Text).Where(t => t.Start < last && t.Start + t.Length > first).ToList();
            if (words.Count == 0) { status("Selecione uma palavra do versículo."); return; }
            target = (verse, words[0].Start, words[^1].Start + words[^1].Length);
        }
        if (target is { } t) createNote?.Invoke(WordAnnotation.Create(t.Verse, t.Start, t.End));
        else status("Clique em uma palavra ou selecione um trecho para criar a nota.");
    }

    private void MarkSelection(string? color, string kind = "background")
    {
        var selection = viewer.Selection;
        if (selection == null || selection.IsEmpty) { status("Selecione um trecho no texto de leitura para aplicar o marcador. No interlinear, volte ao modo de leitura com ≡."); return; }
        var ranges = SelectedRanges(selection);
        if (ranges.Count == 0) { status("Selecione palavras do texto bíblico para marcar."); return; }
        try { library.Markers.Apply(ranges, color, kind); Refresh(false); changed(); status(color == null ? "Cor removida da seleção." : "Trecho marcado e salvo."); }
        catch (Exception ex) { status("Não foi possível salvar o marcador: " + ex.Message); }
    }
    private List<(VerseEntry Verse, int Start, int Length)> SelectedRanges(TextSelection selection)
    {
        var ranges = new List<(VerseEntry Verse, int Start, int Length)>();
        foreach (var (run, source) in sourceRuns)
        {
            if (selection.Start.CompareTo(run.ContentEnd) >= 0 || selection.End.CompareTo(run.ContentStart) <= 0) continue;
            var a = selection.Start.CompareTo(run.ContentStart) > 0 ? selection.Start : run.ContentStart;
            var b = selection.End.CompareTo(run.ContentEnd) < 0 ? selection.End : run.ContentEnd;
            int start = source.Start + new TextRange(run.ContentStart, a).Text.Length, length = new TextRange(a, b).Text.Length;
            if (length > 0) ranges.Add((source.Verse, start, length));
        }
        return ranges;
    }
    private void SelectWord(VerseEntry verse, OriginalWord word)
    {
        state.SelectedWord = word;
        state.Verse = verse.VerseStart; state.EntryIndex = verse.EntryIndex;
        reference.Text = verse.Reference;
        changed(); openWord(verse, word);
    }
    private static ScrollViewer? FindScroll(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer s) return s;
            var nested = FindScroll(child); if (nested != null) return nested;
        }
        return null;
    }
}
