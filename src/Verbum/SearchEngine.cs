using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Verbum;

public enum SearchMode { Intelligent, Exact, Prefix, Contains, Strong, Lemma }
public sealed record SearchToken(string Original, string Normalized, int Start, int Length);

public static class SearchText
{
    private static readonly Regex Words = new(@"[\p{L}\p{M}\p{Nd}\p{Cf}]+", RegexOptions.Compiled);
    // The normalized key is for matching only. Never replace the displayed source text.
    public static string Normalize(string value)
    {
        var builder = new StringBuilder();
        foreach (Rune rune in value.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var cat = Rune.GetUnicodeCategory(rune);
            if (cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) continue;
            if (Rune.IsLetterOrDigit(rune)) builder.Append(rune.Value == 0x03C2 ? new Rune(0x03C3) : Rune.ToLowerInvariant(rune));
            else if (builder.Length > 0 && builder[^1] != ' ') builder.Append(' ');
        }
        return builder.ToString().Trim();
    }
    public static List<SearchToken> Tokenize(string text) => Words.Matches(text).Select(m => new SearchToken(m.Value, Normalize(m.Value), m.Index, m.Length)).Where(t => t.Normalized.Length > 0).ToList();
    public static List<SearchToken> SourceTokens(VerseEntry verse)
    {
        var tokens = Tokenize(verse.Text);
        foreach (var word in verse.Words)
        {
            var segments = word.Segments.Count > 0 ? word.Segments : [new OriginalSegment(word.Text, 0)];
            foreach (var segment in segments)
            foreach (var token in Tokenize(segment.Text)) tokens.Add(token with { Start = word.Start + segment.Start + token.Start });
        }
        return tokens.DistinctBy(t => (t.Start, t.Length)).OrderBy(t => t.Start).ToList();
    }
    public static string[] QueryTokens(string query) => Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
    public static bool HasHebrew(string value) => value.Any(c => c is >= '\u05D0' and <= '\u05EA' or >= '\uFB1D' and <= '\uFB4F');
    public static bool WordMatches(string word, string query, SearchMode mode) => mode switch
    {
        SearchMode.Prefix => word.StartsWith(query, StringComparison.Ordinal),
        SearchMode.Contains => word.Contains(query, StringComparison.Ordinal),
        _ => word == query
    };
    public static bool IsNear(string a, string b)
    {
        if (a.Length < 4 || Math.Abs(a.Length - b.Length) > 1 || HasHebrew(a) != HasHebrew(b)) return false;
        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (a.Length >= b.Length) i++;
            if (b.Length >= a.Length) j++;
        }
        return edits + (a.Length - i) + (b.Length - j) <= 1;
    }
}

public sealed record SearchHit(VerseEntry Verse, string MatchKind, int Rank, List<SearchToken> Highlights)
{
    public string Heading => $"{Verse.Reference}   ·   {Verse.Edition}";
    public string Excerpt => Verse.Text.Length > 280 ? Verse.Text[..280] + "…" : Verse.Text;
}
public sealed record SpellingOption(string Text, int Count, bool Approximate = false);
public sealed record SearchResponse(List<SearchHit> Hits, int Total, List<SpellingOption> Spellings, List<string> Warnings);
internal sealed record IndexedVerse(VerseEntry Verse, string Normalized);
internal sealed record EditionIndex(List<IndexedVerse> Verses, Dictionary<string, Dictionary<string, int>> Words);

public sealed class SearchEngine(BibleLibrary library)
{
    private readonly ConcurrentDictionary<string, Lazy<Task<EditionIndex>>> indexes = new(StringComparer.OrdinalIgnoreCase);
    private Task<EditionIndex> Index(string id) => indexes.GetOrAdd(id, key => new(async () =>
    {
        var edition = await library.LoadAsync(key);
        return await Task.Run(() =>
        {
            var verses = new List<IndexedVerse>();
            var words = new Dictionary<string, Dictionary<string, int>>();
            foreach (var entry in edition.Entries)
            {
                var tokens = SearchText.SourceTokens(entry);
                verses.Add(new(entry, " " + string.Join(" ", tokens.Select(t => t.Normalized)) + " "));
                foreach (var token in tokens)
                {
                    if (!words.TryGetValue(token.Normalized, out var forms)) words[token.Normalized] = forms = new(StringComparer.Ordinal);
                    forms[token.Original] = forms.GetValueOrDefault(token.Original) + 1;
                }
            }
            return new EditionIndex(verses, words);
        });
    })).Value;

