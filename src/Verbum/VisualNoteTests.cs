using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Verbum;

internal static class VisualNoteTests
{
    internal static void Run(Action<string, bool> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "verbum-notes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var note = new TabState { Id = "note123", Kind = "notes", NoteTitle = "Meu estudo", Note = "# Última edição\n\n[outra](verbum://note/note456)" };
            File.WriteAllText(Path.Combine(folder, note.Id + ".json"), JsonSerializer.Serialize(note)); File.WriteAllText(Path.Combine(folder, note.Id + ".md"), note.Note);
            var store = new NoteStore(folder); store.Trash(note);
            check("Deleting notes moves latest content into restorable trash", store.IsTrashed(note.Id) && store.Deleted().Single().Note == note.Note && !File.Exists(Path.Combine(folder, note.Id + ".json")));
            var layout = new LayoutNode { Tabs = [note, new() { Kind = "notescatalog" }], ActiveTab = note.Id }; store.RemoveTrashedTabs(layout);
            check("Trashed note cannot resurrect from saved layout or exit checkpoint", layout.Tabs.Count == 1 && layout.Tabs[0].Kind == "notescatalog" && layout.ActiveTab == layout.Tabs[0].Id);
            var restored = store.Restore(note.Id);
            check("Restoring notes retains identity links title and exact Markdown", !store.IsTrashed(note.Id) && restored.Id == note.Id && restored.Note == note.Note && restored.NoteTitle == note.NoteTitle && File.ReadAllText(Path.Combine(folder, note.Id + ".md")) == note.Note);
            store.Trash(restored); store.Purge(restored.Id);
            var oldLayout = new LayoutNode { Tabs = [restored] }; store.RemoveTrashedTabs(oldLayout);
            check("Permanent deletion removes trash JSON and Markdown and blocks stale sessions", store.Deleted().Count == 0 && oldLayout.Tabs.Count == 0 &&
                !File.Exists(Path.Combine(folder, "lixeira", restored.Id + ".md")) && !File.Exists(Path.Combine(folder, restored.Id + ".json")));
            var verse = new VerseEntry("ARA", 0, "Gênesis", "Gn", 1, 0, 1, 1, "Luz e luz.", true);
            var annotated = WordAnnotation.Create(verse, 6, 9);
            var copy = JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(annotated))!;
            check("Word annotation round-trips the exact repeated word and rejects changed verse text", copy.WordAnchor is { Start: 6, Text: "luz" } a && a.Matches(verse) && !a.Matches(verse with { Text = "Luz e som." }));
            check("Linked hashtag survives Markdown rendering and resolves to its word annotation", annotated.Note.StartsWith("[#luz](") && NoteLinks.TryParse("verbum://word/" + annotated.Id, out var dest) && dest?.Id == annotated.Id && dest.Kind == "word");
            check("Graph includes shared Unicode hashtags and ignores code and link destinations", NoteGraph.Tags(annotated.Note + " #Fé #FÉ [site](https://example.com/#fake) `#code`\n```\n#code\n```").Distinct().SequenceEqual(new[] { "#luz", "#fé" }));
            var other = new TabState { Id = "note456", Kind = "notes" }; var fake = new TabState { Id = "fake", Kind = "notes", Note = "`[não](verbum://note/note456)`\n```\n[falso](verbum://note/note123)\n```" };
            var edges = NoteGraph.Edges([note, other, fake]);
            check("Note graph uses real directed links excludes code and duplicate edges", edges.SetEquals(new[] { ("note123", "note456") }));
        }
        finally { Directory.Delete(folder, true); }
        Application.Current.Dispatcher.Invoke(() =>
        {
            string source = "# Título\r\n\r\n**forte** e *ênfase*\r\n\r\n| coluna | valor |\r\n| --- | --- |\r\n| אָב | texto |\r\n\r\n```hebrew\r\nבָּרָא\r\n```\r\n\r\n[passagem](verbum://bible?edition=ARA&reference=Joel%201%3A1)\r\n";
            var document = VisualMarkdown.Load(source, null);
            check("Opening visual mode without editing preserves exact Markdown including tables fences and CRLF", VisualMarkdown.Save(document) == source);
            var first = (Paragraph)document.Blocks.FirstBlock; first.Inlines.Add(new Run(" alterado")); string changed = VisualMarkdown.Save(document);
            check("Visual editing preserves untouched unsupported Markdown and internal links", changed.Contains("# Título alterado") && changed.Contains("| coluna | valor |\r\n| --- | --- |") && changed.Contains("```hebrew\r\nבָּרָא\r\n```") && changed.Contains("verbum://bible?edition=ARA"));
            var literal = VisualMarkdown.Load("texto", null); var literalParagraph = (Paragraph)literal.Blocks.FirstBlock; literalParagraph.Inlines.Clear(); literalParagraph.Inlines.Add(new Run("# texto literal"));
            check("Visual plain text does not accidentally become a Markdown heading", VisualMarkdown.Save(literal).StartsWith("\\# texto literal"));
            var flow = VisualMarkdown.Load("palavra", null); var paragraph = (Paragraph)flow.Blocks.FirstBlock; var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd); range.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
            check("Visual formatting serializes bold text to Markdown", VisualMarkdown.Save(flow).Trim() == "**palavra**");
            var colors = VisualMarkdown.Load("Antes palavra depois", null);
            var colorRun = (Run)((Paragraph)colors.Blocks.FirstBlock).Inlines.FirstInline;
            var selected = new TextRange(colorRun.ContentStart.GetPositionAtOffset(6)!, colorRun.ContentStart.GetPositionAtOffset(13)!);
            selected.ApplyPropertyValue(TextElement.BackgroundProperty, Theme.Raw("#FFF09A"));
            selected.ApplyPropertyValue(TextElement.ForegroundProperty, Theme.Raw("#800080"));
            string colored = VisualMarkdown.Save(colors);
            var result = MarkdownNoteView.Render(colored);
            var coloredPara = (Paragraph)result.Blocks.FirstBlock;
            var bg = coloredPara.Inlines.OfType<Span>().FirstOrDefault(s => s.Background is SolidColorBrush b && b.Color.ToString() == "#FFFFF09A");
            check("Selected word retains independent highlighter and font color through Markdown", bg?.Inlines.OfType<Span>().Any(s => s.Foreground is SolidColorBrush b && b.Color.ToString() == "#FF800080" && new TextRange(s.ContentStart, s.ContentEnd).Text == "palavra") == true &&
                new TextRange(coloredPara.ContentStart, coloredPara.ContentEnd).Text == "Antes palavra depois");
            check("Highlight markup does not invent graph tags", !NoteGraph.Tags(colored).Any() && NoteGraph.Tags(colored + " #estudo").Single() == "#estudo");
            var defaultHighlight = MarkdownNoteView.Render("==memória==");
            check("Double-equals highlights render in the preview", ((Paragraph)defaultHighlight.Blocks.FirstBlock).Inlines.FirstInline is Span { Background: SolidColorBrush });
            var restoredColors = VisualMarkdown.Load(colored, null);
            ((Paragraph)restoredColors.Blocks.FirstBlock).Inlines.Add(new Run("!"));
            string revised = VisualMarkdown.Save(restoredColors);
            check("Editing a restored colored paragraph preserves its colors", revised.Contains("#FFF09A") && revised.Contains("#800080"));
            var sampleNote = new TabState { NoteTitle = "Memória", NoteColor = "#BBDDF8" };
            var catalog = new NotesCatalogView(() => [sampleNote], _ => { }, () => { }, _ => { }, () => [sampleNote], _ => { }, _ => { });
            catalog.Measure(new(440, 650)); catalog.Arrange(new(0, 0, 440, 650)); catalog.UpdateLayout();
            var navigation = Find<Button>(catalog).Single(b => Equals(b.Content, "Lixeira"));
            navigation.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); catalog.UpdateLayout();
            check("Trash and active notes share the visible navigation menu and search list", navigation.Visibility == Visibility.Visible && Find<Button>(catalog).Any(b => Equals(b.Content, "Notas")) && Find<Button>(catalog).Any(b => Equals(b.Content, "Restaurar")));
            var quoteDoc = VisualMarkdown.Load("> <!--quote:#800080--> Memória", null);
            var quoted = (Paragraph)quoteDoc.Blocks.FirstBlock;
            quoted.Inlines.Add(new Run(" e tradição"));
            check("Quote retains accent after a Markdown round trip", quoted.BorderThickness.Left == 2 && quoted.BorderBrush is SolidColorBrush accent && accent.Color.ToString() == "#FF800080" && VisualMarkdown.Save(quoteDoc).Contains("> <!--quote:#800080--> Memória e tradição"));
            var infoState = new TabState { Note = "[Fonte](https://example.com) " + NoteLinks.Markdown("Joel", NoteLinks.Bible("ARA", "Joel 1:1")) };
            string appended = "";
            var infoPanel = new NoteInfoView(infoState, () => { }, value => appended = value, bible => Task.FromResult<NoteLink?>(new("Joel", NoteLinks.Bible("ARA", "Joel 1:1"))), _ => { }, () => { }, () => { });
            infoPanel.Measure(new(310, 620)); infoPanel.Arrange(new(0, 0, 310, 620)); infoPanel.UpdateLayout();
            var infoText = Find<TextBlock>(infoPanel).Select(t => t.Text).ToList();
            check("Note information distinguishes links and passages and handles undated older notes", infoText.Contains("Links (1)") && infoText.Contains("Passagens relacionadas (1)") && infoText.Contains("Criada · sem registro"));
            Find<Button>(infoPanel).Single(b => Equals(b.Content, "+ Adicionar passagem…")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Find<TextBox>(infoPanel).Single().Text = "aula";
            Find<Button>(infoPanel).Single(b => Equals(b.Content, "+ Adicionar etiqueta")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Info actions append passage Markdown and store a tag", appended.Contains("verbum://bible") && infoState.NoteTags.SequenceEqual(new[] { "#aula" }));
            string content = "Texto normal"; var state = new TabState { NoteEditorMode = "visual" };
            var editor = new MarkdownNoteView(content, value => content = value, _ => Task.FromResult<NoteLink?>(new("Referência", NoteLinks.Note("note456"))), state: state);
            editor.Width = 760; editor.Height = 600; editor.Measure(new(760, 600)); editor.Arrange(new(0, 0, 760, 600)); editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent)); editor.UpdateLayout();
            var rich = Find<RichTextBox>(editor).Single(); var buttons = Find<Button>(editor).ToList();
            check("Normal mode exposes editable formatted text without Markdown source", rich.Visibility == Visibility.Visible && rich.ActualWidth > 500 && new TextRange(rich.Document.ContentStart, rich.Document.ContentEnd).Text.Contains("Texto normal"));
            var r = (Run)((Paragraph)rich.Document.Blocks.FirstBlock).Inlines.FirstInline;
            rich.Selection.Select(r.ContentStart, r.ContentStart.GetPositionAtOffset(5)!);
            foreach (string tool in new[] { "Marcador ▾", "Letra ▾" })
            {
                var palette = buttons.Single(b => Equals(b.Content, tool));
                palette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                rich.Selection.Select(rich.Document.ContentEnd, rich.Document.ContentEnd);
                ((MenuItem)palette.ContextMenu!.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                palette.ContextMenu.IsOpen = false;
            }
            check("Toolbar palettes restore selected word after focus leaves the editor", content.Contains("#FFF09A") && content.Contains("#0000FF") && rich.Selection.Text == "Texto");
            rich.Selection.Select(rich.Document.ContentStart, rich.Document.ContentEnd); buttons.Single(b => b.Tag as string == "Link…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Normal mode inserts persistent internal links", content.Contains("[Referência](verbum://note/note456)"));
            buttons.Single(b => b.Content as string == "MD <>").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); buttons.Single(b => b.Content as string == "Normal").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("Switching normal and MD modes retains edited content and mode", state.NoteEditorMode == "visual" && VisualMarkdown.Save(rich.Document) == content);
        });
    }
    private static IEnumerable<T> Find<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T value) yield return value; foreach (var item in Find<T>(child)) yield return item; }
    }
}
