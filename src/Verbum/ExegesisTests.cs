using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Verbum;

internal static class ExegesisTests
{
    public static void Run(BibleLibrary library, Action<string, bool> check)
    {
        var wlc = library.LoadAsync("WLC-OSHB").GetAwaiter().GetResult();
        var greek = library.LoadAsync("SBLGNT").GetAwaiter().GetResult();
        var catalog = library.Exegesis;
        check("Guide parses same-chapter range and preserves native MT Joel four", ExegesisData.TryPassage(wlc, "Joel 4:1–3", out var range) && range is { Book: 28, Chapter: 4, Start: 1, End: 3 });
        check("Guide rejects reversed range without changing reading reference", !ExegesisData.TryPassage(wlc, "Joel 1:3-1", out _) && !ExegesisData.TryPassage(wlc, "Joel 1:999", out _));
        check("Guide whole chapter includes exact native bounds", ExegesisData.TryPassage(wlc, "Joel 3", out var whole) && whole is { Start: 1, End: 5 });
        check("Guide accepts Greek passage and rejects OT in Greek source", ExegesisData.TryPassage(greek, "João 1:1-5", out var john) && john is { Book: 42, End: 5 } && !ExegesisData.TryPassage(greek, "Joel 1:1", out _));
        var joel = catalog.Chapter("WLC-OSHB", 28, 1)!;
        check("MACULA Hebrew actual Joel chapter includes all twenty verses", joel.Verses.Count == 20 && joel.Revision == "47db250bd55d0d8577f2a94fba114ef16c35b23c");
        var tellVerse = BibleLibrary.ResolveVerse(wlc, 28, 1, 3)!;
        var personal = library.Parsing.GetAwaiter().GetResult(); var lexicons = library.Lexicons.GetAwaiter().GetResult();
        var tellRows = ExegesisData.Rows(wlc, tellVerse, joel.Verses.Single(v => v.Verse == 3), personal, lexicons);
        check("MACULA occurrence gloss resolves screenshot tell verb", tellRows.Any(r => r.Word.Strongs.Contains("H5608a") && r.Annotations.Any(a => a.English == "tell") && r.Gloss.Contains("tell") && r.Parsing.Contains("imperativo")));
        var nameVerse = BibleLibrary.ResolveVerse(wlc, 28, 1, 1)!;
        var nameRows = ExegesisData.Rows(wlc, nameVerse, joel.Verses.Single(v => v.Verse == 1), personal, lexicons);
        check("MACULA proper name Joel has occurrence-linked English gloss", nameRows.Any(r => r.Word.Strongs.Contains("H3100") && r.Annotations.Count == 1 && r.Gloss.Contains("Joel")));
        check("MACULA split suffixes align to complete native word", tellRows.Any(r => r.Annotations.Count >= 3 && r.Word.Text.Contains("בְנֵי")));
        var changed = tellVerse with { Words = [new() { Text = "שונה" }] };
        check("MACULA rejects changed consonantal sequence instead of guessing by Strong", ExegesisData.Align(changed, joel.Verses.Single(v => v.Verse == 3)).Count == 0);
        var pointedWord = new OriginalWord { Text = "בָּרָא" };
        var pointedVerse = new VerseEntry("TEST", 0, "", "", 1, 0, 1, 1, "בָּרָא", true) { Words = [pointedWord] };
        check("MACULA retains vowel distinctions after matching consonants", ExegesisData.Align(pointedVerse, new() { Words = [new() { Text = "בְּרֹא", Id = "other" }] }).Count == 0);
        var johnChapter = catalog.Chapter("SBLGNT", 42, 1)!;
        var logosVerse = BibleLibrary.ResolveVerse(greek, 42, 1, 1)!;
        var greekRows = ExegesisData.Rows(greek, logosVerse, johnChapter.Verses.Single(v => v.Verse == 1), personal, lexicons);
        check("MACULA Greek supplies real word gloss and parsing for logos", greekRows.Any(r => r.Word.Text == "λόγος" && r.Annotations.Any(a => a.Lemma == "λόγος" && a.English == "word") && r.Parsing.Contains("nominativo")));
        check("MACULA Greek class labels support verb filters", greekRows.Any(r => r.Annotations.Any(a => a.PartOfSpeech == "verb" && a.Lemma == "εἰμί")));
        check("Source Greek text and native annotations remain unchanged", logosVerse.Words.All(w => w.Strongs.Count == 0 && w.Morphology.Length == 0));
        check("MACULA Greek frequency index counts lemma occurrences", catalog.Frequencies("SBLGNT").GetValueOrDefault("λόγος") > 100);
        var hottp = catalog.Comments(BibleLibrary.ResolveVerse(wlc, 28, 1, 9)!);
        check("HOTTP maps Joel 1:9 to real alternatives and committee ratings", hottp.Count > 0 && hottp.Any(c => c.Alternatives.Count > 1 && c.Alternatives.Any(a => a.Rating.Length > 0)));
        check("HOTTP does not attach nearby note to Joel 1:1", catalog.Comments(nameVerse).Count == 0);
        check("Missing exegesis source is an explicit empty result", catalog.Chapter("unknown", 28, 1) == null && catalog.Chapter("WLC-OSHB", 100, 1) == null);
        var saved = new TabState { Kind = "exegesis", Edition = "WLC-OSHB", Book = 28, Chapter = 4, Verse = 1, GuideEndVerse = 3, GuideReference = "Joel 4:1–3", GuideThreshold = 12, GuideAllWords = false, GuidePart = 1, GuideExpanded = ["syntax"], Scroll = 170 };
        var restored = JsonSerializer.Deserialize<TabState>(JsonSerializer.Serialize(saved))!;
        check("Guide state restores range filters expansion and scroll", restored.GuideReference == saved.GuideReference && restored.GuideThreshold == 12 && !restored.GuideAllWords && restored.GuidePart == 1 && restored.GuideExpanded.SequenceEqual(["syntax"]) && restored.Scroll == 170 && SessionStore.Validate(new() { Tabs = [restored] }, 0));
        check("Passage note anchors distinguish edition and range without changing notes", ExegesisData.NoteAnchor(saved) == ExegesisData.NoteAnchor(restored) && ExegesisData.NoteAnchor(saved) != ExegesisData.NoteAnchor(new() { Edition = "WLC-OSHB", Book = 28, Chapter = 4, Verse = 1, GuideEndVerse = 4 }));
        int chapters = 0, words = 0, groups = 0; bool valid = true;
        foreach (string directory in new[] { "WLC-OSHB", "SBLGNT" })
        foreach (string path in Directory.EnumerateFiles(Path.Combine(catalog.DirectoryPath, directory), "*.json.gz"))
        {
            using var file = File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress); var data = JsonSerializer.Deserialize<ExegesisChapter>(gzip)!; chapters++;
            valid &= data.Edition == directory && data.Verses.Select(v => v.Verse).Distinct().Count() == data.Verses.Count;
            foreach (var v in data.Verses)
            {
                var ids = v.Words.Select(w => w.Id).ToHashSet(); valid &= ids.Count == v.Words.Count && v.Groups.All(g => g.Words.All(ids.Contains)); words += v.Words.Count; groups += v.Groups.Count;
            }
        }
        check("All 1189 MACULA chapter indices preserve unique words and valid syntax members", valid && chapters == 1189 && words == 613652 && groups == 447807);
    }
}