    public async Task<SearchResponse> SearchAsync(string query, IEnumerable<string> editionIds, SearchMode mode, CancellationToken ct)
    {
        string[] terms = SearchText.QueryTokens(query);
        if (terms.Length == 0) return new([], 0, [], []);
        if (query.Length > 200 || terms.Length > 12) throw new ArgumentException("Use até 200 caracteres e 12 palavras por pesquisa.");
        if (mode is SearchMode.Strong or SearchMode.Lemma) return await SearchLexical(query, editionIds, mode, ct);
        var hits = new List<SearchHit>();
        var spellings = new Dictionary<string, int>(StringComparer.Ordinal);
        var near = new Dictionary<string, int>(StringComparer.Ordinal);
        var warnings = new List<string>();
        var exactTerms = SearchText.Tokenize(query).Select(t => t.Original.Normalize(NormalizationForm.FormC)).ToArray();
        foreach (var id in editionIds)
        {
            ct.ThrowIfCancellationRequested();
            EditionIndex index;
            try { index = await Index(id).WaitAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { warnings.Add($"{id}: {ex.Message}"); continue; }
            var edition = await library.LoadAsync(id);
            warnings.AddRange(edition.Warnings.Select(w => id + ": " + w));
            await Task.Run(() =>
            {
                foreach (var row in index.Verses)
                {
                    ct.ThrowIfCancellationRequested();
                    bool candidate = terms.All(q => mode switch
                    {
                        SearchMode.Prefix => row.Normalized.Contains(" " + q, StringComparison.Ordinal),
                        SearchMode.Contains => row.Normalized.Contains(q, StringComparison.Ordinal),
                        _ => row.Normalized.Contains(" " + q + " ", StringComparison.Ordinal)
                    });
                    if (!candidate) continue;
                    var tokens = mode == SearchMode.Exact ? SearchText.Tokenize(row.Verse.Text) : SearchText.SourceTokens(row.Verse);
                    if (mode == SearchMode.Exact && !exactTerms.All(q => tokens.Any(t => string.Equals(t.Original.Normalize(NormalizationForm.FormC), q, StringComparison.OrdinalIgnoreCase)))) continue;
                    var highlights = tokens.Where(t => mode == SearchMode.Exact
                        ? exactTerms.Any(q => string.Equals(t.Original.Normalize(NormalizationForm.FormC), q, StringComparison.OrdinalIgnoreCase))
                        : terms.Any(q => SearchText.WordMatches(t.Normalized, q, mode))).ToList();
                    bool exact = exactTerms.All(q => tokens.Any(t => string.Equals(t.Original.Normalize(NormalizationForm.FormC), q, StringComparison.OrdinalIgnoreCase)));
                    hits.Add(new(row.Verse, exact ? "Grafia exata" : mode == SearchMode.Prefix ? "Prefixo" : mode == SearchMode.Contains ? "Trecho de palavra" : "Sem sinais / acentos", exact ? 0 : 1, highlights));
                    foreach (var token in highlights) spellings[token.Original] = spellings.GetValueOrDefault(token.Original) + 1;
                }
                if (terms.Length == 1)
                    foreach (var (word, forms) in index.Words)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (word != terms[0] && SearchText.IsNear(word, terms[0]))
                            foreach (var form in forms) near[form.Key] = near.GetValueOrDefault(form.Key) + form.Value;
                    }
            }, ct);
        }
        var ranked = hits.OrderBy(h => h.Rank).ThenBy(h => h.Verse.Edition).ThenBy(h => h.Verse.Book).ThenBy(h => h.Verse.Chapter).ThenBy(h => h.Verse.EntryIndex).ToList();
        var options = spellings.OrderByDescending(x => x.Value).Take(20).Select(x => new SpellingOption(x.Key, x.Value)).ToList();
        if (ranked.Count == 0) options.AddRange(near.OrderByDescending(x => x.Value).Take(8).Select(x => new SpellingOption(x.Key, x.Value, true)));
        return new(ranked, ranked.Count, options, warnings.Distinct().ToList());
    }

    private async Task<SearchResponse> SearchLexical(string query, IEnumerable<string> ids, SearchMode mode, CancellationToken ct)
    {
        var hits = new List<SearchHit>(); var warnings = new List<string>();
        var codes = mode == SearchMode.Strong ? OriginalResource.ParseStrong(query.Trim()) : [];
        if (mode == SearchMode.Strong && (codes.Count != 1 || !Regex.IsMatch(query.Trim(), @"(?i)^[HG]?\d{1,5}[a-z]?$"))) throw new ArgumentException("Digite um código Strong, por exemplo H7225 ou H1254a.");
        string lemma = SearchText.Normalize(query);
        foreach (string id in ids)
        {
            ct.ThrowIfCancellationRequested();
            BibleEdition edition;
            try { edition = await library.LoadAsync(id).WaitAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { warnings.Add(id + ": " + ex.Message); continue; }
            if (edition.Resource == null || !edition.Entries.Any(e => e.Words.Any(w => mode == SearchMode.Strong ? w.Strongs.Count > 0 : w.Lemma.Length > 0))) { warnings.Add(id + ": esta fonte não fornece " + (mode == SearchMode.Strong ? "códigos Strong." : "lemas.")); continue; }
            await Task.Run(() =>
            {
                foreach (var verse in edition.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var matched = verse.Words.Where(w => mode == SearchMode.Strong
                        ? w.Strongs.Any(c => Regex.IsMatch(codes[0], @"[a-z]$") ? c.Equals(codes[0], StringComparison.OrdinalIgnoreCase) : OriginalResource.LexiconKey(c) == OriginalResource.LexiconKey(codes[0]))
                        : w.Lemma.Length > 0 && SearchText.Normalize(w.Lemma) == lemma).ToList();
                    if (matched.Count > 0) hits.Add(new(verse, mode == SearchMode.Strong ? "Strong " + codes[0] : "Lema da fonte", 0,
                        matched.Select(w => new SearchToken(w.Text, SearchText.Normalize(w.Text), w.Start, w.Text.Length)).ToList()));
                }
            }, ct);
        }
        return new(hits.OrderBy(h => h.Verse.Edition).ThenBy(h => h.Verse.Book).ThenBy(h => h.Verse.Chapter).ThenBy(h => h.Verse.EntryIndex).ToList(), hits.Count, [], warnings);
    }
}
