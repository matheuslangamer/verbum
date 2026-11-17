using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Verbum;

public sealed record HebrewLexeme(LexiconEntry Entry, HashSet<string> Forms, bool Extended = false, string ResourceId = "Strong", string RecordId = "", List<string>? Codes = null)
{
    public override string ToString() => Entry.Code + "   " + Entry.Lemma;
}
public static class HebrewLexiconData
{
    public static List<HebrewLexeme> Build(IEnumerable<BibleEdition> editions)
    {
        var data = editions.ToList();
        var entries = data.Where(e => e.Resource != null).SelectMany(e => e.Resource!.Lexicon.Values)
            .Where(e => e.Code.StartsWith('H')).DistinctBy(e => e.Code).ToDictionary(e => e.Code, e => new HebrewLexeme(e, []));
        foreach (var word in data.SelectMany(e => e.Entries).SelectMany(v => v.Words))
        foreach (var raw in word.Strongs)
        {
            string code = OriginalResource.LexiconKey(raw);
            if (!code.StartsWith('H')) continue;
            if (!entries.TryGetValue(code, out var item)) entries[code] = item = new(new LexiconEntry { Code = code, Lemma = word.Lemma, Definition = "A fonte não fornece um verbete lexical para este código." }, [], true);
            item.Forms.Add(SearchText.Normalize(word.Text));
            foreach (var segment in word.Segments) item.Forms.Add(SearchText.Normalize(segment.Text));
        }
        return entries.Values.OrderBy(e => int.TryParse(e.Entry.Code.AsSpan(1), out int n) ? n : int.MaxValue).ToList();
    }
    public static List<HebrewLexeme> Find(IEnumerable<HebrewLexeme> items, string query)
    {
        string text = query.Trim(); if (text.Length == 0) return items.ToList();
        if (Regex.IsMatch(text, @"(?i)^H?\d{1,5}[a-z]?$") )
        {
            string code = OriginalResource.LexiconKey(OriginalResource.ParseStrong(text).Single());
            return items.Where(i => (i.Codes ?? [i.Entry.Code]).Contains(code, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        string key = SearchText.Normalize(text);
        if (key.Length == 0) return [];
        return items.Where(i => SearchText.Normalize(i.Entry.Lemma).Contains(key, StringComparison.Ordinal)
            || i.Forms.Contains(key)
            || SearchText.Normalize(i.Entry.Transliteration).Contains(key, StringComparison.Ordinal)
            || SearchText.Normalize(i.Entry.Definition).Contains(key, StringComparison.Ordinal)).ToList();
    }
}

public sealed class HebrewLexiconView : UserControl
{
    private readonly BibleLibrary library;
    private readonly TabState state;
    private readonly Action changed;
    private readonly Action<LexiconEntry, string> study;
    private readonly TextBox query = new() { FontSize = 17, ToolTip = "Hebraico com ou sem sinais, Strong (H7225), definição ou número de página no PDF pessoal" };
    private readonly ListBox list = new() { DisplayMemberPath = "", MinWidth = 150 };
    private readonly ComboBox sources = new() { Margin = new(0, 0, 0, 10), ToolTip = "Escolha o léxico" };
    private List<LexicalResource> resources = [];
    private readonly StackPanel detail = new() { Margin = new(18) };
    private readonly ScrollViewer detailScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly TextBlock count = new() { Foreground = MainWindow.Muted, FontSize = 11, Margin = new(0, 6, 0, 12) };
    private List<HebrewLexeme> all = [];
    private bool started;
    private bool loaded;
    private string sourceId = "WLC-OSHB";
    public HebrewLexiconView(BibleLibrary library, TabState state, Action changed, Action<LexiconEntry, string> study)
    {
        this.library = library; this.state = state; this.changed = changed; this.study = study;
        var root = new DockPanel { Margin = new(10) }; Content = root;
        var header = new StackPanel(); header.Children.Add(MainWindow.Label("Léxico hebraico", 17, MainWindow.Green));
        sources.Height = sources.MinHeight = 28; sources.Margin = new(0, 5, 0, 5); sources.Padding = new(5, 2, 5, 2); query.Height = 30; query.Padding = new(6, 3, 6, 3); query.FontSize = 15;
        header.Children.Add(sources);
        query.Text = state.LexiconQuery; header.Children.Add(query); header.Children.Add(count); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(.35, GridUnitType.Star), MinWidth = 150 }); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });
        grid.Children.Add(list); detailScroll.Content = detail; Grid.SetColumn(detailScroll, 1); grid.Children.Add(detailScroll); root.Children.Add(grid);
        query.TextChanged += (_, _) => { state.LexiconQuery = query.Text; changed(); if (loaded) Filter(); };
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is HebrewLexeme item) { state.LexiconCode = item.RecordId.Length > 0 ? item.RecordId : item.Entry.Code; Show(item); changed(); } };
        sources.SelectionChanged += (_, _) => { if (sources.SelectedItem is LexicalResource resource) { state.LexiconSource = resource.Id; changed(); if (loaded) Filter(); } };
        Loaded += async (_, _) => { if (started) return; started = true; await Load(); };
    }
    private async Task Load()
    {
        count.Text = "Carregando os verbetes e as formas do acervo…";
        try
        {
            var editions = new List<BibleEdition>();
            foreach (var id in new[] { "WLC-OSHB", "BHS-A-Strong" })
                if (library.Editions.Any(e => e.Id == id)) editions.Add(await library.LoadAsync(id));
            if (editions.Count == 0) { count.Text = "Nenhuma base hebraica disponível nesta biblioteca."; return; }
            sourceId = editions[0].Info.Id; all = await System.Threading.Tasks.Task.Run(() => HebrewLexiconData.Build(editions));
            resources = await library.Lexicons;
            var forms = all.ToDictionary(i => i.Entry.Code, i => i.Forms);
            foreach (var resource in resources)
            foreach (var record in resource.Entries)
                all.Add(new(new LexiconEntry { Code = record.Codes.FirstOrDefault() ?? record.Id, Lemma = record.Lemma, Definition = record.Definition },
                    record.Codes.SelectMany(c => forms.GetValueOrDefault(c) ?? []).ToHashSet(), false, resource.Id, record.Id, record.Codes));
            sources.ItemsSource = new[] { new LexicalResource { Id = "Strong", Title = "Strong · inglês" } }.Concat(resources).ToList();
            sources.SelectedItem = sources.Items.Cast<LexicalResource>().FirstOrDefault(r => r.Id == state.LexiconSource) ?? sources.Items[0];
            loaded = true; Filter();
        }
        catch (Exception ex) { count.Text = "Não foi possível carregar o léxico: " + ex.Message; }
    }
    private void Filter()
    {
        var available = all.Where(i => i.ResourceId == state.LexiconSource).ToList();
        var found = state.LexiconSource == "HOLLADAY-LOCAL" && int.TryParse(query.Text.Trim(), out int page)
            ? available.Where(i => i.RecordId == page.ToString()).ToList() : HebrewLexiconData.Find(available, query.Text); string saved = state.LexiconCode;
        list.ItemsSource = found;
        count.Text = $"{found.Count:N0} de {available.Count:N0} registros · busca ignora sinais" + (state.LexiconSource == "HOLLADAY-LOCAL" ? " · páginas / OCR; prefira buscar em português" : "");
        list.SelectedItem = found.FirstOrDefault(i => (i.RecordId.Length > 0 ? i.RecordId : i.Entry.Code) == saved) ?? found.FirstOrDefault(i => i.Entry.Code == saved) ?? found.FirstOrDefault();
        if (list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
        else { detail.Children.Clear(); detail.Children.Add(MainWindow.Label("Nenhum verbete encontrado. Experimente o lema, a forma completa ou o código Strong.", 13)); }
    }
    private void Show(HebrewLexeme item)
    {
        detail.Children.Clear(); var entry = item.Entry;
        detailScroll.ScrollToTop();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => detailScroll.ScrollToTop()));
        if (item.ResourceId != "Strong")
        {
            var resource = resources.Single(r => r.Id == item.ResourceId);
            detail.Children.Add(MainWindow.Label(resource.Title + " · " + item.RecordId, 12, MainWindow.Green));
            detail.Children.Add(new TextBlock { Text = entry.Lemma, FontFamily = new("Times New Roman"), FontSize = 32, TextWrapping = TextWrapping.Wrap });
            if (item.Codes?.Count > 0) Section("Códigos relacionados pela fonte", string.Join(" · ", item.Codes));
            Definition(entry.Definition);
            if (item.Codes?.FirstOrDefault() is { } code)
                detail.Children.Add(MainWindow.Button("Ver ocorrências / estudar palavra", () => study(new LexiconEntry { Code = code, Lemma = entry.Lemma }, sourceId)));
            if (resource.LocalDocument.Length > 0)
                detail.Children.Add(MainWindow.Button("Abrir PDF pessoal", () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(resource.LocalDocument) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show("Não foi possível abrir o PDF: " + ex.Message); } }));
            Section("Fonte e limites", resource.Attribution + "\n" + resource.License + "\n" + resource.SourceUrl + "\nRevisão: " + resource.Revision + "\nDefinições lexicais não são parsing da ocorrência nem alinhamento confirmado com a ARA.");
            return;
        }
        detail.Children.Add(MainWindow.Label(entry.Code + (item.Extended ? " · código da fonte sem verbete Strong" : " · Strong hebraico/aramaico"), 12, MainWindow.Green));
        detail.Children.Add(new TextBlock { Text = entry.Lemma.Length > 0 ? entry.Lemma : entry.Code, FontFamily = new("Times New Roman"), FontSize = 38, Margin = new(0, 10, 0, 9), TextWrapping = TextWrapping.Wrap });
        if (entry.Transliteration.Length > 0) detail.Children.Add(MainWindow.Label(entry.Transliteration, 17));
        Section("Definição da fonte · inglês", entry.Definition);
        if (entry.Derivation.Length > 0) Section("Derivação segundo a fonte", entry.Derivation);
        string studySource = item.Extended && library.Editions.Any(e => e.Id == "BHS-A-Strong") ? "BHS-A-Strong" : sourceId;
        detail.Children.Add(MainWindow.Button("Ver ocorrências / estudar palavra", () => study(entry, studySource)));
        ShowForms(item.Forms);
        Section("Sobre o léxico", "Léxico de James Strong (1894), edição digital Open Scriptures / David Troidl e David Instone-Brewer. As definições disponíveis estão em inglês e não foram traduzidas automaticamente. Strong reúne verbetes, não um significado fixo para toda ocorrência. Códigos estendidos são identificados separadamente. Formas flexionadas são cruzadas apenas com os códigos fornecidos pelos textos locais.");
    }
    private void Section(string title, string text)
    {
        detail.Children.Add(new TextBlock { Text = title, FontSize = 11, Foreground = MainWindow.Green, Margin = new(0, 18, 0, 6) });
        detail.Children.Add(SelectableText(text));
    }
    private static TextBox SelectableText(string text) => new() { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new(0), Background = System.Windows.Media.Brushes.Transparent, Padding = new(0), FontSize = 14 };
    private void ShowForms(HashSet<string> forms)
    {
        if (forms.Count == 0) return;
        var sorted = forms.Order().ToList();
        Section($"Formas no acervo · {sorted.Count} no total · sem sinais para comparação", string.Join(" · ", sorted.Take(35)));
        if (sorted.Count <= 35) return;
        var expander = new Expander { Header = $"Mostrar outras {sorted.Count - 35} formas", Margin = new(0, 8, 0, 0), Content = SelectableText(string.Join(" · ", sorted.Skip(35))) };
        expander.Expanded += (_, _) => expander.Header = $"Ocultar outras {sorted.Count - 35} formas";
        expander.Collapsed += (_, _) => expander.Header = $"Mostrar outras {sorted.Count - 35} formas";
        detail.Children.Add(expander);
    }
    internal static string[] DefinitionBlocks(string text) => Regex.Split(text, @"(?m)^(Referências da fonte \(BBBCCCVVVSSWWW\):[^\r\n]*)");
    internal static string ReferenceLabel(string code)
    {
        if (!Regex.IsMatch(code, @"^\d{14}$")) return "Referência não reconhecida: " + code;
        int book = int.Parse(code[..3]), chapter = int.Parse(code.Substring(3, 3)), verse = int.Parse(code.Substring(6, 3));
        return book is >= 1 and <= 39 && chapter > 0 && verse >= 0
            ? $"{BibleLibrary.CanonicalNames[book - 1]} {chapter}:{verse}" : "Referência não reconhecida: " + code;
    }
    private void Definition(string text)
    {
        Section("Verbete / texto da fonte", "");
        foreach (string block in DefinitionBlocks(text).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            if (block.StartsWith("Referências da fonte (BBBCCCVVVSSWWW):", StringComparison.Ordinal))
            {
                var codes = block[(block.IndexOf(':') + 1)..].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var references = codes.Select(ReferenceLabel).Distinct().ToList();
                var textBox = SelectableText(string.Join(" · ", references));
                textBox.MaxHeight = 220; textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                textBox.Margin = new(0, 8, 0, 8);
                textBox.ToolTip = "Numeração da fonte UBS. Diferentes elementos do mesmo versículo são reunidos nesta lista.\nCódigos originais: " + string.Join(", ", codes);
                var expander = new Expander { Header = $"Referências deste sentido · {references.Count} versículos · mostrar", Margin = new(0, 8, 0, 12), Content = textBox };
                expander.Expanded += (_, _) => expander.Header = $"Referências deste sentido · {references.Count} versículos · ocultar";
                expander.Collapsed += (_, _) => expander.Header = $"Referências deste sentido · {references.Count} versículos · mostrar";
                detail.Children.Add(expander);
            }
            else detail.Children.Add(SelectableText(block.Trim()));
        }
    }
}
