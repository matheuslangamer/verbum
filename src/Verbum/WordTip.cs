using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Verbum;

/// <summary>Hover card for original words and for translation words linked to them.</summary>
public static class WordTip
{
    public sealed record SegmentLine(string Hebrew, string Description, bool Active);

    /// <summary>One line per prefix, stem and suffix of the graphic word. A BHS segment shows the
    /// whole OSHB word it belongs to, with its own part marked as active.</summary>
    public static List<SegmentLine> Segments(OriginalWord word)
    {
        if (word.Morphology.Length == 0) return [];
        var host = word.Whole is { Morphology.Length: > 0 } whole ? whole : word;
        string raw = host.Morphology; bool aramaic = raw[0] == 'A';
        var parts = (raw[0] is 'H' or 'A' ? raw[1..] : raw).Split('/');
        var pieces = host.Segments.Count > 0 ? host.Segments.Select(s => s.Text).ToList() : [host.Text];
        if (parts.Length != pieces.Count)
        {
            raw = word.Morphology; aramaic = raw[0] == 'A';
            return [new(word.Text, string.Join(" + ", (raw[0] is 'H' or 'A' ? raw[1..] : raw).Split('/').Select(p => OshbPortuguese.DescribeSegment(p, aramaic))), true)];
        }
        return parts.Select((part, i) => new SegmentLine(pieces[i], OshbPortuguese.DescribeSegment(part, aramaic),
            ReferenceEquals(host, word) || i >= word.WholeSegment && i < word.WholeSegment + word.WholeSegmentCount)).ToList();
    }

