using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Verbum;

public sealed class ExegeticalGuideView : UserControl
{
    private readonly BibleLibrary library;
    private readonly TabState state;
    private readonly Action changed;
    private readonly Action<TabState> openNote;
    private readonly Action<VerseEntry, OriginalWord> openWord;
    private readonly Action<LexiconEntry, string> study;
    private readonly TextBox reference = new() { MinWidth = 150, MaxWidth = 310, ToolTip = "Passagem na edição original: Joel 1:1-3, Joel 4 ou João 1:1-5. Intervalos dentro do mesmo capítulo." };
    private readonly ComboBox source = new() { MinWidth = 115 }, parallel = new() { MinWidth = 95 }, part = new() { MinWidth = 110 };
    private readonly CheckBox allWords = new() { Content = "Todas as frequências", VerticalAlignment = VerticalAlignment.Center, Margin = new(6) };
    private readonly TextBox threshold = new() { Width = 55, ToolTip = "Mostrar lemas com até este número de ocorrências no texto original selecionado" };
    private readonly StackPanel output = new(), lexical = new();
    private readonly TextBlock status = MainWindow.Label("Escolha uma passagem para estudar.", 12, MainWindow.Muted);
    private readonly ScrollViewer scroll;
    private readonly ExegesisCatalog catalog;
    private int generation;
    private bool initialized, started;
    internal bool PreviewReady => initialized;
    private Expander? lexicalSection;
    private string export = "";

