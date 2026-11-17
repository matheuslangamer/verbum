using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Verbum;

public sealed class ExegesisCatalog
{
    public string DirectoryPath { get; }
    private readonly ConcurrentDictionary<string, Lazy<ExegesisChapter?>> chapters = [];
    private readonly Lazy<List<TextualComment>> comments;
    private readonly ConcurrentDictionary<string, Dictionary<string, int>> frequencies = [];
    public Dictionary<string, int> Frequencies(string edition) => frequencies.GetOrAdd(edition, e => e is "WLC-OSHB" or "SBLGNT" ? Read<Dictionary<string, int>>(Path.Combine(DirectoryPath, e + "-lemma-counts.json.gz")) ?? [] : []);
    public ExegesisCatalog(string bibleDirectory)
    {
        DirectoryPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(bibleDirectory))!, "exegese");
        comments = new(() => Read<List<TextualComment>>(Path.Combine(DirectoryPath, "hottp.json.gz")) ?? []);
    }
    public ExegesisChapter? Chapter(string edition, int book, int chapter)
    {
        string source = edition == "BHS-A-Strong" ? "WLC-OSHB" : edition;
        if (source is not ("WLC-OSHB" or "SBLGNT") || book < 0 || book > 65 || chapter < 1 || chapter > 200) return null;
        string key = Path.Combine(source, $"{book:D2}-{chapter:D3}.json.gz");
        return chapters.GetOrAdd(key, k => new(() => Read<ExegesisChapter>(Path.Combine(DirectoryPath, k)))).Value;
    }
    public List<TextualComment> Comments(VerseEntry verse) => comments.Value.Where(c => c.References.Any(r => r.Book == verse.Book && r.Chapter == verse.Chapter && r.Verse >= verse.VerseStart && r.Verse <= verse.VerseEnd)).ToList();
    private static T? Read<T>(string path)
    {
        if (!File.Exists(path)) return default;
        using var file = File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress); return JsonSerializer.Deserialize<T>(gzip);
    }
}