    private static TextBlock Text(string text, double size = 13, Brush? color = null, bool hebrew = false, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = color ?? MainWindow.Brush("#24332F"), FontWeight = weight ?? FontWeights.Normal };
        if (hebrew) block.FontFamily = MainWindow.HebrewReadingFont;
        return block;
    }
    private static ToolTip Card(StackPanel panel) => new() { Content = panel, ContentTemplate = null, Padding = new(11, 8, 11, 8), MaxWidth = 620 };

    private static void AddWord(StackPanel panel, string text, string lemma, IEnumerable<string> strongs, List<SegmentLine> lines)
    {
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Text(text, 25, null, true));
        string codes = string.Join(" + ", strongs);
        if (lemma.Length > 0) { head.Children.Add(Text("   lema ", 11, MainWindow.Muted)); head.Children.Add(Text(lemma, 18, null, true)); }
        if (codes.Length > 0) head.Children.Add(Text("   " + codes, 11, MainWindow.Green));
        foreach (FrameworkElement child in head.Children) child.VerticalAlignment = VerticalAlignment.Bottom;
        panel.Children.Add(head);
        if (lines.Count == 1) panel.Children.Add(Text(lines[0].Description, 13.5, null, false, FontWeights.SemiBold));
        else if (lines.Count > 1)
        {
            var grid = new Grid { Margin = new(0, 4, 0, 0) }; grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new());
            for (int i = 0; i < lines.Count; i++)
            {
                grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                var piece = Text(lines[i].Hebrew, 18, lines[i].Active ? null : MainWindow.Muted, true); piece.Margin = new(0, 0, 12, 0); piece.HorizontalAlignment = HorizontalAlignment.Right;
                var label = Text(lines[i].Description, 13, lines[i].Active ? null : MainWindow.Muted, false, lines[i].Active ? FontWeights.SemiBold : FontWeights.Normal); label.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetRow(piece, i); Grid.SetRow(label, i); Grid.SetColumn(label, 1); grid.Children.Add(piece); grid.Children.Add(label);
            }
            panel.Children.Add(grid);
        }
    }

    /// <summary>Card for a Hebrew/Greek word: whole word, segments, gloss and aligned translation.</summary>
    public static ToolTip ForOriginal(BibleLibrary library, BibleEdition? edition, VerseEntry verse, OriginalWord word)
    {
        var panel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };
        var lines = Segments(word);
        var host = lines.Count > 1 && word.Whole != null ? word.Whole : word;
        AddWord(panel, host.Text, word.Lemma, word.Strongs, lines);
        var parsing = Text(word.Morphology.Length > 0 ? "" : "parsing não fornecido por esta fonte", 13, MainWindow.Muted);
        if (word.Morphology.Length == 0) panel.Children.Add(parsing);
        string firstGloss = WordGlosses.Summary([], word, edition?.Resource, []);
        var gloss = Text(firstGloss, 12.5); gloss.Margin = new(0, 5, 0, 0); gloss.FontStyle = FontStyles.Italic; gloss.Visibility = firstGloss.Length > 0 ? Visibility.Visible : Visibility.Collapsed; panel.Children.Add(gloss);
        var translation = Text("", 13.5, MainWindow.Green, false, FontWeights.SemiBold); translation.Margin = new(0, 5, 0, 0); translation.Visibility = Visibility.Collapsed; panel.Children.Add(translation);
        var footer = Text((word.MorphologySource.Length > 0 || edition?.Info.Id == "WLC-OSHB" && word.Morphology.Length > 0 ? "OSHB · " : "") + "clique para a análise completa", 10.5, MainWindow.Muted); footer.Margin = new(0, 5, 0, 0); panel.Children.Add(footer);
        var tip = Card(panel); bool filled = false;
        tip.Opened += async (_, _) =>
        {
            if (filled) return; filled = true;
            try
            {
                if (SearchText.HasHebrew(word.Text))
                {
                    var catalog = await library.Parsing; var matches = catalog.Find(verse, word);
                    if (word.Morphology.Length == 0)
                    {
                        var analyses = matches.Select(m => m.Parsing).Distinct().ToList();
                        parsing.Text = analyses.Count == 1 ? analyses[0] + " (sugestão do arquivo pessoal)" : analyses.Count > 1 ? "análises possíveis · clique para consultar" : parsing.Text;
                    }
                    string text = WordGlosses.Summary(matches, word, edition?.Resource, []);
                    // Extended codes (H9000…) mark prefixes; their lexicon articles are about the letter, not a gloss.
                    bool prefix = word.Strongs.Count > 0 && word.Strongs.All(s => s.StartsWith("H9", StringComparison.Ordinal) && s.Length >= 5);
                    if (text.Length == 0 && !prefix) text = WordGlosses.Summary(matches, word, edition?.Resource, await library.Lexicons);
                    if (text.Length > 0) { gloss.Text = text; gloss.Visibility = Visibility.Visible; }
                }
                if (library.AlignedTranslation() is { } id && await library.Alignment(id) is { } alignment)
                {
                    var found = alignment.Translations(verse, word, await library.LoadAsync(id));
                    if (found.Count > 0)
                    {
                        translation.Text = id + " · " + string.Join(" · ", found.Select(f => "«" + f.Text + "»")) + (found.Min(f => f.Confidence) < 3 ? "  (" + TranslationAlignment.ConfidenceLabel(found.Min(f => f.Confidence)) + ")" : "");
                        translation.Visibility = Visibility.Visible;
                        footer.Text += " · tradução por alinhamento automático";
                    }
                }
            }
            catch { }
        };
        return tip;
    }

    /// <summary>Card for a translation word: the original term(s) it is aligned with.</summary>
    public static ToolTip ForTranslation(BibleLibrary library, TranslationAlignment alignment, string shown, List<AlignedTerm> terms)
    {
        var panel = new StackPanel { FlowDirection = FlowDirection.LeftToRight };
        var codes = new List<string>(); var glosses = new List<TextBlock>();
        foreach (var term in terms.Take(3))
        {
            if (panel.Children.Count > 0) panel.Children.Add(new Border { Height = 1, Background = MainWindow.Line, Margin = new(0, 7, 0, 6) });
            var word = term.Word.ToOriginal();
            if (term.Segment >= 0) { word.Whole = term.Word.ToOriginal(); word.WholeSegment = term.Segment; word.WholeSegmentCount = 1; }
            // Lemma and Strong belong to the stem; a prefix or suffix shows only its own function.
            bool stem = term.Segment < 0 || StemSegment(term.Word) == term.Segment;
            AddWord(panel, term.Word.Text, stem ? term.Word.L : "", stem ? word.Strongs : [], Segments(word));
            var gloss = Text("", 12.5); gloss.FontStyle = FontStyles.Italic; gloss.Margin = new(0, 4, 0, 0); gloss.Visibility = Visibility.Collapsed; panel.Children.Add(gloss);
            glosses.Add(gloss); codes.Add(stem ? term.Word.S : "");
            panel.Children.Add(Text(BibleLibrary.CanonicalNames[term.Verse.B] + " " + term.Verse.C + ":" + term.Verse.V + " · " + alignment.File.Source + " · " + TranslationAlignment.ConfidenceLabel(term.Confidence), 10.5, MainWindow.Muted));
        }
        var footer = Text("«" + shown + "» · alinhamento automático, não revisado · botão direito: analisar o termo original", 10.5, MainWindow.Muted); footer.Margin = new(0, 6, 0, 0); panel.Children.Add(footer);
        var tip = Card(panel); bool filled = false;
        tip.Opened += async (_, _) =>
        {
            if (filled) return; filled = true;
            try
            {
                var catalog = await library.Parsing;
                for (int i = 0; i < glosses.Count; i++)
                {
                    var found = catalog.Glosses(codes[i].Split(' ', StringSplitOptions.RemoveEmptyEntries)).Take(3).Select(g => g.Text).ToList();
                    if (codes[i].Length == 0 || found.Count == 0) continue;
                    glosses[i].Text = string.Join(" / ", found) + " (glossas PT provisórias do lema)"; glosses[i].Visibility = Visibility.Visible;
                }
            }
            catch { }
        };
        return tip;
    }

    internal static int StemSegment(AlignedWord word)
    {
        var parts = (word.M.Length > 0 && word.M[0] is 'H' or 'A' ? word.M[1..] : word.M).Split('/');
        if (parts.Length != word.Segments.Length) return -1;
        int main = parts.Length - 1; while (main > 0 && parts[main].StartsWith('S')) main--;
        return main;
    }

    /// <summary>All text of a card, in reading order (used by the self-tests).</summary>
    public static string PlainText(object? content)
    {
        if (content is ToolTip tip) return PlainText(tip.Content);
        if (content is TextBlock block) return block.Visibility == Visibility.Visible ? block.Text : "";
        if (content is Panel panel) return string.Join("; ", panel.Children.Cast<object>().Select(PlainText).Where(t => t.Length > 0));
        return content as string ?? "";
    }
}
