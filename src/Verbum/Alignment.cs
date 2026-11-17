using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Verbum;

// Automatic word alignment between a translation and the WLC/OSHB words. One file per
// translation in biblioteca/alinhamento, produced by AlignmentBuilder from the local texts.
public sealed class AlignmentFile
{
    public const string CurrentSchema = "verbum.alignment.v1";
    public string Schema { get; set; } = CurrentSchema;
    public string Translation { get; set; } = "";
    public string Source { get; set; } = "WLC-OSHB";
    public string Method { get; set; } = "";
    public string Generated { get; set; } = "";
    public long TranslationBytes { get; set; }
    public Dictionary<string, int> Stats { get; set; } = [];
    public List<AlignedVerse> Verses { get; set; } = [];
}
public sealed class AlignedVerse
{
    public int B { get; set; }
    public int C { get; set; }
    public int V { get; set; }
    public List<AlignedWord> W { get; set; } = [];
    /// <summary>Each link: book, chapter, entry, start, length (translation text) · word, segment (−1 = whole word) · confidence 1–3.</summary>
    public List<int[]> L { get; set; } = [];
}
public sealed class AlignedWord
{
    /// <summary>Pointed form with "/" between OSHB segments.</summary>
    public string T { get; set; } = "";
    public string L { get; set; } = "";
    public string S { get; set; } = "";
    public string M { get; set; } = "";
    /// <summary>Consonant offset and count inside the verse.</summary>
    public int O { get; set; }
    public int N { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string Text => T.Replace("/", "");
    [System.Text.Json.Serialization.JsonIgnore] public string[] Segments => T.Split('/');
    public OriginalWord ToOriginal()
    {
        var word = new OriginalWord { Text = Text, Lemma = L, Strongs = S.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList(), Morphology = M };
        int at = 0; foreach (string segment in Segments) { word.Segments.Add(new(segment, at)); at += segment.Length; }
        return word;
    }
}

public sealed record AlignedTerm(AlignedVerse Verse, AlignedWord Word, int WordIndex, int Segment, int Confidence, int Start, int Length);
public sealed record AlignedTranslation(string Text, int Confidence, string Reference);

public sealed class TranslationAlignment
{
    public AlignmentFile File { get; }
    public string Translation => File.Translation;
    private readonly Dictionary<(int, int, int), List<(AlignedVerse Verse, int[] Link)>> byTarget = [];
    private readonly Dictionary<(int, int, int), AlignedVerse> bySource = [];

    public TranslationAlignment(AlignmentFile file)
    {
        File = file;
        foreach (var verse in file.Verses)
        {
            bySource[(verse.B, verse.C, verse.V)] = verse;
            foreach (var link in verse.L)
            {
                if (link.Length != 8 || link[5] < 0 || link[5] >= verse.W.Count) continue;
                var key = (link[0], link[1], link[2]);
                if (!byTarget.TryGetValue(key, out var list)) byTarget[key] = list = [];
                list.Add((verse, link));
            }
        }
    }

