using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Verbum;

internal static class NoteTests
{
    internal static void Run(Action<string, bool> check)
    {
        string bible = NoteLinks.Bible("WLC-OSHB", "Joel 1:1–3"), note = NoteLinks.Note("abc123"), book = NoteLinks.Book("book123", 7);
        check("Note links retain edition native reference note identity and book section", NoteLinks.TryParse(bible, out var b) && b is { Kind: "bible", Id: "WLC-OSHB", Reference: "Joel 1:1–3" } && NoteLinks.TryParse(note, out var n) && n is { Kind: "note", Id: "abc123" } && NoteLinks.TryParse(book, out var d) && d is { Kind: "book", Section: 7 });
        check("Note links reject local files scripts traversal unknown hosts and invalid section", new[] { "file:///C:/test.txt", "javascript:alert(1)", "verbum://note/../secret", "verbum://other/abc", "verbum://book/abc?section=-1", "verbum://bible?edition=ARA&edition=TB&reference=Joao" }.All(url => !NoteLinks.TryParse(url, out _)));
        var saved = JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(new TabState { Kind = "notes", Note = "texto **original**", NoteEditorMode = "split", NoteCaret = 4, NoteSelectionLength = 3 }));
        check("Notes catalog and editor state survive session serialization", SessionStore.Validate(new() { Tabs = [new() { Kind = "notescatalog" }, saved!] }, 0) && saved is { Note: "texto **original**", NoteEditorMode: "split", NoteCaret: 4, NoteSelectionLength: 3 });
        VisualNoteTests.Run(check);
        Application.Current.Dispatcher.Invoke(() =>
        {
            string? clicked = null;
            var flow = MarkdownNoteView.Render("# Estudo\n" + NoteLinks.Markdown("Bíblia [texto]", bible) + "\n" + NoteLinks.Markdown("Comentário", note) + "\n" + NoteLinks.Markdown("Livro", book) + "\n[site](https://example.com)\n[não abrir](file:///C:/test.txt)\n**forte e *ênfase***\n```\n[sem link](https://example.com)\n```", url => clicked = url);
            var links = flow.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()).ToList();
            links.First().RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            check("Markdown renders safe links and routes clicks without launching on render", links.Count == 4 && clicked == bible && new TextRange(links[0].ContentStart, links[0].ContentEnd).Text == "Bíblia [texto]");
            string content = "palavra"; var state = new TabState { NoteEditorMode = "edit" };
            var view = new MarkdownNoteView(content, value => content = value, _ => Task.FromResult<NoteLink?>(new("Passagem", bible)), state: state);
            view.Measure(new(760, 600)); view.Arrange(new(0, 0, 760, 600)); view.UpdateLayout();
            var input = Find<TextBox>(view).Single(); var buttons = Find<Button>(view).ToList();
            input.Select(0, 7); buttons.Single(x => (x.Content as string) == "B").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            bool bold = content == "**palavra**";
            input.Select(2, 7); buttons.Single(x => (x.Tag as string) == "Link…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Editor formats selection inserts internal link and keeps surrounding text", bold && content == "**" + NoteLinks.Markdown("Passagem", bible) + "**");
            ((MenuItem)buttons.Single(x => Equals(x.Content, "Lado a lado")).ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            check("Editor preview mode persists and exposes clickable link", state.NoteEditorMode == "preview" && Find<FlowDocumentScrollViewer>(view).Single().Document.Blocks.OfType<Paragraph>().Any(p => p.Inlines.OfType<Bold>().Any()));
        });
    }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T value) yield return value; foreach (var nested in Find<T>(child)) yield return nested; }
    }
}
