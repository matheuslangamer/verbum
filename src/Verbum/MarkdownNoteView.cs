using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Verbum;

public sealed class MarkdownNoteView : UserControl
{
    internal Action<string> AppendMarkdown { get; private set; } = _ => { };
    public MarkdownNoteView(string markdown, Action<string> changed, Func<string, Task<NoteLink?>>? createLink = null, Action<string>? openLink = null, TabState? state = null)
    {
        var root = new DockPanel(); Content = root;
        var tools = new WrapPanel { Margin = new(0, 0, 0, 6) }; DockPanel.SetDock(tools, Dock.Top); root.Children.Add(tools);
        var text = new TextBox { Text = markdown, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new("Consolas"), FontSize = 15, Background = MainWindow.Paper, Padding = new(12), BorderThickness = new(0), IsUndoEnabled = true, VerticalContentAlignment = VerticalAlignment.Top };
        var preview = new FlowDocumentScrollViewer { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Surface };
        var visual = new RichTextBox { Background = Theme.Surface, Foreground = Theme.Get("#2D3730"), BorderThickness = new(0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsDocumentEnabled = true, AcceptsTab = true };
        bool syncing = false, visualLoaded = false;
        var body = new Grid(); body.ColumnDefinitions.Add(new()); body.ColumnDefinitions.Add(new() { Width = new(5) }); body.ColumnDefinitions.Add(new());
        body.Children.Add(text); Grid.SetColumn(preview, 2); body.Children.Add(preview);
        var splitter = new GridSplitter { Width = 5, HorizontalAlignment = HorizontalAlignment.Stretch, Background = MainWindow.Line }; Grid.SetColumn(splitter, 1); body.Children.Add(splitter); Grid.SetColumnSpan(visual, 3); body.Children.Add(visual);
        var summary = MainWindow.Label("", 10, MainWindow.Muted); summary.Margin = new(0, 4, 0, 0); DockPanel.SetDock(summary, Dock.Bottom); root.Children.Add(summary); root.Children.Add(body);
        var editGroups = new List<StackPanel>();
        Button? toggle = null, splitButton = null;
        string mode = state?.NoteEditorMode ?? "visual";
        void RenderPreview() => preview.Document = Render(text.Text, openLink);
        void Arrange()
        {
            bool split = mode == "split" && ActualWidth >= 620, showText = mode != "preview" && mode != "visual";
            visual.Visibility = mode == "visual" ? Visibility.Visible : Visibility.Collapsed;
            text.Visibility = showText ? Visibility.Visible : Visibility.Collapsed; preview.Visibility = mode == "preview" || split ? Visibility.Visible : Visibility.Collapsed; splitter.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
            body.ColumnDefinitions[0].Width = showText || mode == "visual" ? new(1, GridUnitType.Star) : new(0); body.ColumnDefinitions[1].Width = new(split ? 5 : 0); body.ColumnDefinitions[2].Width = preview.Visibility == Visibility.Visible ? new(1, GridUnitType.Star) : new(0);
            if (toggle != null) toggle.Content = mode == "visual" ? "MD <>" : "Normal";
            if (splitButton != null) splitButton.Background = mode == "split" ? Theme.Get("#CEE1D2") : Theme.Surface;
            foreach (var controls in editGroups) controls.IsEnabled = mode != "preview";
            summary.Text = $"{text.Text.Length:N0} caracteres · " + (mode == "visual" ? "Edição visual · Ctrl+clique nos links" : "Markdown · links clicáveis na prévia") + (mode == "split" && !split ? " · amplie o bloco para ver lado a lado" : "");
        }
        void SetMode(string value) { if (value == "visual" && !visualLoaded) { syncing = true; visual.Document = VisualMarkdown.Load(text.Text, openLink); syncing = false; visualLoaded = true; } mode = value; if (state != null) state.NoteEditorMode = mode; if (mode != "edit") RenderPreview(); Arrange(); }
        void Wrap(string prefix, string suffix)
        {
            if (mode == "visual") { visual.Focus(); (prefix == "**" ? EditingCommands.ToggleBold : EditingCommands.ToggleItalic).Execute(null, visual); return; }
            int start = text.SelectionStart; string selected = text.SelectedText; text.SelectedText = prefix + selected + suffix; text.Focus(); text.Select(start + prefix.Length, selected.Length);
        }
        TextRange? remembered = null;
        int mdStart = 0, mdLength = 0;
        void Remember()
        {
            remembered = mode == "visual" ? new TextRange(visual.Selection.Start, visual.Selection.End) : null;
            mdStart = text.SelectionStart; mdLength = text.SelectionLength;
        }
        void RestoreSelection()
        {
            if (mode == "visual") { visual.Focus(); if (remembered != null) visual.Selection.Select(remembered.Start, remembered.End); }
            else { text.Focus(); text.Select(mdStart, mdLength); }
        }
        void BlockStyle(string prefix, string? quoteColor = null)
        {
            if (mode != "visual")
            {
                int a = text.SelectionStart, b = a + text.SelectionLength;
                int first = a == 0 ? 0 : text.Text.LastIndexOf('\n', a - 1) + 1;
                int end = text.Text.IndexOf('\n', b > a && text.Text[b - 1] == '\n' ? b - 1 : b);
                if (end < 0) end = text.Text.Length;
                string transformed = Regex.Replace(text.Text[first..end], @"(?m)^(?:#{1,6} |>\s?(?:<!--quote:#[0-9A-Fa-f]{6}--> )?)?", _ => prefix == ">" ? NoteQuote.Prefix(quoteColor) : prefix);
                text.Select(first, end - first); text.SelectedText = transformed; text.Select(first, transformed.Length); text.Focus();
                return;
            }
            visual.BeginChange();
            try
            {
                var p = visual.Selection.Start.Paragraph ?? visual.CaretPosition.Paragraph;
                while (p != null)
                {
                    p.FontWeight = prefix.StartsWith('#') ? FontWeights.Bold : FontWeights.Normal;
                    p.FontSize = prefix.StartsWith('#') ? 30 - 2 * prefix.Trim().Length : 17;
                    p.BorderThickness = new(0); p.Padding = new(0); p.Margin = new(0, 0, 0, 10);
                    if (prefix == ">") NoteQuote.Apply(p, quoteColor);
                    var next = p.NextBlock as Paragraph;
                    if (next == null || next.ContentStart.CompareTo(visual.Selection.End) >= 0) break;
                    p = next;
                }
            }
            finally { visual.EndChange(); }
            visual.Focus(); PublishVisual();
        }
        void Prefix(string prefix)
        {
            if (mode == "visual")
            {
                visual.Focus();
                if (prefix == "- ") EditingCommands.ToggleBullets.Execute(null, visual);
                else if (prefix == "1. ") EditingCommands.ToggleNumbering.Execute(null, visual);
                else if (visual.CaretPosition.Paragraph is { } paragraph) { if (prefix.StartsWith("#")) { paragraph.FontSize = 30 - 2 * prefix.Trim().Length; paragraph.FontWeight = FontWeights.Bold; } else { paragraph.BorderThickness = new(2, 0, 0, 0); paragraph.BorderBrush = MainWindow.Green; paragraph.Padding = new(10, 0, 0, 0); } PublishVisual(); }
                return;
            }
            int start = text.SelectionStart == 0 ? 0 : text.Text.LastIndexOf('\n', text.SelectionStart - 1) + 1, end = text.SelectionStart + text.SelectionLength;
            string value = text.Text[start..end], insertion = prefix + value.Replace("\n", "\n" + prefix);
            text.Select(start, end - start); text.SelectedText = insertion; text.Select(start, insertion.Length); text.Focus();
        }
        async void Link()
        {
            if (createLink == null) return;
            bool inVisual = mode == "visual"; int start = text.SelectionStart, length = text.SelectionLength;
            var result = await createLink(inVisual ? visual.Selection.Text : text.SelectedText); if (result == null) return;
            if (inVisual)
            {
                visual.BeginChange(); visual.Selection.Text = "";
                var run = new Run(result.Label, visual.Selection.Start); var link = new Hyperlink(run.ContentStart, run.ContentEnd) { Tag = result.Target, Foreground = MainWindow.Green, ToolTip = result.Target };
                link.Click += (_, e) => { e.Handled = true; openLink?.Invoke(result.Target); }; visual.EndChange(); visual.Focus(); PublishVisual(); return;
            }
            text.Select(start, length); string insertion = NoteLinks.Markdown(result.Label, result.Target); text.SelectedText = insertion; text.Focus(); text.Select(start + insertion.Length, 0);
        }
        StackPanel Group(bool editing = true)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            tools.Children.Add(new Border { Child = panel, BorderThickness = new(1), CornerRadius = new(5), BorderBrush = MainWindow.Line, Padding = new(1), Margin = new(0, 0, 4, 4) });
            if (editing) editGroups.Add(panel);
            return panel;
        }
        var group = Group();
        Button Tool(string label, Action action, string tip)
        {
            var button = MainWindow.Button(label, action, tip); button.Tag = label; System.Windows.Automation.AutomationProperties.SetName(button, tip); button.Height = 28; button.Padding = new(8, 2, 8, 2); button.Margin = new(1); if (label == "B") button.FontWeight = FontWeights.Bold; if (label == "I") { button.FontFamily = new("Cambria"); button.FontSize = 18; button.FontWeight = FontWeights.Bold; button.FontStyle = FontStyles.Italic; }
            if (label is "Lista" or "1." or "Link…") button.Content = Icons.Create(label switch { "Lista" => "list-dots", "1." => "list-number", _ => "link" }, 17);
            button.Focusable = false; button.Background = Theme.Surface; group.Children.Add(button); return button;
        }
        Tool("B", () => Wrap("**", "**"), "Negrito · Ctrl+B"); Tool("I", () => Wrap("*", "*"), "Itálico · Ctrl+I"); var titles = new ContextMenu();
        foreach (int level in Enumerable.Range(1, 6))
        {
            var heading = new MenuItem { Header = "Título " + level };
            heading.Click += (_, _) => BlockStyle(new string('#', level) + " "); titles.Items.Add(heading);
        }
        Tool("Título ▾", () => titles.IsOpen = true, "Escolher nível do título");
        titles.PlacementTarget = (Button)group.Children[group.Children.Count - 1]; titles.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        group = Group(); Tool("Lista", () => Prefix("- "), "Lista com marcadores"); Tool("1.", () => Prefix("1. "), "Lista numerada"); group = Group();
        Tool("¶", () => BlockStyle(""), "Parágrafo normal");
        var quote = NoteColors.Palette("❝", TextMarkers.FontColors, c => { RestoreSelection(); BlockStyle(">", c); }, () => { RestoreSelection(); BlockStyle(">"); }, Remember);
        quote.ToolTip = "Citação · escolher a cor da borda"; group.Children.Add(quote);
        Tool("Link…", Link, "Vincular Bíblia, nota/comentário, livro ou página web · Ctrl+K");
        group = Group();
        Tool("↶", () => { if (mode == "visual") { if (visual.CanUndo) visual.Undo(); visual.Focus(); } else { if (text.CanUndo) text.Undo(); text.Focus(); } }, "Desfazer · Ctrl+Z"); Tool("↷", () => { if (mode == "visual") { if (visual.CanRedo) visual.Redo(); visual.Focus(); } else { if (text.CanRedo) text.Redo(); text.Focus(); } }, "Refazer · Ctrl+Y");
        group = Group();
        void ApplyColor(string value, bool background)
        {
            RestoreSelection();
            if (mode == "visual")
            {
                visual.Selection.ApplyPropertyValue(background ? TextElement.BackgroundProperty : TextElement.ForegroundProperty, Theme.Raw(value));
                PublishVisual();
            }
            else
            {
                int start = text.SelectionStart;
                var lines = text.SelectedText.Split('\n');
                string insertion = string.Join("\n", lines.Select(line => NoteColors.Wrap(line.TrimEnd('\r'), value, background)));
                text.SelectedText = insertion; text.Focus();
                if (lines.Length == 1 && lines[0].Length == 0) text.Select(start + insertion.IndexOf('>') + 1, 0);
                else text.Select(start, insertion.Length);
            }
        }
        group.Children.Add(NoteColors.Palette("Marcador", TextMarkers.Colors, c => ApplyColor(c, true), beforeOpen: Remember));
        group.Children.Add(NoteColors.Palette("Letra", TextMarkers.FontColors, c => ApplyColor(c, false), beforeOpen: Remember));
        group = Group(false);
        toggle = Tool("MD <>", () => SetMode(mode == "visual" ? "edit" : "visual"), "Alternar edição visual e Markdown");
        splitButton = Tool("Lado a lado", () => SetMode(mode == "split" ? "visual" : "split"), "Markdown e prévia lado a lado");
        var reading = new ContextMenu();
        var previewItem = new MenuItem { Header = "Prévia" }; previewItem.Click += (_, _) => SetMode("preview"); reading.Items.Add(previewItem);
        splitButton.ContextMenu = reading;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += (_, _) => { timer.Stop(); if (preview.Visibility == Visibility.Visible) RenderPreview(); };
        text.TextChanged += (_, _) => { if (syncing) return; visualLoaded = false; changed(text.Text); Arrange(); timer.Stop(); timer.Start(); };
        void PublishVisual()
        {
            if (syncing || mode != "visual") return;
            string value = VisualMarkdown.Save(visual.Document); if (value == text.Text) return;
            syncing = true; text.Text = value; syncing = false; changed(value); Arrange();
        }
        visual.TextChanged += (_, _) => PublishVisual();
        visual.PreviewKeyDown += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.K) { Link(); e.Handled = true; } };
        DataObject.AddPastingHandler(visual, (_, e) => { if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)) { e.DataObject = new DataObject(DataFormats.UnicodeText, e.SourceDataObject.GetData(DataFormats.UnicodeText)); e.FormatToApply = DataFormats.UnicodeText; } else e.CancelCommand(); });
        int initialCaret = Math.Clamp(state?.NoteCaret ?? 0, 0, text.Text.Length), initialLength = Math.Clamp(state?.NoteSelectionLength ?? 0, 0, text.Text.Length - initialCaret);
        text.SelectionChanged += (_, _) => { if (state != null) { state.NoteCaret = text.SelectionStart; state.NoteSelectionLength = text.SelectionLength; } };
        text.PreviewKeyDown += (_, e) => { if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return; if (e.Key == Key.B) { Wrap("**", "**"); e.Handled = true; } if (e.Key == Key.I) { Wrap("*", "*"); e.Handled = true; } if (e.Key == Key.K) { Link(); e.Handled = true; } };
        AppendMarkdown = addition =>
        {
            var next = text.Text.TrimEnd('\r', '\n') + (text.Text.Length == 0 ? "" : "\n\n") + addition;
            text.Text = next;
            if (mode == "visual") SetMode("visual"); else RenderPreview();
        };
        Loaded += (_, _) => { text.Select(initialCaret, initialLength); SetMode(mode); }; SizeChanged += (_, _) => Arrange(); Unloaded += (_, _) => timer.Stop(); Arrange();
    }
    public static FlowDocument Render(string markdown, Action<string>? openLink = null)
    {
        var flow = new FlowDocument { Background = Theme.Surface, Foreground = Theme.Get("#2D3730"), FontFamily = new("Georgia"), FontSize = 17, PagePadding = new(15), ColumnWidth = double.PositiveInfinity }; bool code = false;
        foreach (string line in markdown.Replace("\r", "").Split('\n'))
        {
            if (!code && string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith("```")) { code = !code; continue; }
            var paragraph = new Paragraph { Margin = new(0, 0, 0, 10) };
            if (code) { paragraph.FontFamily = new("Consolas"); paragraph.FontSize = 14; paragraph.Background = MainWindow.Paper; paragraph.Inlines.Add(new Run(line)); flow.Blocks.Add(paragraph); continue; }
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.*)$"); string value = heading.Success ? heading.Groups[2].Value : line;
            if (heading.Success) { paragraph.FontSize = 30 - heading.Groups[1].Length * 2; paragraph.FontWeight = FontWeights.Bold; }
            if (value.StartsWith("> ")) { value = NoteQuote.Unwrap(value, out var color); NoteQuote.Apply(paragraph, color); }
            if (value.StartsWith("- ") || value.StartsWith("* ")) value = "• " + value[2..];
            AddInline(paragraph.Inlines, value, openLink); flow.Blocks.Add(paragraph);
        }
        return flow;
    }
    private static void AddInline(InlineCollection target, string text, Action<string>? openLink, int depth = 0)
    {
        if (depth > 10) { target.Add(new Run(text)); return; }
        const string pattern = NoteColors.Pattern + @"|<u>(?<underline>.*?)</u>|\\(?<escape>.)|`(?<code>[^`]+)`|\[(?<label>(?:\\.|[^\]\\])*)\]\((?<url>[^)\s]+)\)|\*\*(?<bold>.+?)\*\*(?!\*)|__(?<bold>.+?)__|\*(?<italic>.+?)\*|_(?<italic>.+?)_";
        int offset = 0;
        foreach (Match match in Regex.Matches(text, pattern))
        {
            if (match.Index > offset) target.Add(new Run(text[offset..match.Index]));
            if (match.Groups["highlight"].Success || match.Groups["defaultMark"].Success || match.Groups["ink"].Success)
            {
                bool ink = match.Groups["ink"].Success;
                var colored = new Span();
                if (ink) colored.Foreground = Theme.Raw(match.Groups["fg"].Value);
                else colored.Background = Theme.Raw(match.Groups["bg"].Success ? match.Groups["bg"].Value : TextMarkers.Colors["Amarelo"]);
                AddInline(colored.Inlines, match.Groups[ink ? "ink" : match.Groups["highlight"].Success ? "highlight" : "defaultMark"].Value, openLink, depth + 1);
                target.Add(colored);
            }
            else if (match.Groups["underline"].Success) { var underline = new Underline(); AddInline(underline.Inlines, match.Groups["underline"].Value, openLink, depth + 1); target.Add(underline); }
            else if (match.Groups["escape"].Success) target.Add(new Run(match.Groups["escape"].Value));
            else if (match.Groups["code"].Success) target.Add(new Run(match.Groups["code"].Value) { FontFamily = new("Consolas"), Background = MainWindow.Paper });
            else if (match.Groups["url"].Success)
            {
                string url = match.Groups["url"].Value;
                if (NoteLinks.TryParse(url, out _)) { var link = new Hyperlink { Foreground = MainWindow.Green, ToolTip = url, Tag = url }; AddInline(link.Inlines, match.Groups["label"].Value, null, depth + 1); link.Click += (_, e) => { e.Handled = true; openLink?.Invoke(url); }; target.Add(link); }
                else target.Add(new Run(match.Value));
            }
            else { bool bold = match.Groups["bold"].Success; Span span = bold ? new Bold() : new Italic(); AddInline(span.Inlines, match.Groups[bold ? "bold" : "italic"].Value, openLink, depth + 1); target.Add(span); }
            offset = match.Index + match.Length;
        }
        if (offset < text.Length) target.Add(new Run(text[offset..]));
    }
}
