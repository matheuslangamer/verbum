using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Verbum;

public sealed class MainWindow : Window
{
    internal static readonly FontFamily HebrewReadingFont = new("SBL Hebrew, Ezra SIL, Times New Roman");
    internal static Brush Green => Brush("#386754");
    internal static Brush Muted => Brush("#768379");
    internal static Brush Paper => Brush("#FAFAF5");
    internal static Brush Line => Brush("#DCE2D8");
    internal static Brush Brush(string hex) => Theme.Get(hex);
    private BibleLibrary library;
    private SearchEngine search;
    private DocumentLibrary documents;
    private readonly CheckBox searchDocuments = new() { Content = "Pesquisar também nos PDFs e livros", Margin = new(4, 7, 0, 0), ToolTip = "Inclui páginas e seções com texto extraído. Ignora acentos e sinais no modo inteligente; não faz busca semântica nem OCR." };
    private readonly Expander documentResults = new() { Header = "PDFs e livros", Visibility = Visibility.Collapsed, Margin = new(12, 3, 12, 6) };
    private readonly StackPanel documentHits = new();
    private LayoutNode layout;
    private string? activePane;
    private List<TabState>? readerNotes;
    private List<TabState> NotesForReaders() => readerNotes ??= LoadNotes();
    private readonly Dictionary<string, ReaderView> readers = [];
    private readonly Dictionary<string, TabControl> controls = [];
    private readonly ContentControl workspace = new();
    private readonly TextBlock status = new() { Text = "Biblioteca local · leitura offline", FontSize = 11, Foreground = Muted, Margin = new(10, 3, 10, 3) };
    private readonly TextBox query = new() { MinWidth = 150, FontSize = 16, ToolTip = "Digite palavras em português, hebraico ou grego. Enter pesquisa. Ctrl+F foca este campo." };
    private readonly ComboBox mode = new() { Width = 182 }, scope = new() { Width = 160 };
    private readonly StackPanel variants = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock searchSummary = new() { FontSize = 15, FontWeight = FontWeights.SemiBold }, searchDetails = new() { Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly ListBox results = new();
    private readonly Grid center = new();
    private readonly RowDefinition searchRow = new() { Height = new(0) };
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private CancellationTokenSource? searching;
    private SearchResponse? response;
    private readonly ComboBox bookFilter = new() { MinWidth = 210, MaxWidth = 310, ToolTip = "Filtrar ocorrências por livro ou testamento" };
    private List<SearchHit> filteredHits = [];
    private string searchedText = "";
    private string searchedKey = "";
    private string? parallelEvidencePhrase;
    private readonly ComboBox parallelTranslation = new() { Width = 145, ToolTip = "Tradução paralela das ocorrências" };
    private readonly WrapPanel translationSummary = new() { Margin = new(12, 3, 12, 5) };
    private readonly Grid occurrenceHeader = new();
    private readonly Expander translationExpander = new() { Header = "Traduções marcadas", IsExpanded = false, Margin = new(12, 2, 12, 3) };
    private readonly TranslationLinks translationLinks;
    private BibleEdition? parallelEdition;
    private int parallelGeneration;
    private int shown;
    private bool building;
    private string? draggedTab;
    private Point dragOrigin;
    private UIElement? dragSource;
    private readonly Button more;
    private StudySession resumeCheckpoint;
    private bool closePromptActive;
    private bool exitCommitted;
    private readonly bool previewMode;
    private bool sidebarCollapsed;
    private readonly ColumnDefinition sidebarColumn = new();
    private readonly List<(Button Button, string Icon, string Label)> sidebarButtons = [];
    private TextBlock? sidebarFooter;

    public MainWindow(string path, StudySession? previewSession = null, bool previewMode = false)
    {
        this.previewMode = previewMode;
        library = new(path); search = new(library);
        documents = new(path);
        try { translationLinks = new(Path.Combine(SessionStore.DataDirectory, "translation-links.json")); }
        catch (Exception ex) { translationLinks = new(Path.Combine(SessionStore.DataDirectory, "translation-links-recovered.json")); SetStatus("Marcações anteriores indisponíveis; arquivo original preservado: " + ex.Message); }
        var session = previewSession ?? SessionStore.Load();
        layout = session?.Layout ?? LayoutNode.Default(library.Editions);
        NoteFiles.RemoveTrashedTabs(layout);
        activePane = layout.Leaves().First().Id;
        sidebarCollapsed = session?.SidebarCollapsed ?? true;
        Width = Math.Clamp(session?.Width ?? 1480, 1050, 2200); Height = Math.Clamp(session?.Height ?? 960, 720, 1500);
        resumeCheckpoint = SessionExit.Clone(CaptureSession());
        MinWidth = 1050; MinHeight = 700; Title = "VERBUM " + typeof(MainWindow).Assembly.GetName().Version?.ToString(3) + " · Área de estudo bíblico";
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var root = new Grid { Background = Brush("#F4F4EF") };
        root.RowDefinitions.Add(new() { Height = new(46) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = root;

        var header = new Grid { Background = Brush("#F4F4EF") };
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(Icons.Create("book-open", 23)); brand.Children.Add(new TextBlock { Text = "VERBUM", FontFamily = new("Georgia"), FontSize = 23, Foreground = Green, Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }); header.Children.Add(brand);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) };
        actions.Children.Add(Button("+ Bloco", () => Split(ActivePane(), "horizontal"))); actions.Children.Add(Button("Originais", ShowOriginals)); actions.Children.Add(Button("+ Notas", () => AddTab(new() { Kind = "notes", NoteCreated = DateTimeOffset.Now }))); actions.Children.Add(Button("Salvar área", SaveNow));
        foreach (Button button in actions.Children) { button.Height = 29; button.Padding = new(9, 3, 9, 3); button.Margin = new(3, 0, 0, 0); }
        Grid.SetColumn(actions, 2); header.Children.Add(actions); root.Children.Add(header);
        var searchInputs = new Grid { Margin = new(18, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }; searchInputs.ColumnDefinitions.Add(new()); searchInputs.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); searchInputs.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        query.FontSize = 14; query.Height = 30; query.Padding = new(8, 3, 8, 3); query.ToolTip = "Pesquisar palavras · Ctrl+F · Enter para buscar";
        query.KeyDown += (_, e) => { if (e.Key == Key.Enter) { _ = RunSearch(); e.Handled = true; } };
        query.TextChanged += (_, _) => { if (query.Text.Length == 0) query.ToolTip = "Ex.: coração, principio, שלום ou λόγος. O texto pesquisado precisa existir na biblioteca."; }; searchInputs.Children.Add(query);
        mode.ItemsSource = new[] { "Inteligente · sem sinais", "Exata · com sinais", "Começa com", "Trecho de palavra", "Código Strong", "Lema da fonte" }; mode.SelectedIndex = 0; mode.ToolTip = "Modo de pesquisa";
        var details = new WrapPanel { Margin = new(10, 0, 10, 6), Visibility = Visibility.Collapsed }; mode.Height = scope.Height = 28; mode.MinHeight = scope.MinHeight = 28; mode.Padding = scope.Padding = new(5, 2, 5, 2); details.Children.Add(mode); details.Children.Add(scope); details.Children.Add(searchDocuments);
        var optionsButton = Button("Opções ▾", () => details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "Modo de pesquisa, escopo e busca nos documentos"); optionsButton.Height = 30; optionsButton.Padding = new(9, 3, 9, 3); optionsButton.Margin = new(5, 0, 0, 0); Grid.SetColumn(optionsButton, 1); searchInputs.Children.Add(optionsButton);
        void SearchSummary()
        {
            optionsButton.Content = (mode.SelectedIndex switch { 1 => "Exata", 2 => "Prefixo", 3 => "Trecho", 4 => "Strong", 5 => "Lema", _ => "Inteligente" }) + " ▾";
            optionsButton.ToolTip = "Opções da pesquisa\nModo: " + mode.SelectedItem + "\nEscopo: " + scope.SelectedItem + (searchDocuments.IsChecked == true ? "\nInclui livros e PDFs" : "");
        }
        mode.SelectionChanged += (_, _) => SearchSummary(); scope.SelectionChanged += (_, _) => SearchSummary(); searchDocuments.Checked += (_, _) => SearchSummary(); searchDocuments.Unchecked += (_, _) => SearchSummary(); SearchSummary();
        var searchButton = Button("Pesquisar ↵", () => _ = RunSearch()); searchButton.Background = Theme.Get("Primary"); searchButton.Foreground = Theme.Get("OnPrimary"); searchButton.Height = 30; searchButton.Padding = new(10, 3, 10, 3); searchButton.Margin = new(5, 0, 0, 0); Grid.SetColumn(searchButton, 2); searchInputs.Children.Add(searchButton);
        Grid.SetColumn(searchInputs, 1); header.Children.Add(searchInputs); Grid.SetRow(details, 1); root.Children.Add(details);
        var body = new Grid(); sidebarColumn.Width = new(sidebarCollapsed ? 52 : 190); body.ColumnDefinitions.Add(sidebarColumn); body.ColumnDefinitions.Add(new());
        var sidebar = BuildSidebar(); body.Children.Add(sidebar);
        center.RowDefinitions.Add(new() { Height = new(1, GridUnitType.Star) });
        center.RowDefinitions.Add(new() { Height = new(5) });
        center.RowDefinitions.Add(searchRow);
        workspace.Margin = new(6, 5, 6, 0); center.Children.Add(workspace);
        var resultSplitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = Line, ResizeDirection = GridResizeDirection.Rows };
        Grid.SetRow(resultSplitter, 1); center.Children.Add(resultSplitter);
        var searchPanel = new Grid { Background = Theme.Surface, Margin = new(10, 0, 10, 0) };
        searchPanel.RowDefinitions.Add(new() { Height = GridLength.Auto }); searchPanel.RowDefinitions.Add(new() { Height = GridLength.Auto }); searchPanel.RowDefinitions.Add(new() { Height = GridLength.Auto }); searchPanel.RowDefinitions.Add(new()); searchPanel.RowDefinitions.Add(new() { Height = GridLength.Auto }); searchPanel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var sh = new DockPanel { Margin = new(12, 8, 10, 4) };
        var closeSearch = Button("Fechar ×", () => searchRow.Height = new(0)); DockPanel.SetDock(closeSearch, Dock.Right); sh.Children.Add(closeSearch);
        bookFilter.SelectionChanged += (_, _) => ApplyResultFilter();
        DockPanel.SetDock(bookFilter, Dock.Right); sh.Children.Add(bookFilter);
        parallelTranslation.SelectionChanged += (_, _) => _ = LoadParallelTranslation();
        DockPanel.SetDock(parallelTranslation, Dock.Right); sh.Children.Add(parallelTranslation);
        var sc = new StackPanel(); sc.Children.Add(searchSummary); sc.Children.Add(searchDetails); sh.Children.Add(sc); searchPanel.Children.Add(sh);
        var variantScroll = new ScrollViewer { Content = variants, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new(10, 0, 10, 4), MaxHeight = 70 };
        var spellingsExpander = new Expander { Header = "Grafias encontradas", Content = variantScroll, IsExpanded = false, Margin = new(12, 0, 12, 2) };
        Grid.SetRow(spellingsExpander, 1); searchPanel.Children.Add(spellingsExpander);
        results.SelectionChanged += (_, _) => { if (results.SelectedItem is ListBoxItem { Tag: SearchHit hit }) _ = OpenHit(hit); };
        translationExpander.Content = translationSummary; Grid.SetRow(translationExpander, 2); searchPanel.Children.Add(translationExpander);
        var occurrenceTable = new DockPanel(); DockPanel.SetDock(occurrenceHeader, Dock.Top); occurrenceTable.Children.Add(occurrenceHeader); occurrenceTable.Children.Add(results);
        Grid.SetRow(occurrenceTable, 3); searchPanel.Children.Add(occurrenceTable);
        documentResults.Content = new ScrollViewer { Content = documentHits, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 190 }; Grid.SetRow(documentResults, 4); searchPanel.Children.Add(documentResults);
        more = Button("Mostrar mais resultados", MoreResults); more.Visibility = Visibility.Collapsed; Grid.SetRow(more, 5); searchPanel.Children.Add(more);
        Grid.SetRow(searchPanel, 2); center.Children.Add(searchPanel);
        Grid.SetColumn(center, 1); body.Children.Add(center); Grid.SetRow(body, 2); root.Children.Add(body);
        Grid.SetRow(status, 3); root.Children.Add(status);

        saveTimer.Tick += (_, _) => { saveTimer.Stop(); if (!exitCommitted) SaveNow(false); };
        Closing += ConfirmExit;
        PreviewKeyDown += (_, e) =>
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.F) { query.Focus(); query.SelectAll(); e.Handled = true; }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.S) { SaveNow(); e.Handled = true; }
        };
        PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || draggedTab == null || dragSource == null || (e.GetPosition(this) - dragOrigin).Length <= 12) return;
            string id = draggedTab; var source = dragSource; draggedTab = null; dragSource = null;
            SetStatus("Arraste ao centro para integrar ou às bordas para criar um bloco. Esc cancela.");
            DragDrop.DoDragDrop(source, new DataObject("VerbumTab", id), DragDropEffects.Move);
        };
        PreviewMouseLeftButtonUp += (_, _) => { draggedTab = null; dragSource = null; };
        RefreshCatalog(); RenderWorkspace();
        if (library.Editions.Count == 0) SetStatus("Nenhuma Bíblia encontrada. Clique em ‘Pasta da biblioteca’ para escolher seus JSONs.");
    }

    internal static Button Button(string text, Action action, string? tip = null)
    {
        var b = new Button { Content = text, ToolTip = tip }; b.Click += (_, _) => action(); return b;
    }
    internal static TextBlock Label(string text, double size = 13, Brush? color = null) => new() { Text = text, FontSize = size, Foreground = color ?? Brush("#24332F"), TextWrapping = TextWrapping.Wrap };
    private FrameworkElement BuildSidebar()
    {
        var panel = new DockPanel { Background = Brush("#EAEDE5") }; var top = new StackPanel { Margin = new(5, 7, 5, 7) };
        var toggle = Button("☰", () => { sidebarCollapsed = !sidebarCollapsed; UpdateSidebar(); Changed(); }, "Expandir / recolher ferramentas"); toggle.Content = Icons.Create("panel-left"); toggle.Height = 30; toggle.Padding = new(6, 2, 6, 2); top.Children.Add(toggle);
        foreach (var item in new (string Icon, string Label, Action Action)[] { ("book-open", "Bíblias ▾", ShowLibrary), ("library", "Biblioteca", ShowDocuments), ("notebook-pen", "Notas", ShowSavedNotes), ("layers", "Estudo da palavra", ShowWordStudy), ("languages", "Léxico hebraico", ShowHebrewLexicon), ("columns-3", "Guia exegético", () => _ = ShowExegeticalGuide()) })
        {
            var button = Button(item.Label, item.Action, item.Label); button.Height = 36; button.Padding = new(6, 4, 6, 4); button.Margin = new(0, 4, 0, 0); button.HorizontalContentAlignment = HorizontalAlignment.Left; sidebarButtons.Add((button, item.Icon, item.Label)); top.Children.Add(button);
        }
        DockPanel.SetDock(top, Dock.Top); panel.Children.Add(top);
        var themeButton = Button("", () => { }, "Alternar modo claro / escuro"); themeButton.Height = 34; themeButton.Margin = new(5, 4, 5, 10);
        void ThemeIcon() { themeButton.Content = Icons.Create(Theme.IsDark ? "sun" : "moon"); themeButton.ToolTip = Theme.IsDark ? "Usar modo claro" : "Usar modo escuro"; }
        themeButton.Click += (_, _) => { Theme.Apply(!Theme.IsDark); ThemeIcon(); try { if (!previewMode) Theme.Save(); } catch (Exception ex) { SetStatus("Tema alterado; não foi possível salvar preferência: " + ex.Message); } };
        ThemeIcon(); DockPanel.SetDock(themeButton, Dock.Bottom); panel.Children.Add(themeButton);
        sidebarFooter = Label("LOCAL E OFFLINE", 9, Muted); sidebarFooter.Margin = new(8); DockPanel.SetDock(sidebarFooter, Dock.Bottom); panel.Children.Add(sidebarFooter); UpdateSidebar(); return panel;
    }
    private void UpdateSidebar()
    {
        sidebarColumn.Width = new(sidebarCollapsed ? 52 : 190);
        foreach (var item in sidebarButtons) { item.Button.Content = Icons.Label(item.Icon, sidebarCollapsed ? null : item.Label); item.Button.FontSize = sidebarCollapsed ? 17 : 12; item.Button.HorizontalContentAlignment = sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left; }
        if (sidebarFooter != null) sidebarFooter.Visibility = sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ShowLibrary()
    {
        var menu = new ContextMenu { MaxHeight = Math.Max(300, ActualHeight - 150) };
        foreach (var edition in library.Editions)
        {
            var item = new MenuItem { Header = edition.Id }; item.Click += (_, _) => AddTab(new() { Edition = edition.Id, ReferenceSystem = edition.Id == "BHS-A-Strong" ? "MT" : "" }); menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var folder = new MenuItem { Header = "Pasta da biblioteca…" }; folder.Click += (_, _) => ChooseLibrary(); menu.Items.Add(folder);
        var notes = new MenuItem { Header = "Minhas notas" }; notes.Click += (_, _) => ShowSavedNotes(); menu.Items.Add(notes);
        var help = new MenuItem { Header = "Como funciona a busca" }; help.Click += (_, _) => SearchHelp(); menu.Items.Add(help);
        menu.IsOpen = true;
    }
    private void ShowDocuments()
    {
        var pane = ActivePane(); var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "library");
        if (existing == null) AddTab(new() { Kind = "library" }); else { pane.ActiveTab = existing.Id; RenderWorkspace(); Changed(); }
    }
    private void OpenDocument(ReadingDocument document, int section, string term)
    {
        var pane = ActivePane(); var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "book" && t.DocumentId == document.Id);
        if (existing == null) AddTab(new() { Kind = "book", DocumentId = document.Id, DocumentTitle = document.Title, DocumentSection = section, DocumentQuery = term });
        else { existing.DocumentSection = section; existing.DocumentQuery = term; pane.ActiveTab = existing.Id; RenderWorkspace(); Changed(); }
    }
    private async Task ShowExegeticalGuide()
    {
        var pane = ActivePane(); var current = pane.Tabs.FirstOrDefault(t => t.Id == pane.ActiveTab);
        if (current?.Kind == "exegesis") return;
        string original = current?.Book >= 39 && current?.Kind is "bible" or "analysis" or "apparatus" ? "SBLGNT" : "WLC-OSHB";
        if (!library.Editions.Any(e => e.Id == original)) { SetStatus("Texto original indisponível para o guia."); return; }
        try
        {
            var edition = await library.LoadAsync(original); int book = original == "SBLGNT" ? 42 : 28, chapter = 1; int? verse = null;
            if (current?.Kind is "bible" or "analysis" or "apparatus")
            {
                var source = await library.LoadAsync(current.Edition);
                var entry = BibleLibrary.ResolveVerse(source, current.Book, current.Chapter, current.Verse ?? 1);
                var mapped = entry != null ? BibleLibrary.ResolveLinks(source, entry, edition).Targets : [];
                if (mapped.Count == 0) { SetStatus("Não há correspondência explícita da passagem atual no original. Abra uma referência válida no leitor para gerar o guia."); return; }
                book = mapped[0].Book; chapter = mapped[0].Chapter; verse = current.Verse.HasValue ? mapped[0].VerseStart : null;
            }
            string text = edition.Books[book].Name + " " + chapter + (verse.HasValue ? ":" + verse : current?.Kind is "bible" or "analysis" or "apparatus" ? "" : ":1-3");
            // A new guide is a regular tab; existing panes and reading tabs retain their state.
            activePane = pane.Id; AddTab(new() { Kind = "exegesis", Edition = original, Book = book, Chapter = chapter, Verse = verse, GuideReference = text, ReferenceSystem = edition.Resource?.ReferenceSystem ?? "", StudyTranslation = current?.Kind == "bible" && current.Edition is not ("WLC-OSHB" or "BHS-A-Strong" or "SBLGNT") ? current.Edition : "ARA" });
        }
        catch (Exception ex) { SetStatus("Não foi possível abrir o guia: " + ex.Message); }
    }
    private void OpenPassageNotes(TabState guide)
    {
        string anchor = ExegesisData.NoteAnchor(guide);
        foreach (var pane in layout.Leaves())
        {
            var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "notes" && t.NoteAnchor == anchor);
            if (existing != null) { activePane = pane.Id; pane.ActiveTab = existing.Id; RenderWorkspace(); Changed(); return; }
        }
        var folder = Path.Combine(SessionStore.DataDirectory, "notes");
        if (Directory.Exists(folder))
        foreach (string path in Directory.EnumerateFiles(folder, "*.json"))
        {
            try { var saved = JsonSerializer.Deserialize<TabState>(File.ReadAllText(path)); if (saved?.Kind == "notes" && saved.NoteAnchor == anchor) { AddTab(saved); return; } } catch { }
        }
        AddTab(new() { Kind = "notes", NoteCreated = DateTimeOffset.Now, Edition = guide.Edition, Book = guide.Book, Chapter = guide.Chapter, Verse = guide.Verse, GuideEndVerse = guide.GuideEndVerse, NoteAnchor = anchor, SourceReference = guide.GuideReference,
            NoteTitle = "Exegese · " + guide.GuideReference, Note = "# " + guide.GuideReference + "\n\nTexto original: " + guide.Edition + "\n\n## Observações\n\n## Análise\n\n## Proposta de tradução\n\n## Fontes\n" });
    }
    private void ShowWordStudy()
    {
        var pane = ActivePane(); var current = pane.Tabs.FirstOrDefault(t => t.Id == pane.ActiveTab);
        var word = current?.SelectedWord;
        string edition = current?.Edition is "BHS-A-Strong" or "WLC-OSHB" or "SBLGNT" ? current.Edition : "BHS-A-Strong";
        string initial = word?.Strongs.FirstOrDefault() is { } strong ? OriginalResource.LexiconKey(strong) : query.Text.Trim();
        var studyMode = initial.Length == 0 || word?.Strongs.Count > 0 ? SearchMode.Strong : (SearchMode)mode.SelectedIndex is SearchMode.Strong or SearchMode.Lemma ? (SearchMode)mode.SelectedIndex : SearchMode.Intelligent;
        var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "wordstudy");
        if (existing != null) { if (current?.Kind != "wordstudy" && initial.Length > 0) { existing.StudyTerm = initial; existing.StudyMode = studyMode; existing.Edition = edition; } pane.ActiveTab = existing.Id; RenderWorkspace(); }
        else AddTab(new() { Kind = "wordstudy", Edition = edition, StudyTerm = initial, StudyMode = studyMode, StudyTranslation = parallelEdition?.Info.Id ?? "ARA" });
        Changed();
    }
    private void ShowHebrewLexicon()
    {
        var pane = ActivePane(); var current = pane.Tabs.FirstOrDefault(t => t.Id == pane.ActiveTab);
        string selected = current?.SelectedWord?.Strongs.FirstOrDefault() is { } code ? OriginalResource.LexiconKey(code) : "";
        var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "lexicon");
        if (existing != null) { if (selected.Length > 0) { existing.LexiconQuery = selected; existing.LexiconCode = selected; } pane.ActiveTab = existing.Id; RenderWorkspace(); Changed(); }
        else AddTab(new() { Kind = "lexicon", LexiconQuery = selected, LexiconCode = selected });
    }
    private void StudyLexeme(LexiconEntry entry, string edition)
    {
        var pane = ActivePane(); var existing = pane.Tabs.FirstOrDefault(t => t.Kind == "wordstudy");
        if (existing == null) AddTab(new() { Kind = "wordstudy", Edition = edition, StudyTerm = entry.Code, StudyMode = SearchMode.Strong, StudyTranslation = parallelEdition?.Info.Id ?? "ARA" });
        else { existing.Edition = edition; existing.StudyTerm = entry.Code; existing.StudyMode = SearchMode.Strong; pane.ActiveTab = existing.Id; RenderWorkspace(); Changed(); }
    }
    private void ShowStudyOccurrences(List<SearchHit> hits, string text, SearchMode selectedMode, string edition, string translation, string filter, string? evidencePhrase)
    {
        parallelEvidencePhrase = evidencePhrase;
        searching?.Cancel(); query.Text = text; mode.SelectedIndex = (int)selectedMode; scope.SelectedItem = edition; searchedText = text; searchedKey = TranslationLinks.Key(text, selectedMode);
        response = new(hits, hits.Count, [], []); searchRow.Height = new(Math.Max(280, ActualHeight * .38));
        searchDetails.Text = "Estudo da palavra · " + filter;
        parallelTranslation.SelectedItem = translation;
        bookFilter.ItemsSource = new[] { new BookFilter("Todos os livros") }.Concat(hits.GroupBy(h => h.Verse.Book).OrderBy(g => g.Key).Select(g => new BookFilter(g.First().Verse.BookName, g.Key))).ToList();
        bookFilter.SelectedIndex = 0; ApplyResultFilter();
    }
    private void RefreshCatalog()
    {
        scope.ItemsSource = new[] { "Aba ativa", "Toda a biblioteca" }.Concat(library.Editions.Select(e => e.Id)).ToList(); scope.SelectedIndex = 0;
        parallelTranslation.ItemsSource = new[] { "Sem paralelo" }.Concat(library.Editions.Where(e => e.Id is not ("BHS-A-Strong" or "WLC-OSHB" or "SBLGNT")).Select(e => e.Id)).ToList();
        parallelTranslation.SelectedItem = library.Editions.Any(e => e.Id == "ARA") ? "ARA" : "Sem paralelo";
    }
    private void ChooseLibrary()
    {
        var dlg = new OpenFolderDialog { Title = "Escolha a pasta que contém os JSONs das Bíblias", InitialDirectory = Directory.Exists(library.DirectoryPath) ? library.DirectoryPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) };
        if (dlg.ShowDialog(this) != true) return;
        SaveNow(false); searching?.Cancel();
        library = new(dlg.FolderName); search = new(library); documents = new(dlg.FolderName); results.Items.Clear(); response = null;
        RefreshCatalog(); RenderWorkspace(); Changed();
        SetStatus($"Biblioteca: {library.Editions.Count} arquivos JSON. {dlg.FolderName}");
    }
    internal void Changed()
    {
        if (building || closePromptActive || exitCommitted) return;
        // Views report their own navigation here; what they show now is what they are kept for.
        foreach (var kept in views.Values) kept.Fingerprint = Fingerprint(kept.State);
        foreach (var control in controls.Values)
            foreach (TabItem item in control.Items)
                if (item.Tag is TabState t && item.Header is StackPanel h && h.Children[0] is TextBlock label)
                    label.Text = TabTitle(t);
        saveTimer.Stop(); if (!previewMode) saveTimer.Start();
    }
    internal void SetStatus(string message) => status.Text = message;
    private LayoutNode ActivePane() => layout.Leaves().FirstOrDefault(p => p.Id == activePane) ?? layout.Leaves().First();
    private TabState? ActiveBible() => ActivePane().Tabs.FirstOrDefault(t => t.Id == ActivePane().ActiveTab && t.Kind == "bible") ?? ActivePane().Tabs.FirstOrDefault(t => t.Kind == "bible");
    private void AddTab(TabState state)
    {
        var pane = ActivePane(); pane.Tabs.Add(state); pane.ActiveTab = state.Id; RenderWorkspace(); Changed();
    }
    private void Split(LayoutNode pane, string direction)
    {
        if (layout.Leaves().Count() >= 8) { SetStatus("Até 8 blocos nesta versão. Feche um bloco antes de dividir novamente."); return; }
        var old = new LayoutNode { Tabs = pane.Tabs, ActiveTab = pane.ActiveTab };
        var source = pane.Tabs.FirstOrDefault(t => t.Kind == "bible");
        var added = new LayoutNode { Tabs = [new() { Edition = source?.Edition ?? library.Editions.FirstOrDefault()?.Id ?? "", Book = source?.Book ?? 0, Chapter = source?.Chapter ?? 1 }] };
        pane.Kind = "split"; pane.Direction = direction; pane.Ratio = .5; pane.Tabs = []; pane.Children = [old, added]; activePane = added.Id;
        RenderWorkspace(); Changed();
    }
    private void ClosePane(LayoutNode pane)
    {
        if (layout.Kind == "pane") { SetStatus("Mantenha pelo menos um bloco aberto."); return; }
        SaveNow(false);
        LayoutNode Remove(LayoutNode node)
        {
            if (node.Kind == "pane") return node;
            if (node.Children.Any(c => c.Id == pane.Id)) return node.Children.First(c => c.Id != pane.Id);
            node.Children = node.Children.Select(Remove).ToList(); return node;
        }
        layout = Remove(layout); activePane = layout.Leaves().First().Id; RenderWorkspace(); Changed();
    }
    internal void RebuildLayout() => RenderWorkspace();
    private void RenderWorkspace()
    {
        building = true; readerNotes = null; noteSignature = null; readers.Clear(); controls.Clear();
        workspace.Content = RenderNode(layout); building = false;
        var open = layout.Leaves().SelectMany(p => p.Tabs).Select(t => t.Id).ToHashSet();
        foreach (string id in views.Keys.Where(id => !open.Contains(id)).ToList()) views.Remove(id);
    }
    // Rebuilding the layout keeps the views whose tab did not change. Recreating every reader,
    // lexicon and study on each click left the blocks blank while they loaded again.
    private sealed class KeptView(TabState state, BibleLibrary library, FrameworkElement view)
    {
        public TabState State => state; public BibleLibrary Library => library; public FrameworkElement View => view;
        public string Fingerprint = MainWindow.Fingerprint(state), Notes = "";
    }
    private readonly Dictionary<string, KeptView> views = [];
    private string? noteSignature;
    private string NoteSignature() => noteSignature ??= string.Join("|", NotesForReaders().Where(n => n.WordAnchor != null).Select(n => n.Id + ":" + n.NoteTitle + ":" + n.WordAnchor!.Start));
    private static string Fingerprint(TabState s) => string.Join("|", s.Kind, s.Edition, s.Book, s.Chapter, s.Verse, s.EntryIndex, s.ReferenceSystem, s.DocumentId, s.DocumentSection, s.DocumentQuery, s.StudyTerm, s.StudyMode, s.StudyTranslation, s.LexiconSource, s.LexiconQuery, s.LexiconCode, s.GuideReference, s.GuideEndVerse, s.ShowInterlinear, s.HideNoteMarkers, s.FontSize);
    private T Reuse<T>(TabState state, Func<T> create) where T : FrameworkElement
    {
        if (views.TryGetValue(state.Id, out var kept) && ReferenceEquals(kept.State, state) && ReferenceEquals(kept.Library, library) && kept.View is T view && kept.Fingerprint == Fingerprint(state))
        {
            if (view.Parent is ContentControl owner) owner.Content = null;
            return view;
        }
        var created = create(); views[state.Id] = new(state, library, created); return created;
    }
    private FrameworkElement RenderNode(LayoutNode node)
    {
        if (node.Kind == "pane") return RenderPane(node);
        var grid = new Grid();
        bool horizontal = node.Direction == "horizontal";
        double ratio = Math.Clamp(double.IsFinite(node.Ratio) ? node.Ratio : .5, .15, .85);
        if (horizontal)
        {
            grid.ColumnDefinitions.Add(new() { Width = new(ratio, GridUnitType.Star), MinWidth = 160 });
            grid.ColumnDefinitions.Add(new() { Width = new(8) });
            grid.ColumnDefinitions.Add(new() { Width = new(1 - ratio, GridUnitType.Star), MinWidth = 160 });
        }
        else
        {
            grid.RowDefinitions.Add(new() { Height = new(ratio, GridUnitType.Star), MinHeight = 145 });
            grid.RowDefinitions.Add(new() { Height = new(8) });
            grid.RowDefinitions.Add(new() { Height = new(1 - ratio, GridUnitType.Star), MinHeight = 145 });
        }
        var first = RenderNode(node.Children[0]); var second = RenderNode(node.Children[1]);
        var splitter = new GridSplitter { Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
        splitter.DragCompleted += (_, _) =>
        {
            double a = horizontal ? grid.ColumnDefinitions[0].ActualWidth : grid.RowDefinitions[0].ActualHeight;
            double b = horizontal ? grid.ColumnDefinitions[2].ActualWidth : grid.RowDefinitions[2].ActualHeight;
            if (a + b > 0) node.Ratio = a / (a + b); Changed();
        };
        if (horizontal) { Grid.SetColumn(second, 2); Grid.SetColumn(splitter, 1); }
        else { Grid.SetRow(second, 2); Grid.SetRow(splitter, 1); }
        grid.Children.Add(first); grid.Children.Add(splitter); grid.Children.Add(second); return grid;
    }
    private FrameworkElement RenderPane(LayoutNode pane)
    {
        var border = new Border { Background = Theme.Surface, BorderBrush = Line, BorderThickness = new(1), CornerRadius = new(7), ClipToBounds = true };
        border.PreviewMouseDown += (_, _) => activePane = pane.Id;
        var panel = new DockPanel(); border.Child = panel;
        var toolbar = new DockPanel { Background = Brush("#F0F3EC"), LastChildFill = true };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Button("+", () => { activePane = pane.Id; AddTab(new() { Edition = library.Editions.FirstOrDefault()?.Id ?? "" }); }, "Nova aba de Bíblia"));
        buttons.Children.Add(Button("↔", () => Split(pane, "horizontal"), "Dividir lado a lado"));
        buttons.Children.Add(Button("↕", () => Split(pane, "vertical"), "Dividir acima e abaixo"));
        buttons.Children.Add(Button("×", () => ClosePane(pane), "Fechar bloco; notas permanecem salvas"));
        foreach (Button b in buttons.Children) { b.Padding = new(6, 1, 6, 1); b.FontSize = 13; b.Height = 22; b.Margin = new(2, 1, 2, 1); }
        DockPanel.SetDock(buttons, Dock.Right); toolbar.Children.Add(buttons);
        toolbar.Children.Add(new TextBlock { Text = "BLOCO", FontSize = 9, Foreground = Muted, Margin = new(9, 6, 0, 5) });
        DockPanel.SetDock(toolbar, Dock.Top); panel.Children.Add(toolbar);
        var tabs = new TabControl();
        controls[pane.Id] = tabs;
        tabs.SelectionChanged += (_, e) =>
        {
            if (e.Source != tabs || tabs.SelectedItem is not TabItem { Tag: TabState selected }) return;
            pane.ActiveTab = selected.Id; if (!building) activePane = pane.Id; Changed();
        };
        foreach (var state in pane.Tabs)
        {
            var item = new TabItem { Tag = state };
            var tabHeader = new StackPanel { Orientation = Orientation.Horizontal };
            tabHeader.Children.Add(new TextBlock { Text = TabTitle(state), MaxWidth = 165, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            var close = Button("×", () => { SaveNow(false); pane.Tabs.Remove(state); RenderWorkspace(); Changed(); }, "Fechar aba"); close.Padding = new(4, 0, 4, 0); close.Height = 20; close.Margin = new(7, 0, 0, 0); close.Background = Brushes.Transparent; tabHeader.Children.Add(close);
            tabHeader.ToolTip = "Arraste ao centro de um bloco para integrar; às bordas para criar outro bloco.";
            tabHeader.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is DependencyObject child && HasButtonAncestor(child, tabHeader)) return;
                dragOrigin = e.GetPosition(this); draggedTab = state.Id; dragSource = tabHeader;
            };
            var context = new ContextMenu();
            foreach (var region in new[] { DockRegion.Left, DockRegion.Right, DockRegion.Top, DockRegion.Bottom })
            {
                var splitMenu = new MenuItem { Header = "Mover para novo bloco " + RegionName(region) };
                splitMenu.Click += (_, _) => DockTab(state.Id, pane, region); context.Items.Add(splitMenu);
            }
            foreach (var target in layout.Leaves().Where(p => p.Id != pane.Id))
            {
                var menu = new MenuItem { Header = "Mover para bloco " + (layout.Leaves().ToList().IndexOf(target) + 1) }; menu.Click += (_, _) => MoveTab(state.Id, target); context.Items.Add(menu);
            }
            if (state.Kind == "notes") { var delete = new MenuItem { Header = "Excluir nota…" }; delete.Click += (_, _) => DeleteNote(state); context.Items.Add(delete); }
            tabHeader.ContextMenu = context; item.Header = tabHeader;
            if (state.Kind == "notes") item.Content = BuildNotes(state);
            else if (state.Kind == "notescatalog") item.Content = new NotesCatalogView(LoadNotes, OpenNote, () => AddTab(new() { Kind = "notes", NoteCreated = DateTimeOffset.Now }), DeleteNote, () => NoteFiles.Deleted(), RestoreNote, PurgeNote);
            else if (state.Kind == "library") item.Content = new DocumentLibraryView(documents, OpenDocument);
            else if (state.Kind == "book") item.Content = Reuse(state, () => new BookReaderView(documents, state, Changed, note => AddTab(note)));
            else if (state.Kind == "lexicon") item.Content = Reuse(state, () => new HebrewLexiconView(library, state, Changed, StudyLexeme));
            else if (state.Kind == "exegesis") item.Content = Reuse(state, () => new ExegeticalGuideView(library, state, Changed, OpenPassageNotes, OpenWord, StudyLexeme));
            else if (state.Kind == "wordstudy") item.Content = Reuse(state, () => new WordStudyView(library, search, translationLinks, state, Changed, ShowStudyOccurrences));
            else if (state.Kind is "analysis" or "apparatus") item.Content = BuildAnalysis(state);
            else
            {
                bool created = false;
                var view = Reuse(state, () => { created = true; return new ReaderView(library, state, Changed, SetStatus, t => _ = Synchronize(t), OpenWord, OpenVariants, NotesForReaders, AddTab, OpenNote); });
                readers[state.Id] = view; item.Content = view;
                // A kept reader redraws only if the personal word notes changed meanwhile.
                string notes = NoteSignature(); var kept = views[state.Id];
                if (!created && kept.Notes != notes) view.RefreshAnnotations();
                kept.Notes = notes;
            }
            tabs.Items.Add(item);
            if (state.Id == pane.ActiveTab) tabs.SelectedItem = item;
        }
        if (tabs.Items.Count > 0 && tabs.SelectedItem == null) tabs.SelectedIndex = 0;
        if (tabs.Items.Count == 0)
        {
            var empty = new StackPanel { Margin = new(26), VerticalAlignment = VerticalAlignment.Center };
            empty.Children.Add(Label("Este bloco está livre.", 23)); empty.Children.Add(Label("Abra uma Bíblia na biblioteca ou adicione notas.", 13, Muted));
            empty.Children.Add(Button("+ Abrir Bíblia", () => { activePane = pane.Id; AddTab(new() { Edition = library.Editions.FirstOrDefault()?.Id ?? "" }); })); panel.Children.Add(empty);
        }
        else panel.Children.Add(tabs);
        var surface = new Grid { AllowDrop = true }; surface.Children.Add(border);
        var previewText = new TextBlock { Foreground = Green, FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(15) };
        var preview = new Border { Background = Brush("#E0EEE4"), BorderBrush = Green, BorderThickness = new(2), CornerRadius = new(7), Opacity = .92, IsHitTestVisible = false, Visibility = Visibility.Collapsed, Child = previewText };
        surface.Children.Add(preview);
        surface.PreviewDragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent("VerbumTab")) return;
            Point p = e.GetPosition(surface); var region = StudyLayout.Region(p.X, p.Y, surface.ActualWidth, surface.ActualHeight);
            preview.Visibility = Visibility.Visible;
            preview.HorizontalAlignment = region == DockRegion.Left ? HorizontalAlignment.Left : region == DockRegion.Right ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            preview.VerticalAlignment = region == DockRegion.Top ? VerticalAlignment.Top : region == DockRegion.Bottom ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            preview.Width = region is DockRegion.Left or DockRegion.Right ? surface.ActualWidth / 2 : double.NaN;
            preview.Height = region is DockRegion.Top or DockRegion.Bottom ? surface.ActualHeight / 2 : double.NaN;
            previewText.Text = region == DockRegion.Center ? "Solte para integrar como aba" : "Solte para criar bloco " + RegionName(region);
            e.Effects = DragDropEffects.Move; e.Handled = true;
        };
        surface.DragLeave += (_, _) => preview.Visibility = Visibility.Collapsed;
        surface.PreviewDrop += (_, e) =>
        {
            preview.Visibility = Visibility.Collapsed;
            if (e.Data.GetData("VerbumTab") is not string id) return;
            var p = e.GetPosition(surface); var region = StudyLayout.Region(p.X, p.Y, surface.ActualWidth, surface.ActualHeight);
            DockTab(id, pane, region); e.Handled = true;
        };
        return surface;
    }
    private static bool HasButtonAncestor(DependencyObject child, DependencyObject stop)
    {
        while (child != stop) { if (child is Button) return true; child = child is Visual ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child); if (child == null) break; }
        return false;
    }
    private static string RegionName(DockRegion region) => region switch { DockRegion.Left => "à esquerda", DockRegion.Right => "à direita", DockRegion.Top => "acima", DockRegion.Bottom => "abaixo", _ => "no centro" };
    private void DockTab(string id, LayoutNode target, DockRegion region)
    {
        SaveNow(false);
        if (!StudyLayout.Dock(ref layout, id, target.Id, region, out var active)) { SetStatus("Movimento sem alteração. Para dividir o mesmo bloco, mantenha outra aba nele; limite de oito blocos."); return; }
        activePane = active; RenderWorkspace(); Changed();
        SetStatus(region == DockRegion.Center ? "Aba integrada ao bloco." : "Novo bloco criado " + RegionName(region) + ".");
    }
    private void MoveTab(string id, LayoutNode target)
    {
        DockTab(id, target, DockRegion.Center);
    }
    private FrameworkElement BuildNotes(TabState state)
    {
        var grid = new Grid { Margin = new(18, 16, 18, 16) };
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new()); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var title = new TextBox { Text = state.NoteTitle, FontSize = 19, FontWeight = FontWeights.SemiBold, BorderThickness = new(0), Padding = new(0, 4, 0, 10) };
        void Touch() { state.NoteEdited = DateTimeOffset.Now; Changed(); }
        title.TextChanged += (_, _) => { state.NoteTitle = title.Text; Touch(); }; var heading = new DockPanel();
        var identity = new Border { Width = 4, CornerRadius = new(2), Margin = new(0, 0, 8, 5) };
        void ShowColor() { identity.Background = TextMarkers.ValidFontColor(state.NoteColor) ? Theme.Raw(state.NoteColor) : MainWindow.Line; }
        void SetColor(string color) { state.NoteColor = color; ShowColor(); Touch(); }
        ShowColor(); DockPanel.SetDock(identity, Dock.Left); heading.Children.Add(identity); heading.Children.Add(title); grid.Children.Add(heading);
        var noteTools = new WrapPanel { Margin = new(0, 0, 0, 8) };
        noteTools.Children.Add(NoteColors.Palette("Cor da nota", TextMarkers.Colors, SetColor, () => SetColor("")));

        Grid.SetRow(noteTools, 1); grid.Children.Add(noteTools);
        var editor = new MarkdownNoteView(state.Note, value => { state.Note = value; Touch(); }, selected => CreateNoteLink(selected, state.Edition), target => _ = OpenNoteLink(target), state); Grid.SetRow(editor, 2); grid.Children.Add(editor);
        var popup = new System.Windows.Controls.Primitives.Popup { StaysOpen = false, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, AllowsTransparency = true };
        var info = Button("+", () =>
        {
            if (popup.IsOpen) { popup.IsOpen = false; return; }
            popup.Child = new NoteInfoView(state, Touch, editor.AppendMarkdown, bible => CreateNoteLink("", state.Edition, bible),
                target => _ = OpenNoteLink(target), () => DeleteNote(state), () => popup.IsOpen = false)
            { Width = Math.Clamp(grid.ActualWidth, 180, 310), MaxHeight = Math.Clamp(ActualHeight - 140, 200, 620) };
            popup.IsOpen = true;
        }, "Informações da nota, links, passagens e etiquetas");
        popup.PlacementTarget = info; noteTools.Children.Add(info); grid.Unloaded += (_, _) => popup.IsOpen = false;
        var footer = new WrapPanel { Margin = new(0, 6, 0, 0) }; footer.Children.Add(Label("Salvamento automático local · Ctrl+S", 10, Muted)); var notes = LoadNotes(); var incoming = NoteGraph.Edges(notes).Where(e => e.To == state.Id).Select(e => e.From).ToHashSet(); foreach (var related in notes.Where(n => incoming.Contains(n.Id))) footer.Children.Add(Button("↗ " + related.NoteTitle, () => OpenNote(related), "Esta nota menciona a nota atual")); Grid.SetRow(footer, 3); grid.Children.Add(footer); return grid;
    }
    private void SaveNow() => SaveNow(true);
    private bool SaveNow(bool notify)
    {
        if (previewMode) return true;
        try
        {
            var session = CaptureSession();
            SaveNotes();
            SessionStore.Save(session);
            if (notify) resumeCheckpoint = SessionExit.Clone(session);
            if (notify) SetStatus("Área de estudo e notas salvas · " + DateTime.Now.ToString("HH:mm"));
            return true;
        }
        catch (Exception ex) { SetStatus("Não foi possível salvar: " + ex.Message); return false; }
    }
    private StudySession CaptureSession() => new() { SidebarCollapsed = sidebarCollapsed, Layout = layout, LibraryPath = library.DirectoryPath, Width = ActualWidth > 0 ? ActualWidth : Width, Height = ActualHeight > 0 ? ActualHeight : Height };
    private void SaveNotes()
    {
        var notesDir = Path.Combine(SessionStore.DataDirectory, "notes"); Directory.CreateDirectory(notesDir);
        foreach (var note in layout.Leaves().SelectMany(p => p.Tabs).Where(t => t.Kind == "notes" && !NoteFiles.IsTrashed(t.Id)))
        {
            string path = Path.Combine(notesDir, SafeNoteId(note.Id) + ".json"), temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(note)); File.Move(temp, path, true);
            File.WriteAllText(Path.Combine(notesDir, SafeNoteId(note.Id) + ".md"), note.Note);
        }
    }
    private bool SavePreviousPosition()
    {
        try
        {
            SaveNotes();
            var previous = SessionExit.Clone(resumeCheckpoint); NoteFiles.RemoveTrashedTabs(previous.Layout);
            var savedNotes = new List<TabState>();
            foreach (var note in previous.Layout.Leaves().SelectMany(p => p.Tabs).Where(t => t.Kind == "notes"))
            {
                string path = Path.Combine(SessionStore.DataDirectory, "notes", SafeNoteId(note.Id) + ".json");
                if (File.Exists(path)) { var saved = JsonSerializer.Deserialize<TabState>(File.ReadAllText(path)); if (saved?.Kind == "notes" && saved.Id == note.Id) savedNotes.Add(saved); }
            }
            SessionExit.PreserveNotes(previous, savedNotes); SessionStore.Save(previous); return true;
        }
        catch (Exception ex) { SetStatus("Não foi possível preservar a posição salva: " + ex.Message); return false; }
    }
    private void ConfirmExit(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (previewMode) return;
        if (closePromptActive) { e.Cancel = true; return; }
        closePromptActive = true; bool pendingSave = saveTimer.IsEnabled; saveTimer.Stop();
        try
        {
            var result = MessageBox.Show(this, "Salvar onde parou?\n\nSim: continuar desta posição na próxima abertura.\nNão: manter a posição do início desta sessão ou do último Salvar área.\nCancelar: continuar no VERBUM.\n\nSuas notas e marcações continuam preservadas.", "Sair do VERBUM", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel);
            bool completed = SessionExit.Complete(result, () => SaveNow(false), SavePreviousPosition);
            exitCommitted = completed;
            e.Cancel = !completed;
            if (completed) searching?.Cancel();
            else
            {
                if (result is MessageBoxResult.Yes or MessageBoxResult.No) MessageBox.Show(this, "Não foi possível salvar. O VERBUM continuará aberto.\n\n" + status.Text, "Salvar antes de sair", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (pendingSave) saveTimer.Start();
            }
        }
        finally { closePromptActive = false; }
    }
    private static string SafeNoteId(string id) => new(id.Where(char.IsAsciiLetterOrDigit).Take(64).ToArray());
    private NoteStore NoteFiles => new(Path.Combine(SessionStore.DataDirectory, "notes"));
    private List<TabState> LoadNotes()
    {
        var notes = new Dictionary<string, TabState>(); var dir = Path.Combine(SessionStore.DataDirectory, "notes");
        if (Directory.Exists(dir)) foreach (string path in Directory.EnumerateFiles(dir, "*.json"))
        {
            try { var note = JsonSerializer.Deserialize<TabState>(File.ReadAllText(path)); if (note?.Kind == "notes" && note.Id.Length > 0 && !NoteFiles.IsTrashed(note.Id)) notes[note.Id] = note; }
            catch (Exception ex) { SetStatus("Uma nota não pôde ser lida; arquivo preservado: " + Path.GetFileName(path) + " · " + ex.Message); }
        }
        foreach (var note in layout.Leaves().SelectMany(p => p.Tabs).Where(t => t.Kind == "notes" && !NoteFiles.IsTrashed(t.Id))) notes[note.Id] = note;
        return notes.Values.OrderBy(n => n.NoteTitle, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private void DeleteNote(TabState note)
    {
        if (previewMode) return;
        if (MessageBox.Show(this, "Mover a nota ‘" + note.NoteTitle + "’ para a lixeira?\n\nVocê poderá restaurá-la em Notas → Lixeira.", "Excluir nota", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            NoteFiles.Trash(note); NoteFiles.RemoveTrashedTabs(layout); NoteFiles.RemoveTrashedTabs(resumeCheckpoint.Layout);
            var pane = ActivePane(); if (pane.Tabs.Count == 0) { var catalog = new TabState { Kind = "notescatalog" }; pane.Tabs.Add(catalog); pane.ActiveTab = catalog.Id; }
            RenderWorkspace(); SaveNow(false); Changed(); SetStatus("Nota movida para a lixeira. Os vínculos de outras notas foram preservados.");
        }
        catch (Exception ex) { SetStatus("Não foi possível concluir a exclusão: " + ex.Message); }
    }
    private void PurgeNote(TabState note)
    {
        if (previewMode || MessageBox.Show(this, $"Apagar definitivamente ‘{note.NoteTitle}’? A nota deixará de poder ser restaurada pela lixeira.", "Excluir definitivamente", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { NoteFiles.Purge(note.Id); SetStatus("Nota excluída definitivamente."); }
        catch (Exception ex) { SetStatus("Não foi possível concluir a exclusão: " + ex.Message); }
    }
    private void RestoreNote(TabState note)
    {
        if (previewMode) return;
        try { NoteFiles.Restore(note.Id); readerNotes = null; foreach (var reader in readers.Values) reader.RefreshAnnotations(); Changed(); SetStatus("Nota restaurada com seus vínculos originais."); }
        catch (Exception ex) { SetStatus("Não foi possível restaurar a nota: " + ex.Message); }
    }
    private void OpenNote(TabState note)
    {
        var existing = layout.Leaves().FirstOrDefault(p => p.Tabs.Any(t => t.Kind == "notes" && t.Id == note.Id));
        if (existing != null) { activePane = existing.Id; existing.ActiveTab = note.Id; RenderWorkspace(); Changed(); }
        else AddTab(note);
    }
    private void ShowSavedNotes()
    {
        var existing = layout.Leaves().FirstOrDefault(p => p.Tabs.Any(t => t.Kind == "notescatalog"));
        if (existing != null) { activePane = existing.Id; existing.ActiveTab = existing.Tabs.First(t => t.Kind == "notescatalog").Id; RenderWorkspace(); Changed(); }
        else AddTab(new() { Kind = "notescatalog" });
    }
    private Task<NoteLink?> CreateNoteLink(string selected, string edition, bool bibleOnly = false)
    {
        try { var dialog = new NoteLinkDialog(this, library, LoadNotes(), documents.Load(), selected, edition, bibleOnly); return Task.FromResult(dialog.ShowDialog() == true ? dialog.Result : null); }
        catch (Exception ex) { SetStatus("Não foi possível inserir o vínculo: " + ex.Message); return Task.FromResult<NoteLink?>(null); }
    }
    private async Task OpenNoteLink(string target)
    {
        try
        {
            if (!NoteLinks.TryParse(target, out var destination) || destination == null) { SetStatus("Vínculo não reconhecido."); return; }
            if (destination.Kind == "note") { var note = LoadNotes().FirstOrDefault(n => n.Id == destination.Id); if (note == null) SetStatus("Nota vinculada não encontrada; seu texto foi preservado."); else OpenNote(note); }
            else if (destination.Kind == "word")
            {
                var anchor = LoadNotes().FirstOrDefault(n => n.Id == destination.Id)?.WordAnchor;
                if (anchor == null) { SetStatus("Anotação indisponível."); return; }
                var edition = await library.LoadAsync(anchor.Edition);
                var verse = edition.Chapters.GetValueOrDefault((anchor.Book, anchor.Chapter))?.FirstOrDefault(anchor.Matches);
                if (verse == null) { SetStatus("O texto desta edição mudou ou está indisponível."); return; }
                var previous = layout.Leaves().FirstOrDefault(p => p.Tabs.Any(t => t.Kind == "bible" && t.Edition == anchor.Edition));
                if (previous != null) activePane = previous.Id;
                await OpenHit(new(verse, "Anotação", 0, [new(anchor.Text, SearchText.Normalize(anchor.Text), anchor.Start, anchor.Text.Length)]));
            }
            else if (destination.Kind == "book") { var book = documents.Find(destination.Id); if (book == null || destination.Section >= book.Sections.Count) SetStatus("Livro ou seção vinculada não encontrado na biblioteca atual."); else OpenDocument(book, destination.Section, ""); }
            else if (destination.Kind == "bible")
            {
                var edition = await library.LoadAsync(destination.Id);
                if (!ExegesisData.TryPassage(edition, destination.Reference, out var passage) || passage == null) { SetStatus("A passagem vinculada não existe nesta edição."); return; }
                var existing = layout.Leaves().SelectMany(p => p.Tabs.Select(t => (Pane: p, Tab: t))).FirstOrDefault(x => x.Tab.Kind == "bible" && x.Tab.Edition == destination.Id);
                if (existing.Tab != null) { activePane = existing.Pane.Id; existing.Pane.ActiveTab = existing.Tab.Id; existing.Tab.Book = passage.Book; existing.Tab.Chapter = passage.Chapter; existing.Tab.Verse = passage.Start; existing.Tab.EntryIndex = null; existing.Tab.Scroll = 0; RenderWorkspace(); Changed(); }
                else AddTab(new() { Edition = destination.Id, Book = passage.Book, Chapter = passage.Chapter, Verse = passage.Start, ReferenceSystem = edition.Resource?.ReferenceSystem ?? "" });
            }
            else if (destination.Kind == "web") System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(destination.Id) { UseShellExecute = true });
        }
        catch (Exception ex) { SetStatus("Não foi possível abrir o vínculo: " + ex.Message); }
    }
    private async Task Synchronize(TabState source)
    {
        if (source.LinkGroup == "Livre") return;
        try
        {
            var origin = await library.LoadAsync(source.Edition);
            if (!origin.Chapters.TryGetValue((source.Book, source.Chapter), out var sourceEntries) || !sourceEntries.All(e => e.ChapterValid)) { SetStatus("Sincronização suspensa: numeração deste livro precisa de revisão."); return; }
            foreach (var target in layout.Leaves().SelectMany(p => p.Tabs).Where(t => t.Kind == "bible" && t.Id != source.Id && t.LinkGroup == source.LinkGroup))
            {
                if (!readers.TryGetValue(target.Id, out var view)) continue;
                await view.NavigateLinked(source.Book, source.Chapter, source.Verse, origin);
            }
        }
        catch (Exception ex) { SetStatus("Não foi possível sincronizar: " + ex.Message); }
    }
    private async Task RunSearch()
    {
        parallelEvidencePhrase = null;
        string text = query.Text.Trim(); if (text.Length == 0) { query.Focus(); SetStatus("Digite uma palavra ou expressão para pesquisar."); return; }
        searching?.Cancel(); var cts = new CancellationTokenSource(); searching = cts;
        documentResults.Visibility = Visibility.Collapsed; documentHits.Children.Clear(); bool includeDocuments = searchDocuments.IsChecked == true;
        searchRow.Height = new(Math.Max(280, ActualHeight * .38)); results.Items.Clear(); variants.Children.Clear(); response = null; more.Visibility = Visibility.Collapsed;
        searchSummary.Text = "Pesquisando na biblioteca…"; searchDetails.Text = "A primeira pesquisa prepara o índice local. O texto original não é modificado.";
        string selectedScope = scope.SelectedItem as string ?? "Aba ativa";
        var active = ActiveBible();
        string[] ids = selectedScope == "Toda a biblioteca" ? library.Editions.Select(e => e.Id).ToArray() : [selectedScope == "Aba ativa" ? active?.Edition ?? library.Editions.FirstOrDefault()?.Id ?? "" : selectedScope];
        var selectedMode = (SearchMode)Math.Max(0, mode.SelectedIndex);
        try
        {
            var found = await search.SearchAsync(text, ids.Where(i => i.Length > 0), selectedMode, cts.Token);
            if (cts.IsCancellationRequested) return;
            response = found; shown = 0; searchedText = text; searchedKey = TranslationLinks.Key(text, selectedMode);
            searchSummary.Text = $"{found.Total:N0} ocorrências · {text}";
            bool hebrewQuery = SearchText.HasHebrew(text);
            searchDetails.Text = selectedMode switch { SearchMode.Strong => "Ocorrências pelos códigos lexicais fornecidos pela fonte. Não é pesquisa de tradução ou sinônimos.", SearchMode.Lemma => "Ocorrências pelo lema associado pela fonte/lexicon Strong, ignorando seus sinais.", SearchMode.Exact => "Grafia exata: sinais preservados. Clique em uma ocorrência para ler o contexto.", _ => "Palavras no mesmo trecho, ignorando sinais e acentos. Grafias originais abaixo; comparação de formas escritas." };
            if (found.Total == 0) searchDetails.Text = hebrewQuery ? "Nenhuma ocorrência neste escopo. Selecione BHS-A-Strong ou WLC-OSHB para o hebraico; tente ‘Trecho de palavra’ para procurar dentro de formas com prefixos." : "Nenhuma ocorrência. Experimente outra grafia ou uma fonte que forneça o tipo de anotação procurado.";
            foreach (var option in found.Spellings)
            {
                var chip = Button((option.Approximate ? "Sugestão: " : "") + option.Text + $" ({option.Count})", () => { query.Text = option.Text; mode.SelectedIndex = option.Approximate ? 0 : 1; _ = RunSearch(); }, option.Approximate ? "Aproximação ortográfica de até uma edição. Não é equivalência de significado." : "Pesquisar apenas esta grafia, mantendo os sinais");
                if (SearchText.HasHebrew(option.Text)) chip.FontSize = 19; variants.Children.Add(chip);
            }
            if (found.Warnings.Count > 0) { searchDetails.ToolTip = string.Join("\n", found.Warnings); searchDetails.Text += $"  ·  {found.Warnings.Count} avisos de importação (passe o mouse)."; }
            bookFilter.ItemsSource = new[] { new BookFilter("Todos os livros", -1), new BookFilter("Antigo Testamento", -2), new BookFilter("Novo Testamento", -3) }
                .Concat(found.Hits.GroupBy(h => h.Verse.Book).OrderBy(g => g.Key).Select(g => new BookFilter(g.First().Verse.BookName + $" ({g.Count():N0})", g.Key))).ToList();
            bookFilter.SelectedIndex = 0; ApplyResultFilter(); SetStatus($"Pesquisa concluída · {ids.Length} versão(ões) · {found.Total:N0} ocorrências");
            if (includeDocuments)
            {
                var hits = await Task.Run(() => documents.Search(text, selectedMode, cts.Token), cts.Token); if (cts.IsCancellationRequested) return;
                documentResults.Header = $"PDFs e livros · {hits.Count} páginas/seções encontradas"; documentResults.Visibility = Visibility.Visible; documentResults.IsExpanded = true;
                foreach (var hit in hits.Take(200))
                {
                    var panel = new StackPanel(); panel.Children.Add(Label(hit.Document.Title + " · " + hit.Document.Sections[hit.Section].Title, 12, Green));
                    var excerpt = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13 }; int start = 0;
                    foreach (var match in DocumentLibrary.Matches(hit.Excerpt, text, selectedMode, false)) { if (match.Start > start) excerpt.Inlines.Add(new System.Windows.Documents.Run(hit.Excerpt[start..match.Start])); excerpt.Inlines.Add(new System.Windows.Documents.Run(hit.Excerpt.Substring(match.Start, match.Length)) { FontWeight = FontWeights.Bold, Background = Brushes.Khaki }); start = match.Start + match.Length; }
                    if (start < hit.Excerpt.Length) excerpt.Inlines.Add(new System.Windows.Documents.Run(hit.Excerpt[start..])); panel.Children.Add(excerpt);
                    var button = Button("", () => OpenDocument(hit.Document, hit.Section, text)); button.Content = panel; button.HorizontalContentAlignment = HorizontalAlignment.Stretch; documentHits.Children.Add(button);
                }
                if (hits.Count == 0) documentHits.Children.Add(Label("Nenhum trecho no texto extraído. Adicione documentos pela Biblioteca; PDFs digitalizados precisam de OCR.", 13));
                if (hits.Count > 200) documentHits.Children.Add(Label("Mostrando os primeiros 200 trechos. Refine a consulta para reduzir a seleção.", 12, Muted));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!cts.IsCancellationRequested) { searchSummary.Text = "Não foi possível pesquisar"; searchDetails.Text = ex.Message; } }
    }
    private void ApplyResultFilter()
    {
        if (response == null) return;
        var filter = bookFilter.SelectedItem as BookFilter ?? new BookFilter("Todos os livros");
        filteredHits = response.Hits.Where(filter.Matches).ToList(); shown = 0; results.Items.Clear();
        RefreshOccurrenceHeader();
        searchSummary.Text = $"{filteredHits.Count:N0} de {response.Total:N0} ocorrências · {searchedText}";
        MoreResults();
        UpdateTranslationSummary();
    }
    private void MoreResults()
    {
        if (response == null) return;
        foreach (var hit in filteredHits.Skip(shown).Take(100))
        {
            var content = OccurrenceColumns(parallelEdition != null);
            var referenceCell = new StackPanel { Margin = new(10, 10, 7, 10) };
            var open = Button(hit.Verse.Reference, () => _ = OpenHit(hit), "Abrir passagem"); open.Padding = new(0); open.Margin = new(0); open.Background = Brushes.Transparent; open.HorizontalContentAlignment = HorizontalAlignment.Left; open.HorizontalAlignment = HorizontalAlignment.Left;
            referenceCell.Children.Add(open); referenceCell.Children.Add(Label(hit.Verse.Edition, 10, Muted)); content.Children.Add(referenceCell);
            var excerpt = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = SearchText.HasHebrew(hit.Verse.Text) ? 19 : 14, FlowDirection = SearchText.HasHebrew(hit.Verse.Text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
            if (SearchText.HasHebrew(hit.Verse.Text)) excerpt.FontFamily = HebrewReadingFont;
            foreach (var span in SearchPresentation.Spans(hit)) excerpt.Inlines.Add(new Run(span.Text) { FontWeight = span.Match ? FontWeights.Bold : FontWeights.Normal, Background = span.Match ? Brush("#F6E4A1") : Brushes.Transparent });
            var originalCell = new Border { Child = excerpt, Padding = new(12, 10, 12, 10), BorderBrush = Line, BorderThickness = new(1, 0, 0, 0) }; Grid.SetColumn(originalCell, 1); content.Children.Add(originalCell);
            if (parallelEdition is { } translation)
            {
                var parallel = new StackPanel { Margin = new(10, 7, 10, 7) };
                var translationCell = new Border { Child = parallel, BorderBrush = Line, BorderThickness = new(1, 0, 0, 0) }; Grid.SetColumn(translationCell, 2); content.Children.Add(translationCell); _ = PopulateParallel(parallel, hit, translation, searchedKey);
            }
            int rowIndex = results.Items.Count;
            results.Items.Add(new ListBoxItem { Content = content, Tag = hit, Padding = new(0), BorderThickness = new(0, 0, 0, 1), BorderBrush = Line, Background = rowIndex % 2 == 0 ? Theme.Surface : Paper });
        }
        shown = Math.Min(shown + 100, filteredHits.Count); more.Visibility = shown < filteredHits.Count ? Visibility.Visible : Visibility.Collapsed;
        more.Content = $"Mostrar mais · {shown:N0} de {filteredHits.Count:N0}";
    }
    private static Grid OccurrenceColumns(bool parallel)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(155) }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        if (parallel) grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        return grid;
    }
    private void RefreshOccurrenceHeader()
    {
        occurrenceHeader.Children.Clear(); occurrenceHeader.ColumnDefinitions.Clear();
        occurrenceHeader.ColumnDefinitions.Add(new() { Width = new(155) }); occurrenceHeader.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        if (parallelEdition != null) occurrenceHeader.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        occurrenceHeader.Background = Brush("#EAEFE7"); occurrenceHeader.Margin = new(0, 0, 18, 0);
        var titles = parallelEdition == null ? new[] { "REFERÊNCIA", "TEXTO PESQUISADO" } : new[] { "REFERÊNCIA", "TEXTO PESQUISADO", parallelEdition.Info.Id + " · TRADUÇÃO" };
        for (int i = 0; i < titles.Length; i++)
        {
            var title = new TextBlock { Text = titles[i], FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Green, Padding = new(12, 8, 12, 8) };
            if (i == 2) title.ToolTip = "Negrito e realce: vínculo manual, expressão filtrada ou sugestão pelas glossas e flexões portuguesas, identificada ao passar o mouse. Sugestões não entram nas contagens de traduções confirmadas. Use o botão direito para confirmar uma seleção.";
            Grid.SetColumn(title, i); occurrenceHeader.Children.Add(title);
        }
    }
    private async Task LoadParallelTranslation()
    {
        int generation = ++parallelGeneration; string id = parallelTranslation.SelectedItem as string ?? "Sem paralelo";
        parallelEdition = null;
        try
        {
            if (id != "Sem paralelo")
            {
                var edition = await library.LoadAsync(id);
                if (generation != parallelGeneration) return;
                parallelEdition = edition;
            }
            ApplyResultFilter();
        }
        catch (Exception ex) { if (generation == parallelGeneration) SetStatus("Paralelo indisponível: " + ex.Message); }
    }
    private void UpdateTranslationSummary()
    {
        translationSummary.Children.Clear();
        translationExpander.Visibility = parallelEdition == null ? Visibility.Collapsed : Visibility.Visible;
        if (parallelEdition == null || response == null) return;
        var marked = filteredHits.Where(h => parallelEdition.Resource?.ReferenceSystem == "MT" || !BibleLibrary.IsSuperscriptionHit(h)).Select(h => (Hit: h, Link: translationLinks.Find(searchedKey, h.Verse, parallelEdition.Info.Id)))
            .Where(x => x.Link != null && BibleLibrary.ResolveVerse(parallelEdition, x.Link.TargetBook, x.Link.TargetChapter, x.Link.TargetVerse) is { } v && TranslationLinks.Valid(x.Link, v)).ToList();
        int references = marked.Select(x => (x.Link!.TargetBook, x.Link.TargetChapter, x.Link.TargetVerse)).Distinct().Count();
        translationExpander.Header = $"Traduções marcadas · {references:N0} versículos";
        translationSummary.Children.Add(Label($"{parallelEdition.Info.Id} · traduções marcadas por você: {references:N0} versículos. Contagens parciais; sem alinhamento automático.", 11, Muted));
        if (marked.Count > 0) translationSummary.Children.Add(Button("Todas as ocorrências", ApplyResultFilter));
        foreach (var group in marked.GroupBy(x => x.Link!.Phrase).OrderByDescending(g => g.Count()).Take(20))
        {
            int count = group.Select(x => (x.Link!.TargetBook, x.Link.TargetChapter, x.Link.TargetVerse)).Distinct().Count();
            translationSummary.Children.Add(Button(group.Key + $" · {count} v.", () =>
            {
                var ids = group.Select(x => x.Hit).ToHashSet(); filteredHits = filteredHits.Where(ids.Contains).ToList();
                shown = 0; results.Items.Clear(); MoreResults(); searchSummary.Text = $"{filteredHits.Count:N0} ocorrências · tradução marcada: {group.Key}";
            }, "Mostrar apenas estas marcações. Selecione novamente um livro para restaurar o conjunto."));
        }
    }
    private async Task PopulateParallel(StackPanel panel, SearchHit hit, BibleEdition translation, string queryKey)
    {
        try
        {
            var source = await library.LoadAsync(hit.Verse.Edition);
            if (translation.Resource?.ReferenceSystem != "MT" && BibleLibrary.IsSuperscriptionHit(hit)) { panel.Children.Add(Label("Superscrição hebraica · texto ausente nesta biblioteca portuguesa.", 12, Muted)); return; }
            var parsing = await library.Parsing;
            var saved = translationLinks.Find(queryKey, hit.Verse, translation.Info.Id);
            VerseEntry? target = saved == null ? null : BibleLibrary.ResolveVerse(translation, saved.TargetBook, saved.TargetChapter, saved.TargetVerse);
            if (target != null && !TranslationLinks.Valid(saved!, target)) target = null;
            bool compatible = BibleLibrary.CanSynchronize(source, translation);
            var mapping = BibleLibrary.ResolveLinks(source, hit.Verse, translation);
            bool mapped = mapping.Targets.Count > 0;
            if (target == null && mapped) target = mapping.Targets[0];
            var body = new StackPanel(); panel.Children.Add(body);
            void ShowTarget(VerseEntry? verse, bool append = false)
            {
                if (!append) body.Children.Clear(); target = verse;
                if (verse == null) { body.Children.Add(Label(compatible ? "Sem correspondência." : "MT · escolha a referência portuguesa.", 12, Muted)); return; }
                if (mapping.Targets.Count > 1 || verse.Book != hit.Verse.Book || verse.Chapter != hit.Verse.Chapter || verse.VerseStart != hit.Verse.VerseStart || verse.VerseEnd != hit.Verse.VerseEnd) body.Children.Add(Label(verse.Reference, 10, Muted));
                // Rich formatting preserves selection, using a read-only RichTextBox for the parallel verse.
                var paragraph = new Paragraph { Margin = new(0) };
                if (SearchText.HasHebrew(verse.Text)) { paragraph.FontFamily = HebrewReadingFont; paragraph.FontSize = 19; }
                var link = translationLinks.Find(queryKey, hit.Verse, translation.Info.Id);
                var highlights = new List<SearchToken>();
                bool suggested = false;
                if (link != null && TranslationLinks.Valid(link, verse)) highlights.Add(new(link.Phrase, "", link.Start, link.Phrase.Length));
                if (!string.IsNullOrWhiteSpace(parallelEvidencePhrase)) highlights.AddRange(GlossParallels.Highlights(verse.Text, parallelEvidencePhrase));
                else if (highlights.Count == 0 && !SearchText.HasHebrew(searchedText) && !searchedText.Any(c => c is >= '\u0370' and <= '\u03FF') && !queryKey.StartsWith("4:") && !queryKey.StartsWith("5:"))
                {
                    var terms = SearchText.QueryTokens(searchedText);
                    highlights.AddRange(SearchText.Tokenize(verse.Text).Where(t => terms.Contains(t.Normalized)));
                }
                if (highlights.Count == 0)
                {
                    highlights.AddRange(ParallelHighlighting.SuggestForHit(verse.Text, hit, parsing)); suggested = highlights.Count > 0;
                }
                foreach (var span in SearchPresentation.Spans(new(verse, "", 0, highlights))) paragraph.Inlines.Add(new Run(span.Text) { FontWeight = span.Match ? FontWeights.Bold : FontWeights.Normal, Background = span.Match ? Brush("#F6E4A1") : Brushes.Transparent, ToolTip = span.Match && suggested ? "Realce sugerido pelas glossas e flexões portuguesas. Não é alinhamento confirmado; selecione e confirme pelo botão direito." : null });
                var rich = new RichTextBox { Document = new FlowDocument(paragraph) { Foreground = Theme.Get("#2D3730"), Background = Theme.Surface, PagePadding = new(2), FontSize = 14 }, IsReadOnly = true, BorderThickness = new(0), Background = Brushes.Transparent, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, MinHeight = 30, ToolTip = "Selecione uma expressão e use o botão direito para vinculá-la à pesquisa." };
                body.Children.Add(rich);
                void MarkSelection()
                {
                    string phrase = rich.Selection.Text.Trim();
                    int start = verse.Text.IndexOf(phrase, StringComparison.Ordinal);
                    if (phrase.Length == 0 || start < 0) { SetStatus("Selecione no paralelo a palavra ou expressão correspondente ao termo pesquisado."); return; }
                    if (verse.Text.IndexOf(phrase, start + phrase.Length, StringComparison.Ordinal) >= 0) { SetStatus("A seleção se repete neste versículo. Selecione uma expressão maior que identifique o trecho."); return; }
                    try { translationLinks.Save(queryKey, hit.Verse, verse, phrase, start); ShowTarget(verse); UpdateTranslationSummary(); SetStatus("Tradução marcada por você e incluída na contagem parcial de " + translation.Info.Id + "."); }
                    catch (Exception ex) { SetStatus("Não foi possível salvar a marcação: " + ex.Message); }
                }
                var menu = new ContextMenu();
                var copy = new MenuItem { Header = "Copiar seleção" }; copy.Click += (_, _) => rich.Copy(); menu.Items.Add(copy);
                var mark = new MenuItem { Header = "Vincular seleção à pesquisa" }; mark.Click += (_, _) => MarkSelection(); menu.Items.Add(mark);
                menu.Opened += (_, _) => { copy.IsEnabled = mark.IsEnabled = !string.IsNullOrWhiteSpace(rich.Selection.Text); };
                if (link != null)
                {
                    var undo = new MenuItem { Header = "Desfazer vínculo" }; undo.Click += (_, _) =>
                    {
                        try { translationLinks.Remove(queryKey, hit.Verse, translation.Info.Id); ShowTarget(verse); UpdateTranslationSummary(); SetStatus("Vínculo desfeito; a Bíblia permanece intacta."); }
                        catch (Exception ex) { SetStatus("Não foi possível desfazer: " + ex.Message); }
                    }; menu.Items.Add(undo);
                }
                rich.ContextMenu = menu;
            }
            ShowTarget(target);
            if (saved == null && mapping.Targets.Count > 1)
            {
                foreach (var additional in mapping.Targets.Skip(1)) ShowTarget(additional, true);
                body.Children.Add(Label("Um versículo da fonte corresponde aos versículos acima.", 10, Muted));
            }
            if (!compatible && (!mapped || target == null))
            {
                var manual = new StackPanel { Orientation = Orientation.Horizontal }; var reference = new TextBox { Width = 155, ToolTip = "Referência do versículo na tradução escolhida, por exemplo Gn 1:1" };
                manual.Children.Add(reference); manual.Children.Add(Button("Usar referência", () =>
                {
                    if (!BibleLibrary.TryReference(translation, reference.Text, out int b, out int c, out int? v) || !v.HasValue) { SetStatus("Informe uma referência completa existente na tradução, por exemplo Gn 1:1."); return; }
                    ShowTarget(BibleLibrary.ResolveVerse(translation, b, c, v.Value));
                })); panel.Children.Add(new Expander { Header = "Escolher referência…", Content = manual, FontSize = 11, IsExpanded = false });
            }
        }
        catch (Exception ex) { panel.Children.Add(Label("Paralelo indisponível: " + ex.Message, 11, Muted)); }
    }
    private async Task OpenHit(SearchHit hit)
    {
        var pane = ActivePane();
        var tab = pane.Tabs.FirstOrDefault(t => t.Kind == "bible" && t.Edition == hit.Verse.Edition);
        if (tab == null) { tab = new() { Edition = hit.Verse.Edition }; pane.Tabs.Add(tab); }
        if (hit.Verse.Edition == "BHS-A-Strong") tab.ReferenceSystem = "MT";
        tab.Book = hit.Verse.Book; tab.Chapter = hit.Verse.Chapter; tab.Verse = hit.Verse.VerseStart; tab.EntryIndex = hit.Verse.EntryIndex; tab.Scroll = 0;
        pane.ActiveTab = tab.Id; RenderWorkspace();
        if (readers.TryGetValue(tab.Id, out var view)) await view.ShowHit(hit);
        if (hit.Verse.IsMapped) await Synchronize(tab);
        Changed();
    }
    private void SearchHelp() => MessageBox.Show(this,
        "INTELIGENTE\nEncontra palavras inteiras com ou sem sinais vocálicos, cantilação e acentos. Preserva as consoantes e mostra as grafias originais.\n\nEXATA\nExige os sinais da palavra digitada. Maiúsculas e minúsculas são equivalentes.\n\nCOMEÇA COM / TRECHO\nProcuram prefixos e partes de palavras.\n\nVÁRIAS PALAVRAS\nTodas devem aparecer na mesma entrada, em qualquer ordem.\n\nSTRONG / LEMA\nConsultam as anotações reais da base, quando presentes. Strong identifica verbetes; não é um significado fixo. Códigos sem sufixo incluem as subdivisões da fonte.\n\nORIGINAIS\nBHS-A-Strong: hebraico segmentado, com numeração MT original. WLC-OSHB: texto hebraico com morfologia em versificação MT. SBLGNT: grego e variantes entre edições, sem Strong/morfologia nesta fonte. Clique em palavras para consultar os dados.\n\nO interlinear lexical não alinha artificialmente as traduções portuguesas. Nenhum texto é enviado à internet.", "Pesquisa no VERBUM", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string TabTitle(TabState state) => state.Kind switch { "exegesis" => "Exegese · " + (state.GuideReference.Length > 0 ? state.GuideReference : "passagem"), "library" => "Biblioteca", "book" => state.DocumentTitle, "lexicon" => "Léxico hebraico", "wordstudy" => "Estudo · " + (state.StudyTerm.Length > 0 ? state.StudyTerm : "palavra"), "notescatalog" => "Suas notas", "notes" => "✎ " + state.NoteTitle, "analysis" => "Palavra · " + (state.SelectedWord?.Text ?? "análise"), "apparatus" => "Variantes · " + state.SourceReference, _ => state.Edition };
    private void ShowOriginals()
    {
        var menu = new ContextMenu();
        foreach (var id in new[] { "BHS-A-Strong", "WLC-OSHB", "SBLGNT" })
        {
            var entry = new MenuItem { Header = id, IsEnabled = library.Editions.Any(e => e.Id == id) };
            entry.Click += (_, _) => AddTab(new() { Edition = id, Book = id == "SBLGNT" ? 42 : 0, Chapter = 1 }); menu.Items.Add(entry);
        }
        menu.IsOpen = true;
    }
    private void OpenWord(VerseEntry verse, OriginalWord word) => OpenTool(new() { Kind = "analysis", Edition = verse.Edition, Book = verse.Book, Chapter = verse.Chapter, Verse = verse.VerseStart, SelectedWord = word, SourceReference = verse.Reference });
    private void OpenVariants(VerseEntry verse) => OpenTool(new() { Kind = "apparatus", Edition = verse.Edition, Book = verse.Book, Chapter = verse.Chapter, Verse = verse.VerseStart, SourceReference = verse.Reference, VariantNotes = verse.Apparatus });
    private void OpenTool(TabState tool)
    {
        var source = ActivePane();
        var target = layout.Leaves().LastOrDefault(p => p.Id != source.Id);
        if (target == null)
        {
            Split(source, "horizontal"); target = ActivePane();
        }
        var existing = target.Tabs.FirstOrDefault(t => t.Kind == tool.Kind);
        if (existing != null) { int index = target.Tabs.IndexOf(existing); tool.Id = existing.Id; target.Tabs[index] = tool; }
        else target.Tabs.Add(tool);
        target.ActiveTab = tool.Id; activePane = target.Id; RenderWorkspace(); Changed();
    }
    private FrameworkElement BuildAnalysis(TabState state)
    {
        var content = new StackPanel { Margin = new(22) };
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        content.Children.Add(Label(state.Kind == "apparatus" ? "NOTAS E VARIANTES" : "ANÁLISE DA PALAVRA", 10, Green));
        content.Children.Add(new TextBlock { Text = state.Kind == "apparatus" ? state.SourceReference : state.SelectedWord?.Text ?? "", FontFamily = new("Times New Roman"), FontSize = 35, Margin = new(0, 12, 0, 7), TextWrapping = TextWrapping.Wrap });
        content.Children.Add(Label(state.SourceReference + " · " + state.Edition, 12, Muted));
        void AddSection(string name, string value)
        {
            content.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, Foreground = Green, Margin = new(0, 18, 0, 5) });
            content.Children.Add(new TextBox { Text = value, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new(0), Background = Brushes.Transparent, Padding = new(0), FontSize = 14 });
        }
        async Task Populate()
        {
            try
            {
                var edition = await library.LoadAsync(state.Edition);
                if (state.Kind == "apparatus")
                {
                    AddSection("Alcance da fonte", state.Edition == "SBLGNT" ? "Aparato SBLGNT: comparação entre edições publicadas. Não é o aparato de testemunhos manuscritos do NA28/UBS." : state.Edition == "WLC-OSHB" ? "Notas de leitura da fonte OSHB, incluindo qere/ketiv. Não é o aparato crítico impresso da BHS." : "Notas disponibilizadas por esta fonte; o aparato crítico da BHS não está incluído.");
                    if (state.VariantNotes.Count == 0) AddSection("Referência selecionada", "Esta fonte não fornece notas para a referência selecionada. Isso não significa ausência de variantes na tradição textual.");
                    foreach (var note in state.VariantNotes) AddSection("Leitura / nota original", note);
                    if (state.Edition == "SBLGNT") AddSection("Siglas e sinais", "WH: Westcott–Hort · Treg: Tregelles · RP: Robinson–Pierpont · NA28: Nestle–Aland (edição comparada) · NIV: texto subjacente à NIV · Holmes: editor SBLGNT.\n] separa leituras · + adição · – omissão. Preserve a nota integral ao citar.");
                }
                else if (state.SelectedWord is { } word)
                {
                    AddSection("Lema lexical", word.Lemma.Length > 0 ? word.Lemma : "Não fornecido por esta base.");
                    var buttons = new WrapPanel { Margin = new(0, 10, 0, 0) };
                    foreach (string code in word.Strongs)
                    {
                        buttons.Children.Add(Button(code + " · ocorrências", () => LexicalSearch(state.Edition, code, SearchMode.Strong)));
                        if (code.StartsWith("H900", StringComparison.Ordinal)) AddSection("Código estendido", code + " é uma extensão de mapeamento da fonte para segmentos como prefixos e partículas; não é um verbete convencional do Strong de 1894.");
                    }
                    content.Children.Add(buttons);
                    if (word.Strongs.Count == 0) AddSection("Strong", "Esta fonte não fornece código Strong para esta palavra.");
                    if (word.Lemma.Length > 0) content.Children.Add(Button("Pesquisar este lema", () => LexicalSearch(state.Edition, word.Lemma, SearchMode.Lemma)));
                    if (SearchText.HasHebrew(word.Text)) content.Children.Add(Button("Consultar outros léxicos", ShowHebrewLexicon));
                    AddSection("Morfologia", OriginalResource.MorphologySummary(word.Morphology));
                    if (word.MorphologySource.Length > 0) AddSection("Origem da morfologia", word.MorphologySource);
                    if (SearchText.HasHebrew(word.Text))
                    {
                        var catalog = await library.Parsing;
                        var sourceVerse = state.Verse.HasValue ? BibleLibrary.ResolveVerse(edition, state.Book, state.Chapter, state.Verse.Value) : null;
                        AddSection("Parsing e glossas do arquivo pessoal", catalog.Describe(sourceVerse ?? new VerseEntry(state.Edition, state.Book, edition.Books[state.Book].Name, "", state.Chapter, 0, state.Verse, state.Verse, "", true), word));
                        var context = sourceVerse ?? new VerseEntry(state.Edition, state.Book, edition.Books[state.Book].Name, "", state.Chapter, 0, state.Verse, state.Verse, "", true);
                        string gloss = WordGlosses.Summary(catalog.Find(context, word), word, edition.Resource, await library.Lexicons);
                        if (gloss.Length > 0) AddSection("Glossa / sentidos possíveis", gloss);
                    }
                    foreach (string code in word.Strongs)
                    {
                        if (edition.Resource?.Lexicon.TryGetValue(OriginalResource.LexiconKey(code), out var lex) == true)
                        {
                            AddSection(code + " · transliteração do léxico", lex.Transliteration);
                            AddSection(code + " · verbete Strong (inglês)", lex.Definition);
                            if (lex.Derivation.Length > 0) AddSection("Derivação segundo o léxico", lex.Derivation);
                        }
                    }
                    if (word.Reading.Length > 0) AddSection("Leitura / segmentação", word.Reading);
                    AddSection("Identificador da fonte", word.SourceId);
                    AddSection("Como interpretar", "Strong é um identificador lexical, não um significado fixo. O painel mostra dados da base; as traduções portuguesas não estão alinhadas palavra a palavra. Morfologia e lema ausentes não são inferidos automaticamente.");
                }
                if (edition.Resource is { } resource) AddSection("Fonte e atribuição", resource.Attribution + "\n" + resource.License + "\n" + resource.SourceUrl + "\nRevisão: " + resource.Revision);
            }
            catch (Exception ex) { AddSection("Recurso indisponível", ex.Message); }
        }
        _ = Populate(); return scroll;
    }
    private void LexicalSearch(string edition, string term, SearchMode selectedMode)
    {
        query.Text = term; scope.SelectedItem = edition; mode.SelectedIndex = (int)selectedMode; _ = RunSearch();
    }
}

