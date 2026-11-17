using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Verbum;

public sealed record WordStudySlice(string Phrase, List<SearchHit> Hits, bool Unmarked = false);
public static class WordStudyData
{
    public static List<WordStudySlice> Group(IEnumerable<SearchHit> hits, string key, BibleEdition target, TranslationLinks links)
    {
        var rows = hits.DistinctBy(h => (h.Verse.Edition, h.Verse.Book, h.Verse.Chapter, h.Verse.EntryIndex)).Select(h =>
        {
            var link = links.Find(key, h.Verse, target.Info.Id);
            bool valid = !(BibleLibrary.IsSuperscriptionHit(h) && target.Resource?.ReferenceSystem != "MT") && link != null && BibleLibrary.ResolveVerse(target, link.TargetBook, link.TargetChapter, link.TargetVerse) is { } verse && TranslationLinks.Valid(link, verse);
            return (Hit: h, Phrase: valid ? link!.Phrase : null);
        }).ToList();
        var slices = rows.Where(r => r.Phrase != null).GroupBy(r => r.Phrase!).Select(g => new WordStudySlice(g.Key, g.Select(r => r.Hit).ToList())).OrderByDescending(s => s.Hits.Count).ThenBy(s => s.Phrase).ToList();
        var unmarked = rows.Where(r => r.Phrase == null).Select(r => r.Hit).ToList();
        if (unmarked.Count > 0) slices.Add(new("Sem tradução marcada", unmarked, true));
        return slices;
    }
}

public sealed class WordStudyView : UserControl
{
    private readonly BibleLibrary library;
    private readonly SearchEngine search;
    private readonly TranslationLinks links;
    private readonly TabState state;
    private readonly Action changed;
    private readonly Action<List<SearchHit>, string, SearchMode, string, string, string, string?> showOccurrences;
    private readonly TextBox term = new() { MinWidth = 140, FontSize = 18, ToolTip = "Código Strong, lema ou forma original" };
    private readonly ComboBox source = new() { MinWidth = 140 }, translation = new() { MinWidth = 110 }, mode = new() { MinWidth = 130 };
    private readonly StackPanel output = new();
    private CancellationTokenSource? pending;
    private bool started;
    private static readonly string[] Palette = ["#2357C6", "#D83545", "#FF7B16", "#31A3A4", "#A074D3", "#57AE6C", "#E3AE32", "#D678AF"];