public sealed record ExegesisPassage(int Book, int Chapter, int Start, int End, string Label);
public sealed record ExegesisRow(OriginalWord Word, List<ExegesisWord> Annotations, string Parsing, string Gloss, string LexicalKey);
public static class ExegesisData
{
    public static bool TryPassage(BibleEdition edition, string text, out ExegesisPassage? passage)
    {
        passage = null; string query = text.Trim().Replace('–', '-').Replace('—', '-');
        var interval = Regex.Match(query, @"^(.*?:\s*\d+)\s*-\s*(\d+)$");
        string first = interval.Success ? interval.Groups[1].Value : query;
        if (!BibleLibrary.TryReference(edition, first, out int book, out int chapter, out int? verse)) return false;
        if (!edition.Chapters.TryGetValue((book, chapter), out var entries)) return false;
        var numbered = entries.Where(v => v.IsMapped).ToList(); if (numbered.Count == 0) return false;
        int start = verse ?? numbered.Min(v => v.VerseStart!.Value), end = interval.Success ? int.Parse(interval.Groups[2].Value) : verse ?? numbered.Max(v => v.VerseEnd!.Value);
        if (end < start || BibleLibrary.ResolveVerse(edition, book, chapter, end) == null) return false;
        passage = new(book, chapter, start, end, edition.Books[book].Name + $" {chapter}:{start}" + (end != start ? $"–{end}" : "")); return true;
    }
    private static string Letters(string text) => string.Concat(SearchText.Normalize(text).Where(char.IsLetter));
    // Alignment is admitted only for a complete equal letter sequence, then equal pointed forms.
    // A lemma/Strong match alone never attributes an occurrence gloss or syntax to a word.
    public static Dictionary<OriginalWord, List<ExegesisWord>> Align(VerseEntry verse, ExegesisVerse? annotations)
    {
        var result = new Dictionary<OriginalWord, List<ExegesisWord>>(); if (annotations == null || annotations.Words.Count == 0) return result;
        var native = verse.Words; var rows = annotations.Words;
        if (Letters(string.Concat(native.Select(w => w.Text))) != Letters(string.Concat(rows.Select(w => w.Text)))) return result;
        var spans = new List<(int Start, int End, ExegesisWord Word)>(); int offset = 0;
        foreach (var row in rows) { int end = offset + Letters(row.Text).Length; spans.Add((offset, end, row)); offset = end; }
        offset = 0;
        foreach (var word in native)
        {
            int end = offset + Letters(word.Text).Length;
            var matches = spans.Where(s => s.Start >= offset && s.End <= end && s.End > s.Start).ToList();
            if (matches.Count > 0 && matches[0].Start == offset && matches[^1].End == end && PortugueseParsing.FormKey(word.Text) == PortugueseParsing.FormKey(string.Concat(matches.Select(s => s.Word.Text)))) result[word] = matches.Select(s => s.Word).ToList();
            offset = end;
        }
        return result;
    }
    public static List<ExegesisRow> Rows(BibleEdition edition, VerseEntry verse, ExegesisVerse? annotations, PortugueseParsing parsing, List<LexicalResource> lexicons)
    {
        var aligned = Align(verse, annotations);
        return verse.Words.Select(word =>
        {
            var data = aligned.GetValueOrDefault(word) ?? []; var personal = SearchText.HasHebrew(word.Text) ? parsing.Find(verse, word) : [];
            string morphology = word.Morphology.Length > 0 ? OriginalResource.MorphologySummary(word.Morphology) : data.Count > 0 ? string.Join(" + ", data.Select(Parsing)) + " · MACULA" : "Parsing da ocorrência não fornecido.";
            string gloss;
            var portuguese = personal.Select(m => m.Portuguese.Trim()).Where(s => s is not ("" or "—" or "-" or "–")).Distinct().Take(2).ToList();
            if (portuguese.Count > 0) gloss = string.Join(" / ", portuguese) + " · PT provisório, arquivo pessoal";
            else if (data.Any(d => !string.IsNullOrWhiteSpace(d.English))) gloss = string.Join(" + ", data.Where(d => d.English.Length > 0).Select(d => d.English)) + " · inglês, MACULA/Cherith";
            else { gloss = WordGlosses.Summary(personal, word, edition.Resource, lexicons); if (gloss.Length == 0) gloss = "Glossa não disponível nas fontes carregadas."; }
            string key = word.Strongs.FirstOrDefault() ?? data.Select(d => d.Strong).FirstOrDefault(s => s.Length > 0) ?? "";
            if (key.Length > 0 && !key.StartsWith('H') && !key.StartsWith('G')) key = (verse.Book < 39 ? "H" : "G") + key.TrimStart('0');
            return new ExegesisRow(word, data, morphology, gloss, key);
        }).ToList();
    }
    public static string Parsing(ExegesisWord word)
    {
        if (word.Morphology.Length > 0 && SearchText.HasHebrew(word.Text)) return OshbPortuguese.Describe("H" + word.Morphology).Split('\n')[0];
        return string.Join(" · ", new[] { Translate(word.PartOfSpeech) }.Concat(word.Features.Select(p => Translate(p.Key) + ": " + Translate(p.Value)))) + (word.Morphology.Length > 0 ? " (" + word.Morphology + ")" : "");
    }
    public static string Translate(string key) => key switch
    {
        "noun" => "substantivo", "verb" => "verbo", "adj" or "adjective" => "adjetivo", "adv" or "adverb" => "advérbio", "pron" or "pronoun" => "pronome", "prep" or "preposition" => "preposição", "conj" or "conjunction" => "conjunção", "det" or "article" => "artigo", "participle" => "particípio", "num" => "numeral", "interj" => "interjeição", "ptcl" => "partícula",
        "cl" => "oração", "np" => "sintagma nominal", "vp" => "sintagma verbal", "pp" => "sintagma preposicional", "s" => "sujeito", "o" => "objeto", "p" => "predicado", "v" => "verbo", "io" => "objeto indireto", "advp" => "sintagma adverbial",
        "gender" => "gênero", "number" => "número", "person" => "pessoa", "state" => "estado", "stem" => "binyan", "type" => "tipo", "tense" => "tempo", "voice" => "voz", "mood" => "modo", "case" => "caso", "degree" => "grau",
        "masculine" => "masculino", "feminine" => "feminino", "neuter" => "neutro", "singular" => "singular", "plural" => "plural", "dual" => "dual", "first" => "primeira", "second" => "segunda", "third" => "terceira", "nominative" => "nominativo", "genitive" => "genitivo", "dative" => "dativo", "accusative" => "acusativo", "vocative" => "vocativo", "present" => "presente", "aorist" => "aoristo", "future" => "futuro", "perfect" => "perfeito", "imperfect" => "imperfeito", "active" => "ativa", "passive" => "passiva", "middle" => "média", "indicative" => "indicativo", "imperative" => "imperativo", "subjunctive" => "subjuntivo", "infinitive" => "infinitivo", "proper" => "próprio", "common" => "comum", _ => key
    };
    public static string NoteAnchor(TabState state) => $"exegesis:{state.Edition}:{state.Book}:{state.Chapter}:{state.Verse}:{state.GuideEndVerse}";
}