    public static string PathFor(string libraryPath, string translation)
        => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(libraryPath))!, "alinhamento", translation + ".json.gz");

    public static TranslationAlignment? Load(string path, long translationBytes)
    {
        if (!System.IO.File.Exists(path)) return null;
        using var stream = new GZipStream(System.IO.File.OpenRead(path), CompressionMode.Decompress);
        var file = JsonSerializer.Deserialize<AlignmentFile>(stream, new JsonSerializerOptions { MaxDepth = 12 });
        // Offsets belong to one exact text; a different file must be aligned again.
        if (file == null || file.Schema != AlignmentFile.CurrentSchema || file.TranslationBytes != translationBytes) return null;
        return new(file);
    }
    public static void Save(string path, AlignmentFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        using (var stream = new GZipStream(System.IO.File.Create(temp), CompressionLevel.SmallestSize)) JsonSerializer.Serialize(stream, file);
        System.IO.File.Move(temp, path, true);
    }

    public bool Covers(VerseEntry verse) => byTarget.ContainsKey((verse.Book, verse.Chapter, verse.EntryIndex));

    /// <summary>Original words linked to the translation text in [start, start + length).</summary>
    public List<AlignedTerm> Originals(VerseEntry verse, int start, int length)
    {
        if (!byTarget.TryGetValue((verse.Book, verse.Chapter, verse.EntryIndex), out var links)) return [];
        return links.Where(x => x.Link[3] < start + length && x.Link[3] + x.Link[4] > start && x.Link[3] + x.Link[4] <= verse.Text.Length)
            .Select(x => new AlignedTerm(x.Verse, x.Verse.W[x.Link[5]], x.Link[5], x.Link[6], x.Link[7], x.Link[3], x.Link[4]))
            .OrderByDescending(t => t.Confidence).ThenBy(t => t.WordIndex).ToList();
    }

    /// <summary>Translation expressions linked to one original word or prefix/suffix segment.</summary>
    public List<AlignedTranslation> Translations(VerseEntry verse, OriginalWord word, BibleEdition translation)
    {
        if (!verse.VerseStart.HasValue || !bySource.TryGetValue((verse.Book, verse.Chapter, verse.VerseStart.Value), out var aligned)) return [];
        int start = word.ConsonantStart, length = MorphologyLinks.Letters(word.Text).Length;
        if (start < 0)
        {
            start = 0;
            foreach (var before in verse.Words) { if (ReferenceEquals(before, word)) break; start += MorphologyLinks.Letters(before.Text).Length; }
        }
        if (length == 0) return [];
        var wanted = new HashSet<(int Word, int Segment)>();
        for (int w = 0; w < aligned.W.Count; w++)
        {
            var candidate = aligned.W[w];
            if (candidate.O >= start + length || candidate.O + candidate.N <= start) continue;
            // The same consonants must be at this position; otherwise the editions differ here.
            bool whole = candidate.O >= start && candidate.O + candidate.N <= start + length;
            if (whole && MorphologyLinks.Letters(candidate.Text) != MorphologyLinks.Letters(word.Text) && candidate.N == length) continue;
            if (whole) wanted.Add((w, -1));
            int at = candidate.O; var segments = candidate.Segments;
            for (int s = 0; s < segments.Length; s++)
            {
                int size = MorphologyLinks.Letters(segments[s]).Length;
                if (size > 0 && at >= start && at + size <= start + length) wanted.Add((w, s));
                at += size;
            }
        }
        var results = new List<AlignedTranslation>();
        var links = aligned.L.Where(l => l.Length == 8 && wanted.Contains((l[5], l[6]))).ToList();
        // Uncertain links are shown only when nothing better points to this word.
        if (links.Any(l => l[7] >= 2)) links.RemoveAll(l => l[7] < 2);
        foreach (var group in links.GroupBy(l => (l[0], l[1], l[2])))
        {
            if (!translation.Chapters.TryGetValue((group.Key.Item1, group.Key.Item2), out var entries) || group.Key.Item3 >= entries.Count) continue;
            var target = entries[group.Key.Item3]; var tokens = SearchText.Tokenize(target.Text);
            var picked = group.Where(l => l[3] + l[4] <= target.Text.Length).OrderBy(l => l[3]).ToList();
            if (picked.Count == 0) continue;
            var text = new System.Text.StringBuilder(); int previous = -1;
            foreach (var link in picked)
            {
                int index = tokens.FindIndex(t => t.Start == link[3]);
                if (index < 0) continue;
                if (text.Length > 0) text.Append(previous >= 0 && index == previous + 1 ? " " : " … ");
                text.Append(target.Text.AsSpan(link[3], link[4])); previous = index;
            }
            if (text.Length > 0) results.Add(new(text.ToString(), picked.Min(l => l[7]), target.Reference));
        }
        return results;
    }

    public static string ConfidenceLabel(int confidence) => confidence >= 3 ? "ligação provável" : confidence == 2 ? "ligação plausível" : "ligação incerta";
}