    public WordStudyView(BibleLibrary library, SearchEngine search, TranslationLinks links, TabState state, Action changed,
        Action<List<SearchHit>, string, SearchMode, string, string, string, string?> showOccurrences)
    {
        this.library = library; this.search = search; this.links = links; this.state = state; this.changed = changed; this.showOccurrences = showOccurrences;
        var content = new StackPanel { Margin = new(10) };
        Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        content.Children.Add(new TextBlock { Text = "Formas de tradução", FontFamily = new("Georgia"), FontSize = 18, Margin = new(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap });
        term.Text = state.StudyTerm;
        var entry = new Grid(); entry.ColumnDefinitions.Add(new()); entry.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); term.FontSize = 15; term.Height = 30; term.Padding = new(6, 3, 6, 3); entry.Children.Add(term); var run = MainWindow.Button("Estudar", () => _ = Study()); run.Height = 30; run.Padding = new(8, 3, 8, 3); run.Margin = new(4, 0, 0, 0); Grid.SetColumn(run, 1); entry.Children.Add(run); content.Children.Add(entry);
        var options = new WrapPanel { Margin = new(0, 7, 0, 8) };
        source.ItemsSource = library.Editions.Where(e => e.Id is "BHS-A-Strong" or "WLC-OSHB" or "SBLGNT").Select(e => e.Id).ToList(); source.SelectedItem = state.Edition;
        if (source.SelectedItem == null && source.Items.Count > 0) source.SelectedIndex = 0;
        source.ToolTip = "Texto original pesquisado";
        translation.ItemsSource = library.Editions.Where(e => e.Id is not ("BHS-A-Strong" or "WLC-OSHB" or "SBLGNT")).Select(e => e.Id).ToList(); translation.SelectedItem = state.StudyTranslation;
        if (translation.SelectedItem == null && translation.Items.Count > 0) translation.SelectedIndex = 0;
        translation.ToolTip = "Tradução usada nas marcações";
        mode.ItemsSource = new[] { "Código Strong", "Lema da fonte", "Forma sem sinais" }; mode.SelectedIndex = state.StudyMode == SearchMode.Lemma ? 1 : state.StudyMode == SearchMode.Intelligent ? 2 : 0;
        mode.ToolTip = "Tipo de pesquisa do estudo";
        options.Children.Add(source); options.Children.Add(translation); options.Children.Add(mode);
        var settings = new Expander { Header = "Fontes e modo", Content = options, IsExpanded = state.StudyOptionsExpanded, Margin = new(0, 4, 0, 0) }; settings.Expanded += (_, _) => { state.StudyOptionsExpanded = true; changed(); }; settings.Collapsed += (_, _) => { state.StudyOptionsExpanded = false; changed(); }; content.Children.Add(settings);
        term.ToolTip = "Strong, lema ou forma. Ajuste as fontes e o modo na opção abaixo; clique nas fatias para consultar ocorrências.";
        output.Margin = new(0, 8, 0, 0); content.Children.Add(output);
        term.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; _ = Study(); } };
        Loaded += (_, _) => { if (started && pending?.IsCancellationRequested != true) return; started = true; if (!string.IsNullOrWhiteSpace(term.Text)) _ = Study(); else Empty(); };
        Unloaded += (_, _) => pending?.Cancel();
    }
    private void Empty()
    {
        output.Children.Clear(); output.Children.Add(Donut([], 0, _ => { }));
        output.Children.Add(MainWindow.Label("Escolha uma palavra para começar. As contagens de tradução usam suas marcações; nenhuma equivalência é presumida.", 13, MainWindow.Muted));
    }
    private async Task Study()
    {
        string text = term.Text.Trim(), original = source.SelectedItem as string ?? "", translated = translation.SelectedItem as string ?? "";
        if (text.Length == 0) { Empty(); term.Focus(); return; }
        if (original.Length == 0 || translated.Length == 0) { output.Children.Clear(); output.Children.Add(MainWindow.Label("Selecione um texto original e uma tradução disponíveis.")); return; }
        pending?.Cancel(); var cts = pending = new CancellationTokenSource();
        var selectedMode = mode.SelectedIndex == 1 ? SearchMode.Lemma : mode.SelectedIndex == 2 ? SearchMode.Intelligent : SearchMode.Strong;
        state.StudyTerm = text; state.StudyTranslation = translated; state.Edition = original; state.StudyMode = selectedMode; changed();
        output.Children.Clear(); output.Children.Add(MainWindow.Label("Preparando o estudo…", 14));
        try
        {
            var found = await search.SearchAsync(text, [original], selectedMode, cts.Token);
            var target = await library.LoadAsync(translated).WaitAsync(cts.Token);
            var originalEdition = await library.LoadAsync(original).WaitAsync(cts.Token);
            var parsing = await library.Parsing.WaitAsync(cts.Token);
            var codes = GlossParallels.MatchedCodes(found.Hits);
            if (selectedMode == SearchMode.Strong && codes.Count == 0) codes = OriginalResource.ParseStrong(text).Select(OriginalResource.LexiconKey).ToList();
            var glosses = await System.Threading.Tasks.Task.Run(() => parsing.Glosses(codes), cts.Token);
            var parallelGlosses = await System.Threading.Tasks.Task.Run(() => GlossParallels.Compare(found.Hits, originalEdition, target, glosses), cts.Token);
            if (cts.IsCancellationRequested) return;
            var slices = WordStudyData.Group(found.Hits, TranslationLinks.Key(text, selectedMode), target, links);
            int total = slices.Sum(s => s.Hits.Count), marked = slices.Where(s => !s.Unmarked).Sum(s => s.Hits.Count);
            output.Children.Clear();
            output.Children.Add(MainWindow.Label(text + " · " + original + " → " + translated, 18, MainWindow.Green));
            output.Children.Add(MainWindow.Label($"{total:N0} versículos na fonte · {marked:N0} com tradução marcada", 12, MainWindow.Muted));
            output.Children.Add(new TextBlock { Text = "Glossas possíveis · português", FontSize = 18, Foreground = MainWindow.Green, Margin = new(0, 18, 0, 7), TextWrapping = TextWrapping.Wrap });
            output.Children.Add(MainWindow.Label("Do seu arquivo de parsing, com tradução automática provisória. São sentidos para estudo, não traduções confirmadas da " + translated + ".", 12, MainWindow.Muted));
            if (glosses.Count == 0)
            {
                output.Children.Add(MainWindow.Label("Não há glossa portuguesa disponível para os códigos encontrados. Consulte a definição original abaixo.", 12, MainWindow.Muted));
            }
            else
            {
                foreach (var candidate in parallelGlosses.Items)
                {
                    var line = new DockPanel { Margin = new(0, 4, 0, 4) };
                    var evidence = MainWindow.Button($"{candidate.Hits.Count:N0} paralelos com a expressão", () =>
                        showOccurrences(candidate.Hits, text, selectedMode, original, translated, "Expressão no paralelo: " + candidate.Gloss.Text + " · coocorrência, sem alinhamento confirmado", candidate.Gloss.Text));
                    evidence.IsEnabled = candidate.Hits.Count > 0; evidence.ToolTip = "A expressão aparece no mesmo versículo português. Isso não comprova qual palavra hebraica ela traduz. As categorias podem se sobrepor.";
                    DockPanel.SetDock(evidence, Dock.Right); line.Children.Add(evidence);
                    var meaning = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; meaning.Children.Add(MainWindow.Label(candidate.Gloss.Text, 16));
                    meaning.Children.Add(MainWindow.Label(string.Join(" / ", candidate.Gloss.Codes) + " · inglês da fonte: " + candidate.Gloss.English, 10, MainWindow.Muted));
                    line.Children.Add(meaning); output.Children.Add(line);
                }
                output.Children.Add(MainWindow.Label($"Consulta literal na {translated}: {parallelGlosses.Compared:N0} paralelos comparáveis; {parallelGlosses.Missing:N0} sem correspondência. As contagens podem se sobrepor e não somam traduções do lema. Use a tabela para conferir o contexto e confirmar vínculos.", 11, MainWindow.Muted));
            }
            var definitions = codes.Where(code => originalEdition.Resource?.Lexicon.ContainsKey(code) == true).Take(4).Select(code => originalEdition.Resource!.Lexicon[code]).ToList();
            if (definitions.Count > 0)
            {
                var lexicon = new StackPanel();
                foreach (var entry in definitions) { lexicon.Children.Add(MainWindow.Label(entry.Code + " · " + entry.Lemma, 14, MainWindow.Green)); lexicon.Children.Add(MainWindow.Label(entry.Definition, 12)); }
                output.Children.Add(new Expander { Header = "Definição original do léxico · inglês", Content = lexicon, IsExpanded = glosses.Count == 0, Margin = new(0, 10, 0, 10) });
            }
            output.Children.Add(new TextBlock { Text = "Traduções confirmadas · " + translated, FontSize = 18, Foreground = MainWindow.Green, Margin = new(0, 18, 0, 4) });
            if (marked == 0) output.Children.Add(MainWindow.Label("Ainda não há vínculos confirmados nesta pesquisa. Por isso o gráfico é cinza; as glossas acima aparecem independentemente de marcações.", 12, MainWindow.Muted));
            void Open(WordStudySlice slice) => showOccurrences(slice.Hits, text, selectedMode, original, translated, slice.Phrase, null);
            var chart = new Grid { Margin = new(0, 16, 0, 12) }; chart.ColumnDefinitions.Add(new() { Width = new(174) }); chart.ColumnDefinitions.Add(new());
            chart.Children.Add(Donut(slices, total, Open));
            var legend = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new(12, 0, 0, 0) };
            for (int i = 0; i < slices.Count; i++)
            {
                var slice = slices[i]; var row = new DockPanel();
                var dot = new Ellipse { Width = 12, Height = 12, Fill = MainWindow.Brush(slice.Unmarked ? "#CAD1CA" : Palette[i % Palette.Length]), Margin = new(2, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(dot, Dock.Left); row.Children.Add(dot);
                var count = MainWindow.Label(slice.Hits.Count.ToString("N0"), 13, MainWindow.Muted); count.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(count, Dock.Right); row.Children.Add(count);
                var button = MainWindow.Button(slice.Phrase, () => Open(slice)); button.Background = Brushes.Transparent; button.Padding = new(2, 5, 2, 5); button.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(button); legend.Children.Add(row);
            }
            Grid.SetColumn(legend, 1); chart.Children.Add(legend); output.Children.Add(chart);
            output.Children.Add(MainWindow.Label("COBERTURA PARCIAL", 10, MainWindow.Green));
            output.Children.Add(MainWindow.Label("As cores representam traduções marcadas manualmente. Cinza indica versículos ainda sem vínculo; não é uma tradução adicional. Não temos alinhamento automático para esta Bíblia.", 12, MainWindow.Muted));
            if (found.Warnings.Count > 0) output.Children.Add(MainWindow.Label(string.Join("\n", found.Warnings), 12, MainWindow.Muted));
            if (total == 0) output.Children.Add(MainWindow.Label("Nenhum versículo encontrado. Confira o tipo de pesquisa e os dados disponíveis na fonte.", 13));
            output.Children.Add(MainWindow.Button("Ver todas as ocorrências", () => showOccurrences(found.Hits, text, selectedMode, original, translated, "Todas as ocorrências", null)));
            output.Children.Add(MainWindow.Label("DISTRIBUIÇÃO POR LIVRO", 10, MainWindow.Green));
            var distribution = new Grid { Height = 54, Margin = new(0, 10, 0, 5) };
            var counts = found.Hits.GroupBy(h => h.Verse.Book).ToDictionary(g => g.Key, g => g.Count()); int maximum = Math.Max(1, counts.Values.DefaultIfEmpty(0).Max());
            for (int book = 0; book < 66; book++)
            {
                distribution.ColumnDefinitions.Add(new()); int count = counts.GetValueOrDefault(book); int currentBook = book;
                var bar = new Border { Background = count == 0 ? MainWindow.Line : MainWindow.Green, Height = count == 0 ? 2 : Math.Max(4, 48.0 * count / maximum), Margin = new(1, 0, 1, 0), VerticalAlignment = VerticalAlignment.Bottom, Cursor = count > 0 ? Cursors.Hand : Cursors.Arrow, ToolTip = BibleLibrary.CanonicalNames[book] + $" · {count} versículos" };
                bar.MouseLeftButtonUp += (_, _) => { if (count > 0) showOccurrences(found.Hits.Where(h => h.Verse.Book == currentBook).ToList(), text, selectedMode, original, translated, BibleLibrary.CanonicalNames[currentBook], null); };
                Grid.SetColumn(bar, book); distribution.Children.Add(bar);
            }
            output.Children.Add(distribution); output.Children.Add(MainWindow.Label("Passe o mouse nas barras para identificar o livro; clique para filtrar.", 11, MainWindow.Muted));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!cts.IsCancellationRequested) { output.Children.Clear(); output.Children.Add(MainWindow.Label("Não foi possível preparar o estudo: " + ex.Message)); } }
    }
    private static FrameworkElement Donut(List<WordStudySlice> slices, int total, Action<WordStudySlice> select)
    {
        var grid = new Grid { Width = 164, Height = 164, HorizontalAlignment = HorizontalAlignment.Left }; var canvas = new Canvas { Width = 164, Height = 164 }; grid.Children.Add(canvas);
        const double radius = 62, center = 82; double angle = -90;
        if (total == 0) { var empty = new Ellipse { Width = radius * 2, Height = radius * 2, Stroke = MainWindow.Line, StrokeThickness = 18 }; Canvas.SetLeft(empty, center - radius); Canvas.SetTop(empty, center - radius); canvas.Children.Add(empty); }
        for (int i = 0; i < slices.Count; i++)
        {
            var slice = slices[i]; double sweep = 360.0 * slice.Hits.Count / total; var color = MainWindow.Brush(slice.Unmarked ? "#CAD1CA" : Palette[i % Palette.Length]);
            Point At(double degrees) => new(center + radius * Math.Cos(degrees * Math.PI / 180), center + radius * Math.Sin(degrees * Math.PI / 180));
            Shape segment;
            if (sweep >= 359.999)
            {
                segment = new Ellipse { Width = radius * 2, Height = radius * 2 }; Canvas.SetLeft(segment, center - radius); Canvas.SetTop(segment, center - radius);
            }
            else
            {
                double visible = Math.Max(sweep * .7, sweep - 1.2); var figure = new PathFigure { StartPoint = At(angle), IsClosed = false };
                figure.Segments.Add(new ArcSegment(At(angle + visible), new Size(radius, radius), 0, visible > 180, SweepDirection.Clockwise, true));
                segment = new Path { Data = new PathGeometry([figure]) };
            }
            segment.Stroke = color; segment.StrokeThickness = 18; segment.Cursor = Cursors.Hand; segment.ToolTip = slice.Phrase + $" · {slice.Hits.Count} versículos";
            segment.MouseLeftButtonUp += (_, _) => select(slice); canvas.Children.Add(segment); angle += sweep;
        }
        var label = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        label.Children.Add(new TextBlock { Text = total.ToString("N0"), FontSize = 28, FontFamily = new("Georgia"), HorizontalAlignment = HorizontalAlignment.Center });
        label.Children.Add(new TextBlock { Text = "versículos", FontSize = 10, Foreground = MainWindow.Muted, HorizontalAlignment = HorizontalAlignment.Center }); grid.Children.Add(label); return grid;
    }
}
