using System.IO;
using System.Text.Json;

namespace Verbum;

public sealed class BibleBook
{
    public string Abbrev { get; set; } = "";
    public string Name { get; set; } = "";
    public List<List<string>> Chapters { get; set; } = [];
}

public sealed record EditionInfo(string Id, string Path)
{
    public override string ToString() => Id;
}

public sealed record VerseEntry(string Edition, int Book, string BookName, string Abbrev, int Chapter,
    int EntryIndex, int? VerseStart, int? VerseEnd, string Text, bool ChapterValid)
{
    public bool IsSuperscription { get; init; }
    public int SuperscriptionPrefixLength { get; init; }
    public List<OriginalWord> Words { get; init; } = [];
    public List<SourceReference> KjvReferences { get; init; } = [];
    public List<string> Apparatus { get; init; } = [];
    public bool IsMapped => ChapterValid && VerseStart.HasValue;
    public string Number => VerseStart.HasValue ? VerseEnd > VerseStart ? $"{VerseStart}–{VerseEnd}" : $"{VerseStart}" : $"§{EntryIndex + 1}";
    public string Reference => ChapterValid ? $"{BookName} {Chapter}:{Number}" : $"{BookName} · capítulo no arquivo {Chapter}, entrada {EntryIndex + 1}";
}

public sealed class BibleEdition
{
    public Dictionary<(int Book, int Chapter, int Verse), List<VerseEntry>> FromKjv { get; } = [];
    public OriginalResource? Resource { get; init; }
    public required EditionInfo Info { get; init; }
    public required List<BibleBook> Books { get; init; }
    public List<VerseEntry> Entries { get; } = [];
    public List<string> Warnings { get; } = [];
    public Dictionary<(int, int), List<VerseEntry>> Chapters { get; } = [];
}
public sealed record VerseLink(List<VerseEntry> Targets, string Description);

public sealed class TabState
{
    public bool ReaderOptionsExpanded { get; set; }
    public bool GuideOptionsExpanded { get; set; }
    public bool StudyOptionsExpanded { get; set; }
    public string GuideReference { get; set; } = "";
    public int? GuideEndVerse { get; set; }
    public bool GuideAllWords { get; set; } = true;
    public int GuideThreshold { get; set; } = 30;
    public int GuidePart { get; set; }
    public int GuidePage { get; set; }
    public List<string> GuideExpanded { get; set; } = ["text", "words"];
    public WordAnnotation? WordAnchor { get; set; }
    public bool HideNoteMarkers { get; set; }
    public string NoteAnchor { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string DocumentTitle { get; set; } = "";
    public int DocumentSection { get; set; }
    public string DocumentQuery { get; set; } = "";
    public string LexiconSource { get; set; } = "Strong";
    public string LexiconQuery { get; set; } = "";
    public string LexiconCode { get; set; } = "";
    public string StudyTerm { get; set; } = "";
    public string StudyTranslation { get; set; } = "ARA";
    public SearchMode StudyMode { get; set; } = SearchMode.Strong;
    public string ReferenceSystem { get; set; } = "";
    public OriginalWord? SelectedWord { get; set; }
    public string SourceReference { get; set; } = "";
    public List<string> VariantNotes { get; set; } = [];
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "bible";
    public string Edition { get; set; } = "ALM1911";
    public int Book { get; set; } = 0;
    public int Chapter { get; set; } = 1;
    public int? Verse { get; set; }
    public int? EntryIndex { get; set; }
    public string LinkGroup { get; set; } = "Livre";
    public double FontSize { get; set; } = 20;
    public double Scroll { get; set; }
    public bool ShowInterlinear { get; set; }
    public string NoteEditorMode { get; set; } = "visual";
    public int NoteCaret { get; set; }
    public int NoteSelectionLength { get; set; }
    public DateTimeOffset? NoteCreated { get; set; }
    public DateTimeOffset? NoteEdited { get; set; }
    public List<string> NoteTags { get; set; } = [];
    public string NoteColor { get; set; } = "";
    public string NoteTitle { get; set; } = "Notas do estudo";
    public string Note { get; set; } = "";
}

public sealed class LayoutNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "pane";
    public string Direction { get; set; } = "horizontal";
    public double Ratio { get; set; } = 0.5;
    public List<LayoutNode> Children { get; set; } = [];
    public List<TabState> Tabs { get; set; } = [];
    public string? ActiveTab { get; set; }
    public IEnumerable<LayoutNode> Leaves() => Kind == "pane" ? [this] : Children.SelectMany(x => x.Leaves());
    public static LayoutNode Default(IEnumerable<EditionInfo> editions)
    {
        var ids = editions.Select(x => x.Id).ToList();
        var first = ids.Contains("ALM1911") ? "ALM1911" : ids.FirstOrDefault() ?? "";
        var second = ids.Contains("TB") ? "TB" : ids.Skip(1).FirstOrDefault() ?? first;
        return new() { Kind = "split", Ratio = .54, Children = [
            new() { Tabs = [new() { Edition = first, Book = 42, Chapter = 1, LinkGroup = "A" }] },
            new() { Tabs = [new() { Edition = second, Book = 42, Chapter = 1, LinkGroup = "A" }, new() { Kind = "notes", NoteCreated = DateTimeOffset.Now }] }
        ] };
    }
}

public sealed class StudySession
{
    public bool SidebarCollapsed { get; set; } = true;
    public int Schema { get; set; } = 1;
    public string LibraryPath { get; set; } = "";
    public LayoutNode Layout { get; set; } = new();
    public double Width { get; set; } = 1450;
    public double Height { get; set; } = 940;
}

public static class SessionStore
{
    public static readonly string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Verbum");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static StudySession? Load()
    {
        foreach (var name in new[] { "session.json", "session.backup.json" })
        {
            try
            {
                var session = JsonSerializer.Deserialize<StudySession>(File.ReadAllText(Path.Combine(DataDirectory, name)), Options);
                if (session?.Schema == 1 && Validate(session.Layout, 0)) return session;
            }
            catch { }
        }
        return null;
    }
    internal static bool Validate(LayoutNode? node, int depth)
    {
        if (node == null || depth > 12) return false;
        if (node.Kind == "pane") return node.Tabs is { Count: <= 100 } && node.Tabs.All(t => t != null && t.Kind is "bible" or "notes" or "analysis" or "apparatus" or "wordstudy" or "lexicon" or "library" or "book" or "exegesis" or "notescatalog");
        return node.Kind == "split" && node.Children is { Count: 2 } && node.Children.All(c => Validate(c, depth + 1));
    }
    public static void Save(StudySession session)
    {
        Directory.CreateDirectory(DataDirectory);
        var path = Path.Combine(DataDirectory, "session.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(session, Options));
        if (File.Exists(path)) File.Replace(temp, path, Path.Combine(DataDirectory, "session.backup.json"));
        else File.Move(temp, path);
    }
}