    public ExegeticalGuideView(BibleLibrary library, TabState state, Action changed, Action<TabState> openNote, Action<VerseEntry, OriginalWord> openWord, Action<LexiconEntry, string> study)
    {
        this.library = library; this.state = state; this.changed = changed; this.openNote = openNote; this.openWord = openWord; this.study = study; catalog = library.Exegesis;
        var root = new DockPanel { Margin = new(10) }; Content = root;
        var head = new StackPanel(); head.Children.Add(MainWindow.Label("Guia exegético", 17, MainWindow.Green));
        source.ItemsSource = library.Editions.Where(e => e.Id is "WLC-OSHB" or "SBLGNT").Select(e => e.Id).ToList(); source.SelectedItem = state.Edition; if (source.SelectedItem == null && source.Items.Count > 0) source.SelectedIndex = 0;
        source.ToolTip = "Edição original; WLC usa numeração hebraica MT";
        reference.Text = state.GuideReference.Length > 0 ? state.GuideReference : "Joel 1:1-3";
        parallel.ItemsSource = library.Editions.Where(e => e.Id is not ("WLC-OSHB" or "BHS-A-Strong" or "SBLGNT")).Select(e => e.Id).ToList(); parallel.SelectedItem = state.StudyTranslation; if (parallel.SelectedItem == null && parallel.Items.Count > 0) parallel.SelectedIndex = 0; parallel.ToolTip = "Tradução em paralelo por referência";
        allWords.IsChecked = state.GuideAllWords; allWords.Content = "Sem limite"; allWords.Margin = new(0, 0, 10, 0); threshold.Text = state.GuideThreshold.ToString();
        part.ItemsSource = new[] { "Todas as classes", "Verbos", "Substantivos", "Adjetivos", "Advérbios", "Pronomes", "Preposições", "Conjunções" }; part.SelectedIndex = Math.Clamp(state.GuidePart, 0, 7);
        foreach (var control in new Control[] { source, reference, parallel, part, threshold }) { control.Height = 28; control.MinHeight = 28; control.Margin = new(0); control.Padding = new(5, 2, 5, 2); control.VerticalContentAlignment = VerticalAlignment.Center; }
        source.MinWidth = 110; parallel.MinWidth = 90; reference.MinWidth = 35; reference.MaxWidth = double.PositiveInfinity; part.MinWidth = 130;
        Button Small(string title, Action action) { var button = MainWindow.Button(title, action); button.Height = 28; button.Margin = new(3, 0, 0, 0); button.Padding = new(8, 3, 8, 3); return button; }
        var navigation = new Grid { Margin = new(0, 5, 0, 3) }; navigation.ColumnDefinitions.Add(new()); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); navigation.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); navigation.Children.Add(reference);
        var generate = Small("Gerar", () => { state.GuidePage = 0; _ = Generate(); }); generate.ToolTip = "Gerar guia da referência · Enter"; generate.Background = Theme.Get("Primary"); generate.Foreground = Theme.Get("OnPrimary"); Grid.SetColumn(generate, 1); navigation.Children.Add(generate);
        var optionsBody = new StackPanel(); var sources = new WrapPanel();
        foreach (var pair in new[] { ("Original", source), ("Paralelo", parallel) }) { var field = new StackPanel { Margin = new(0, 0, 10, 6) }; field.Children.Add(MainWindow.Label(pair.Item1, 10, MainWindow.Muted)); field.Children.Add(pair.Item2); sources.Children.Add(field); }
        optionsBody.Children.Add(sources);
        var filters = new WrapPanel(); var frequency = new StackPanel { Orientation = Orientation.Horizontal, Height = 28, Margin = new(0, 0, 10, 6) }; frequency.Children.Add(allWords);
        var until = MainWindow.Label("Até", 12); until.VerticalAlignment = VerticalAlignment.Center; until.Margin = new(0, 0, 5, 0); frequency.Children.Add(until); frequency.Children.Add(threshold); filters.Children.Add(frequency);
        part.Margin = new(0, 0, 8, 6); filters.Children.Add(part); var apply = Small("Aplicar", () => _ = Generate()); apply.Margin = new(0, 0, 0, 6); filters.Children.Add(apply); optionsBody.Children.Add(filters);
        void ToggleLimit() { bool enabled = allWords.IsChecked != true; threshold.IsEnabled = enabled; until.Opacity = enabled ? 1 : .45; } allWords.Checked += (_, _) => ToggleLimit(); allWords.Unchecked += (_, _) => ToggleLimit(); ToggleLimit();
        var options = new Border { Child = optionsBody, Background = MainWindow.Paper, BorderBrush = MainWindow.Line, BorderThickness = new(1), CornerRadius = new(5), Padding = new(8), Margin = new(0, 4, 0, 4), Visibility = state.GuideOptionsExpanded ? Visibility.Visible : Visibility.Collapsed };
        var menu = new ContextMenu(); void Item(string label, Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item("Fontes e filtros…", () => { state.GuideOptionsExpanded = !state.GuideOptionsExpanded; options.Visibility = state.GuideOptionsExpanded ? Visibility.Visible : Visibility.Collapsed; changed(); });
        Item("Notas da passagem", () => { if (state.GuideReference.Length > 0) openNote(state); }); Item("Copiar resumo Markdown", () => { if (export.Length > 0) { Clipboard.SetText(export); status.Text = "Resumo Markdown copiado com as fontes."; } });
        var more = Small("⋯", () => menu.IsOpen = true); more.ToolTip = "Fontes, filtros, notas da passagem e exportação"; menu.PlacementTarget = more; Grid.SetColumn(more, 2); navigation.Children.Add(more); head.Children.Add(navigation); head.Children.Add(options);
        status.Margin = new(0, 2, 0, 3); status.FontSize = 11; head.Children.Add(status); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        scroll = new() { Content = output, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; root.Children.Add(scroll);
        scroll.ScrollChanged += (_, _) => { if (initialized && Math.Abs(state.Scroll - scroll.VerticalOffset) > 2) { state.Scroll = scroll.VerticalOffset; changed(); } };
        reference.KeyDown += (_, e) => { if (e.Key == Key.Enter) { state.GuidePage = 0; _ = Generate(); e.Handled = true; } };
        Loaded += (_, _) => { if (!started) { started = true; _ = Generate(true); } };
        Unloaded += (_, _) => { generation++; };
    }
    private Expander Section(string key, string title, UIElement content)
    {
        var expander = new Expander { Header = title, Content = content, IsExpanded = state.GuideExpanded.Contains(key), Margin = new(0, 10, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        expander.Expanded += (_, e) => { if (e.OriginalSource != expander) return; if (!state.GuideExpanded.Contains(key)) state.GuideExpanded.Add(key); changed(); };
        expander.Collapsed += (_, e) => { if (e.OriginalSource != expander) return; state.GuideExpanded.Remove(key); changed(); };
        output.Children.Add(expander); return expander;
    }
    private static TextBox Selectable(string text, double size = 14) => new() { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new(0), Background = Brushes.Transparent, Padding = new(5), FontSize = size };
    private async Task Generate(bool restoreScroll = false)
    {
        int epoch = ++generation; initialized = false;
        string editionId = source.SelectedItem as string ?? "", targetId = parallel.SelectedItem as string ?? "";
        if (editionId.Length == 0) { status.Text = "Não há texto original disponível nesta biblioteca."; return; }
        if (!int.TryParse(threshold.Text, out int maximum) || maximum < 1 || maximum > 1_000_000) { status.Text = "Informe um limite de frequência entre 1 e 1.000.000."; return; }
        status.Text = "Preparando passagem e fontes locais…";
        try
        {
            var edition = await library.LoadAsync(editionId);
            if (epoch != generation) return;
            if (!ExegesisData.TryPassage(edition, reference.Text, out var passage) || passage == null) { status.Text = "Referência inválida nesta edição. Use, por exemplo, Joel 1:1-3 (WLC) ou João 1:1-5 (SBLGNT)."; return; }
            var target = targetId.Length > 0 ? await library.LoadAsync(targetId) : null;
            var parsing = await library.Parsing; var lexicons = await library.Lexicons;
            var annotations = await Task.Run(() => catalog.Chapter(editionId, passage.Book, passage.Chapter));
            var verses = edition.Chapters[(passage.Book, passage.Chapter)].Where(v => v.IsMapped && v.VerseStart <= passage.End && v.VerseEnd >= passage.Start).ToList();
            int pages = Math.Max(1, (verses.Count + 9) / 10); int pageNumber = Math.Clamp(state.GuidePage, 0, pages - 1);
            var pageVerses = verses.Skip(pageNumber * 10).Take(10).ToList();
            var rows = await Task.Run(() => pageVerses.ToDictionary(v => v, v => ExegesisData.Rows(edition, v, annotations?.Verses.FirstOrDefault(a => a.Verse == v.VerseStart), parsing, lexicons)));
            var comments = passage.Book < 39 ? await Task.Run(() => pageVerses.SelectMany(v => catalog.Comments(v)).DistinctBy(c => c.Id).ToList()) : [];
            // Frequency counts are words with the same lemma in this edition, never verse counts.
            var counts = await Task.Run(() => edition.Entries.SelectMany(v => v.Words).Where(w => w.Lemma.Length > 0).GroupBy(w => w.Lemma).ToDictionary(g => g.Key, g => g.Count()));
            var maculaCounts = await Task.Run(() => catalog.Frequencies(editionId));
            if (epoch != generation) return;
            state.Edition = editionId; state.Book = passage.Book; state.Chapter = passage.Chapter; state.Verse = passage.Start; state.GuideEndVerse = passage.End; state.GuideReference = passage.Label; state.StudyTranslation = targetId; state.GuideAllWords = allWords.IsChecked == true; state.GuideThreshold = maximum; state.GuidePart = part.SelectedIndex; state.GuidePage = pageNumber; state.ReferenceSystem = edition.Resource?.ReferenceSystem ?? "";
            reference.Text = passage.Label; changed();
            if (lexicalSection != null) { lexicalSection.Content = null; lexicalSection = null; }
            output.Children.Clear(); lexical.Children.Clear();
            status.Text = passage.Label + " · " + editionId + (passage.Book < 39 ? " · numeração MT" : "") + $" · página {pageNumber + 1}/{pages}";
            var md = new StringBuilder("# Guia exegético — " + passage.Label + "\n\nFonte do texto: " + editionId + ".\n\n");
            if (pages > 1)
            {
                var paging = new WrapPanel(); var prev = MainWindow.Button("‹ Passagens anteriores", () => { state.GuidePage--; _ = Generate(); }); prev.IsEnabled = pageNumber > 0;
                var next = MainWindow.Button("Próximas passagens ›", () => { state.GuidePage++; _ = Generate(); }); next.IsEnabled = pageNumber + 1 < pages; paging.Children.Add(prev); paging.Children.Add(next); paging.Children.Add(MainWindow.Label("10 versículos por página; o resumo copia esta página.", 12, MainWindow.Muted)); output.Children.Add(paging);
            }
            var texts = new StackPanel();
            foreach (var verse in pageVerses)
            {
                texts.Children.Add(MainWindow.Label(verse.Reference + " · " + editionId, 12, MainWindow.Green)); var original = Selectable(verse.Text, 22); original.FontFamily = SearchText.HasHebrew(verse.Text) ? MainWindow.HebrewReadingFont : new("Times New Roman"); if (SearchText.HasHebrew(verse.Text)) original.FlowDirection = FlowDirection.RightToLeft; texts.Children.Add(original); md.AppendLine("## " + verse.Reference).AppendLine().AppendLine(verse.Text).AppendLine();
                if (target != null)
                {
                    var link = BibleLibrary.ResolveLinks(edition, verse, target);
                    texts.Children.Add(Selectable(link.Targets.Count > 0 ? string.Join("\n", link.Targets.Select(t => t.Reference + " · " + targetId + "\n" + t.Text)) : link.Description));
                    md.AppendLine(link.Targets.Count > 0 ? string.Join("\n\n", link.Targets.Select(t => t.Reference + " · " + targetId + ": " + t.Text)) : link.Description).AppendLine();
                }
            }
            Section("text", "Texto original e paralelo", texts);
            var words = new StackPanel(); int total = 0, covered = 0, shown = 0;
            words.Children.Add(MainWindow.Label("Glossas são auxiliares de leitura; não são alinhamento com a tradução portuguesa. Clique numa palavra para consultar os verbetes.", 12, MainWindow.Muted));
            foreach (var verse in pageVerses)
            {
                words.Children.Add(MainWindow.Label(verse.Reference, 16, MainWindow.Green)); md.AppendLine("### Palavra por palavra · " + verse.Reference).AppendLine();
                var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star), MinWidth = 65 }); grid.ColumnDefinitions.Add(new() { Width = new(1.6, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = new(2, GridUnitType.Star) });
                AddCells(grid, 0, [MainWindow.Label("Palavra / lema", 11, MainWindow.Green), MainWindow.Label("Parsing", 11, MainWindow.Green), MainWindow.Label("Glossa / fonte", 11, MainWindow.Green)]);
                foreach (var row in rows[verse])
                {
                    total++; if (row.Annotations.Count > 0) covered++;
                    bool nativeCount = row.Word.Lemma.Length > 0;
                    string countLemma = nativeCount ? row.Word.Lemma : row.Annotations.Count == 1 ? row.Annotations[0].Lemma : "";
                    int? count = countLemma.Length > 0 && (nativeCount ? counts : maculaCounts).TryGetValue(countLemma, out int n) ? n : null;
                    if (!state.GuideAllWords && (!count.HasValue || count.Value > maximum)) continue;
                    if (!MatchesPart(row, state.GuidePart)) continue;
                    shown++; var form = new StackPanel(); var button = MainWindow.Button(row.Word.Text, () => ShowLexicon(edition, verse, row, lexicons)); button.FontFamily = SearchText.HasHebrew(row.Word.Text) ? MainWindow.HebrewReadingFont : new("Times New Roman"); button.FontSize = 22; button.Padding = new(3); button.HorizontalAlignment = HorizontalAlignment.Left; form.Children.Add(button);
                    string lemma = row.Word.Lemma.Length > 0 ? row.Word.Lemma : string.Join(" + ", row.Annotations.Select(a => a.Lemma).Where(s => s.Length > 0).Distinct()); var lemmaLabel = MainWindow.Label(lemma, 15); if (SearchText.HasHebrew(lemma)) lemmaLabel.FontFamily = MainWindow.HebrewReadingFont; form.Children.Add(lemmaLabel); if (count.HasValue) form.Children.Add(MainWindow.Label(count + (nativeCount ? " ocorrências na edição" : " ocorrências no MACULA"), 10, MainWindow.Muted));
                    var parsingText = Selectable(row.Parsing, 12); var gloss = Selectable(row.Gloss, 13); AddCells(grid, grid.RowDefinitions.Count, [form, parsingText, gloss]);
                    md.AppendLine("- **" + row.Word.Text + "** — " + lemma + "; " + row.Parsing.Replace("\n", "; ") + "; " + row.Gloss);
                }
                words.Children.Add(grid); if (rows[verse].Count == 0) words.Children.Add(MainWindow.Label("Esta edição não fornece palavras anotadas para a entrada.", 12, MainWindow.Muted));
            }
            words.Children.Insert(0, MainWindow.Label($"{shown} palavras exibidas; {covered}/{total} com correspondência exata ao MACULA nesta página.", 12, MainWindow.Green));
            if (!state.GuideAllWords) words.Children.Add(MainWindow.Label("Sem lema identificável não há contagem comparável: essas palavras ficam fora do filtro de frequência. Use Todas as frequências para vê-las.", 12, MainWindow.Muted));
            Section("words", "Palavra por palavra", words);
            lexical.Children.Add(MainWindow.Label("Clique numa palavra acima. Os verbetes são candidatos lexicais; os sentidos não são escolhidos automaticamente.", 12, MainWindow.Muted)); lexicalSection = Section("lexicon", "Consulta lexical", lexical);
            var syntax = new StackPanel(); syntax.Children.Add(MainWindow.Label("Estrutura anotada pelo MACULA. Os nomes em português descrevem classes; as regras da fonte são mantidas. Grupos que atravessam versículos mostram apenas a parte na passagem.", 12, MainWindow.Muted));
            foreach (var verse in pageVerses)
            {
                var a = annotations?.Verses.FirstOrDefault(v => v.Verse == verse.VerseStart); syntax.Children.Add(MainWindow.Label(verse.Reference, 15, MainWindow.Green));
                if (a == null || a.Groups.Count == 0) { syntax.Children.Add(MainWindow.Label("Sem estrutura sintática disponível nesta fonte.", 12, MainWindow.Muted)); continue; }
                var admitted = rows[verse].SelectMany(r => r.Annotations).Select(w => w.Id).ToHashSet();
                int rendered = 0;
                foreach (var group in a.Groups.Where(g => g.Words.All(admitted.Contains)))
                {
                    string label = string.Join(" · ", new[] { ExegesisData.Translate(group.Kind), ExegesisData.Translate(group.Role), group.Rule }.Where(s => s.Length > 0).Distinct());
                    var panel = new StackPanel { Margin = new(Math.Min(group.Depth, 6) * 10, 2, 0, 5) }; panel.Children.Add(MainWindow.Label(label, 11, MainWindow.Green));
                    string text = string.Join(" ", a.Words.Where(w => group.Words.Contains(w.Id)).GroupBy(w => w.Order).Select(g => string.Concat(g.Select(w => w.Text)))); var phrase = Selectable(text, 17); if (verse.Book < 39) { phrase.FlowDirection = FlowDirection.RightToLeft; phrase.FontFamily = MainWindow.HebrewReadingFont; } panel.Children.Add(phrase); syntax.Children.Add(panel); rendered++;
                }
                if (rendered == 0) syntax.Children.Add(MainWindow.Label("A forma da fonte não corresponde integralmente ao texto aberto; a estrutura não foi atribuída por aproximação.", 12, MainWindow.Muted));
            }
            Section("syntax", "Estrutura sintática · MACULA", syntax);
            var variants = new StackPanel(); md.AppendLine().AppendLine("## Variantes e comentários textuais").AppendLine();
            var apparatus = pageVerses.Where(v => v.Apparatus.Count > 0).ToList();
            foreach (var verse in apparatus) { variants.Children.Add(MainWindow.Label(verse.Reference + " · notas da fonte " + editionId, 13, MainWindow.Green)); variants.Children.Add(Selectable(string.Join("\n\n", verse.Apparatus))); md.AppendLine(string.Join("\n\n", verse.Apparatus)); }
            foreach (var comment in comments)
            {
                string refs = string.Join("; ", comment.References.Select(r => BibleLibrary.CanonicalNames[r.Book] + $" {r.Chapter}:{r.Verse}"));
                var text = new StringBuilder("UBS HOTTP · " + comment.Id + " · inglês\nReferências da entrada: " + refs + (comment.Remark.Length > 0 ? "\n\n" + comment.Remark : "") + (comment.Suggestion.Length > 0 ? "\n\nProposta da fonte: " + comment.Suggestion : ""));
                foreach (var alternative in comment.Alternatives) text.Append("\n\n" + alternative.Text + (alternative.Rating.Length > 0 ? " · avaliação " + alternative.Rating : "") + "\n" + alternative.Literal + (alternative.Factors.Count > 0 ? "\nFatores da fonte: " + string.Join(", ", alternative.Factors) : "") + (alternative.Versions.Count > 0 ? "\n" + string.Join("\n", alternative.Versions) : ""));
                variants.Children.Add(new Expander { Header = "HOTTP " + comment.Id + " · " + refs, Content = Selectable(text.ToString()), Margin = new(0, 5, 0, 8) }); md.AppendLine(text.ToString()).AppendLine();
            }
            if (comments.Count == 0 && apparatus.Count == 0) variants.Children.Add(MainWindow.Label("Nenhuma nota localizada para esta página nas fontes carregadas. Isso não prova ausência de variantes.", 12, MainWindow.Muted));
            variants.Children.Add(MainWindow.Label("HOTTP é um relatório de crítica textual, não o aparato completo BHS/NA28. As avaliações A–D são da comissão, não do VERBUM. As referências e alternativas da entrada são preservadas.", 12, MainWindow.Muted));
            variants.Children.Add(MainWindow.Button("Critérios A–D e fatores HOTTP", ShowHottpHelp));
            Section("variants", "Variantes e comentários textuais", variants);
            var sources = new StackPanel(); string credits = (annotations != null ? annotations.Source + " · CC BY 4.0 nas camadas selecionadas\nRevisão: " + annotations.Revision : "MACULA não disponível para este capítulo") + "\n\nMACULA Hebrew: sintaxe Westminster/Groves Center, morfologia Open Scriptures e glossas inglesas Cherith. MACULA Greek: sintaxe Clear/Biblica e glossas Cherith sobre SBLGNT. Campos semânticos MARBLE/SDBH usados sob permissão não foram importados.\n\nUBS HOTTP © United Bible Societies 1973–1980, 2003–2023 · CC BY-SA 4.0. Revisão a15a96cfaa4ca18a6c0339016645ba1e534db3c8.\n\nPortuguês pessoal: glossas automáticas provisórias; Strong/BDB/UBS: definições das fontes, sem seleção automática de sentido. O paralelo português usa o mapa de referências já existente.\n\nFontes, licenças e manifesto ficam em biblioteca/fontes e biblioteca/exegese/manifest.json. Não há classificação automática de palavras importantes nem alinhamento português novo.";
            sources.Children.Add(Selectable(credits, 12)); Section("sources", "Fontes e limites", sources); md.AppendLine().AppendLine("## Fontes").AppendLine().AppendLine(credits); export = md.ToString();
            double savedScroll = restoreScroll ? state.Scroll : 0;
            await Dispatcher.InvokeAsync(() => { if (epoch == generation) { scroll.ScrollToVerticalOffset(savedScroll); initialized = true; } }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex) { if (epoch == generation) status.Text = "Não foi possível gerar o guia: " + ex.Message; }
    }
    private static bool MatchesPart(ExegesisRow row, int index)
    {
        if (index <= 0) return true;
        string[] parts = index switch { 1 => ["verb"], 2 => ["noun"], 3 => ["adj", "adjective"], 4 => ["adv", "adverb"], 5 => ["pron", "pronoun"], 6 => ["prep", "preposition"], 7 => ["conj", "conjunction"], _ => [] };
        return row.Annotations.Any(a => parts.Contains(a.PartOfSpeech));
    }
    private static void AddCells(Grid grid, int row, UIElement[] cells)
    {
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int c = 0; c < cells.Length; c++) { var border = new Border { Child = cells[c], Padding = new(4), BorderBrush = MainWindow.Line, BorderThickness = new(0, 0, 0, 1), Background = row % 2 == 0 ? MainWindow.Paper : Theme.Surface }; Grid.SetRow(border, row); Grid.SetColumn(border, c); grid.Children.Add(border); }
    }
    private void ShowLexicon(BibleEdition edition, VerseEntry verse, ExegesisRow row, List<LexicalResource> resources)
    {
        lexical.Children.Clear(); lexical.Children.Add(MainWindow.Label(row.Word.Text, 25, MainWindow.Green)); lexical.Children.Add(Selectable(row.Gloss)); lexical.Children.Add(Selectable(row.Parsing));
        var codes = row.Word.Strongs.Select(OriginalResource.LexiconKey).ToHashSet();
        if (row.LexicalKey.Length > 0) codes.Add(OriginalResource.LexiconKey(row.LexicalKey));
        var tools = new WrapPanel(); tools.Children.Add(MainWindow.Button("Análise da palavra", () => openWord(verse, row.Word)));
        if (row.Word.Strongs.Count > 0 || row.Word.Lemma.Length > 0) tools.Children.Add(MainWindow.Button("Estudar ocorrências", () => study(new LexiconEntry { Code = row.Word.Strongs.FirstOrDefault() ?? row.LexicalKey, Lemma = row.Word.Lemma }, edition.Info.Id)));
        lexical.Children.Add(tools); int found = 0;
        foreach (string code in codes)
            if (edition.Resource?.Lexicon.TryGetValue(code, out var entry) == true) { lexical.Children.Add(MainWindow.Label(code + " · Strong · inglês", 13, MainWindow.Green)); lexical.Children.Add(Selectable(entry.Definition)); found++; }
        foreach (var resource in resources.Where(r => r.Id is "UBS-PT" or "BDB"))
        foreach (var entry in resource.Entries.Where(e => e.Codes.Any(c => codes.Contains(OriginalResource.LexiconKey(c)))))
        { lexical.Children.Add(new Expander { Header = resource.Title + " · " + entry.Id + " · " + entry.Lemma, Content = Selectable(entry.Definition), Margin = new(0, 7, 0, 5) }); found++; }
        if (found == 0) lexical.Children.Add(MainWindow.Label("Nenhum verbete relacionado encontrado nos léxicos carregados. A glossa MACULA pode estar disponível independentemente de um dicionário.", 12, MainWindow.Muted));
        if (lexicalSection != null) { lexicalSection.IsExpanded = true; lexicalSection.BringIntoView(); }
    }
    private void ShowHottpHelp()
    {
        string dir = Path.Combine(Path.GetDirectoryName(library.DirectoryPath)!, "fontes", "UBS-HOTTP");
        var panel = new StackPanel { Margin = new(15) };
        foreach (string name in new[] { "HOTTP-RATINGS-EN.HTML", "HOTTP-FACTORS-EN.HTML" })
        {
            string path = Path.Combine(dir, name); if (!File.Exists(path)) continue;
            string text = File.ReadAllText(path, Encoding.Latin1); text = System.Text.RegularExpressions.Regex.Replace(text, @"(?is)<(style|script)\b[^>]*>.*?</\1>", ""); text = System.Text.RegularExpressions.Regex.Replace(text, @"(?i)</?(p|h[1-6]|br)\b[^>]*>", "\n"); text = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "")); panel.Children.Add(Selectable(text));
        }
        new Window { Owner = Window.GetWindow(this), Title = "UBS HOTTP · critérios e fatores da comissão · inglês", Width = 750, Height = 650, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
    internal async Task WritePreview(string path, double previewWidth = 1060)
    {
        await Generate();
        await Generate(); // Exercise regeneration without leaving a reused lexical panel attached.
        if (!initialized || export.Length == 0) throw new InvalidOperationException(status.Text);
        if (state.GuideExpanded.Contains("variants"))
            foreach (var nested in output.Children.OfType<Expander>().Where(e => e.Header.ToString()!.StartsWith("Variantes")).SelectMany(e => ((StackPanel)e.Content).Children.OfType<Expander>()).Take(1)) nested.IsExpanded = true;
        Width = previewWidth; Height = 900; Background = Theme.Surface;
        Measure(new Size(previewWidth, 900)); Arrange(new Rect(0, 0, previewWidth, 900)); UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)previewWidth, 900, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); using var file = File.Create(path); encoder.Save(file);
        File.WriteAllText(Path.ChangeExtension(path, ".md"), export);
    }
}
