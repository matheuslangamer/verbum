namespace Verbum;

public sealed record WordAnnotation(string Edition, int Book, int Chapter, int Entry, int Start, string Text, string VerseText)
{
    public bool Matches(VerseEntry verse) => verse.Edition == Edition && verse.Book == Book && verse.Chapter == Chapter &&
        verse.EntryIndex == Entry && verse.Text == VerseText && Start >= 0 && Text.Length > 0 &&
        Start <= verse.Text.Length - Text.Length && verse.Text.AsSpan(Start, Text.Length).SequenceEqual(Text);

    internal static TabState Create(VerseEntry verse, int start, int end)
    {
        string text = verse.Text[start..end];
        string tag = "#" + string.Join("-", SearchText.Tokenize(text).Select(t => t.Original));
        var note = new TabState { Kind = "notes", NoteCreated = DateTimeOffset.Now, Edition = verse.Edition, SourceReference = verse.Reference,
            NoteTitle = text + " · " + verse.Reference, WordAnchor = new(verse.Edition, verse.Book, verse.Chapter, verse.EntryIndex, start, text, verse.Text) };
        note.Note = NoteLinks.Markdown(tag, "verbum://word/" + note.Id) + " · " + verse.Reference + " (" + verse.Edition + ")\n\n";
        return note;
    }
}
