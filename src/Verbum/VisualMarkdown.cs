using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Verbum;

internal static class VisualMarkdown
{
    private sealed record Source(string Original, string Canonical);
    private sealed record DocumentSource(string Original, string Canonical, string Tail);
    internal static FlowDocument Load(string markdown, Action<string>? open)
    {
        var document = new FlowDocument { FontFamily = new("Georgia"), FontSize = 17, Foreground = Theme.Get("#2D3730"), Background = Theme.Surface, PagePadding = new(15), ColumnWidth = double.PositiveInfinity };
        var lines = Regex.Matches(markdown, @"[^\n]*\n|[^\n]+$").Select(m => m.Value).ToList(); string prefix = "";
        for (int i = 0; i < lines.Count; i++)
        {
            string raw = lines[i], line = raw.TrimEnd('\r', '\n');
            if (string.IsNullOrWhiteSpace(line)) { prefix += raw; continue; }
            Block block;
            if (line.TrimStart().StartsWith("```"))
            {
                var code = new StringBuilder();
                while (++i < lines.Count) { raw += lines[i]; if (lines[i].TrimStart().StartsWith("```")) break; code.Append(lines[i]); }
                block = new Paragraph(new Run(code.ToString().TrimEnd('\r', '\n'))) { FontFamily = new("Consolas"), FontSize = 14, Background = MainWindow.Paper, Margin = new(0, 0, 0, 10) };
            }
            else
            {
                var list = Regex.Match(line, @"^(\s*)([-*+] |\d+\. )(.*)$");
                var rendered = MarkdownNoteView.Render(list.Success ? list.Groups[3].Value : line, open);
                var p = (Paragraph?)rendered.Blocks.FirstBlock ?? new Paragraph(); rendered.Blocks.Remove(p);
                if (list.Success)
                {
                    var items = new List { MarkerStyle = char.IsDigit(list.Groups[2].Value[0]) ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc, Margin = new(22, 0, 0, 7), Padding = new(0) };
                    items.ListItems.Add(new ListItem(p)); block = items;
                }
                else block = p;
            }
            document.Blocks.Add(block); block.Tag = new Source(prefix + raw, ""); block.Tag = new Source(prefix + raw, SerializeBlock(block)); prefix = "";
        }
        if (document.Blocks.Count == 0) document.Blocks.Add(new Paragraph());
        document.Tag = new DocumentSource(markdown, Canonical(document), prefix); return document;
    }
    private static string Canonical(FlowDocument document) => string.Join("\n\n", document.Blocks.Select(SerializeBlock));
    internal static string Save(FlowDocument document)
    {
        var original = document.Tag as DocumentSource;
        if (original != null && Canonical(document) == original.Canonical) return original.Original;
        var result = new StringBuilder();
        foreach (Block block in document.Blocks)
        {
            string canonical = SerializeBlock(block), value;
            if (block.Tag is Source source && source.Canonical == canonical) value = source.Original;
            else value = (block.Tag is Source old ? Regex.Match(old.Original, @"^(?:[ \t]*\r?\n)*").Value : "") + canonical + "\n";
            if (result.Length > 0 && result[^1] != '\n') result.Append('\n'); result.Append(value);
        }
        result.Append(original?.Tail ?? ""); return result.ToString();
    }
    private static string SerializeBlock(Block block)
    {
        if (block is Paragraph p)
        {
            bool heading = p.FontWeight == FontWeights.Bold && p.FontSize >= 18;
            if (p.FontFamily.Source.Contains("Consolas"))
            {
                string language = p.Tag is Source old ? Regex.Match(old.Original, @"(?m)^[ \t]*```([^\r\n]*)").Groups[1].Value : "";
                return "```" + language + "\n" + new TextRange(p.ContentStart, p.ContentEnd).Text.TrimEnd('\r', '\n') + "\n```";
            }
            string value = SerializeInline(p.Inlines, heading, false);
            value = NoteColors.Serialize(p, value);
            if (!heading && p.BorderThickness.Left == 0) value = Regex.Replace(value, @"^(\s*)(#{1,6}|[>+\-]|\d+\.)(\s)", @"$1\$2$3");
            if (heading) value = new string('#', Math.Clamp((int)Math.Round((30 - p.FontSize) / 2), 1, 6)) + " " + value;
            if (p.BorderThickness.Left > 0) value = NoteQuote.MarkdownPrefix(p) + value;
            return value;
        }
        if (block is List list)
        {
            int index = list.StartIndex; return string.Join("\n", list.ListItems.Select(item => (list.MarkerStyle == TextMarkerStyle.Decimal ? (index++) + ". " : "- ") + string.Join("\n", item.Blocks.Select(SerializeBlock)).Replace("\n", "\n  ")));
        }
        return new TextRange(block.ContentStart, block.ContentEnd).Text.TrimEnd('\r', '\n');
    }
    private static string Escape(string value) => Regex.Replace(value, @"([\\*_[\]`])", @"\$1");
    private static string SerializeInline(InlineCollection inlines, bool parentBold, bool parentItalic)
    {
        var text = new StringBuilder();
        foreach (Inline inline in inlines)
        {
            bool bold = inline.FontWeight == FontWeights.Bold, italic = inline.FontStyle == FontStyles.Italic;
            string value = inline switch
            {
                Run run => Escape(run.Text), LineBreak => "\n",
                Hyperlink link => "[" + SerializeInline(link.Inlines, bold, italic) + "](" + (link.Tag as string ?? link.NavigateUri?.ToString() ?? "") + ")",
                Span span => SerializeInline(span.Inlines, bold || parentBold, italic || parentItalic),
                _ => new TextRange(inline.ContentStart, inline.ContentEnd).Text
            };
            if (inline is Run code && code.FontFamily.Source.Contains("Consolas")) value = "`" + code.Text + "`";
            if (bold && !parentBold) value = "**" + value + "**";
            if (italic && !parentItalic) value = "*" + value + "*";
            if (inline.TextDecorations == TextDecorations.Underline && inline is not Hyperlink) value = "<u>" + value + "</u>";
            value = NoteColors.Serialize(inline, value);
            text.Append(value);
        }
        return text.ToString();
    }
}
