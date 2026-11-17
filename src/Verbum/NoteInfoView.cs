using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Verbum;

internal sealed class NoteInfoView : UserControl
{
    private readonly StackPanel body = new();
    internal NoteInfoView(TabState note, Action touch, Action<string> append, Func<bool,Task<NoteLink?>> choose,
        Action<string> open, Action delete, Action close)
    {
        Content = new Border { Background = Theme.Surface, BorderBrush = MainWindow.Line, BorderThickness = new(1), Padding = new(12),
            Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        void Section(string title) => body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new(0, 12, 0, 5) });
        void ActionButton(string label, Action action) { var b = MainWindow.Button(label, action); b.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(b); }
        string Date(DateTimeOffset? time) => time?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "sem registro";
        async void AddLink(bool bible)
        {
            close(); var link = await choose(bible); if (link != null) append(NoteLinks.Markdown(link.Label, link.Target));
        }
        void Draw()
        {
            body.Children.Clear(); Section("Esta nota");
            body.Children.Add(MainWindow.Label("Criada · " + Date(note.NoteCreated), 12));
            body.Children.Add(MainWindow.Label("Editada · " + Date(note.NoteEdited), 12));
            body.Children.Add(MainWindow.Label("Tipo · " + (note.WordAnchor != null ? "Anotação de palavra" : "Nota"), 12));
            var document = MarkdownNoteView.Render(note.Note);
            var links = document.Blocks.OfType<Paragraph>().SelectMany(p => Links(p.Inlines)).DistinctBy(l => l.Target).ToList();
            bool Passage(NoteLink l) => NoteLinks.TryParse(l.Target, out var dest) && dest?.Kind is "bible" or "word";
            if (note.WordAnchor != null && links.All(l => l.Target != "verbum://word/" + note.Id))
                links.Add(new(note.SourceReference, "verbum://word/" + note.Id));
            foreach (bool bible in new[] { false, true })
            {
                var filtered = links.Where(l => Passage(l) == bible).ToList();
                Section((bible ? "Passagens relacionadas" : "Links") + $" ({filtered.Count})");
                foreach (var link in filtered) ActionButton(link.Label, () => { close(); open(link.Target); });
                ActionButton(bible ? "+ Adicionar passagem…" : "+ Adicionar link…", () => AddLink(bible));
            }
            Section("Etiquetas");
            var tags = new WrapPanel(); body.Children.Add(tags);
            foreach (var tag in NoteGraph.Tags(note.Note).Concat(note.NoteTags).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (note.NoteTags.Contains(tag) && !NoteGraph.Tags(note.Note).Contains(tag))
                    tags.Children.Add(MainWindow.Button(tag + " ×", () => { note.NoteTags.Remove(tag); touch(); Draw(); }, "Remover etiqueta"));
                else tags.Children.Add(MainWindow.Label(tag + "  ", 13, MainWindow.Green));
            }
            var input = new TextBox { ToolTip = "Nome da etiqueta", MaxLength = 100 }; body.Children.Add(input);
            ActionButton("+ Adicionar etiqueta", () =>
            {
                var tag = "#" + input.Text.Trim().TrimStart('#').Normalize().ToLowerInvariant();
                if (!Regex.IsMatch(tag, @"^#[\p{L}\p{Nd}_][\p{L}\p{M}\p{Nd}_/-]*$")) { input.ToolTip = "Use letras, números, hífen ou sublinhado."; input.Focus(); return; }
                if (!NoteGraph.Tags(note.Note).Concat(note.NoteTags).Contains(tag)) { note.NoteTags.Add(tag); touch(); }
                Draw();
            });
            ActionButton("Excluir nota…", () => { close(); delete(); });
        }
        Draw();
    }
    private static IEnumerable<NoteLink> Links(InlineCollection inlines)
    {
        foreach (Inline inline in inlines)
        {
            if (inline is Hyperlink { Tag: string url } link) yield return new(new TextRange(link.ContentStart, link.ContentEnd).Text, url);
            else if (inline is Span span) foreach (var child in Links(span.Inlines)) yield return child;
        }
    }
}