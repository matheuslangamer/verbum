namespace Verbum;

public sealed record GlossParallel(PortugueseGloss Gloss, List<SearchHit> Hits);
public sealed record GlossParallelReport(List<GlossParallel> Items, int Compared, int Missing);
public static class GlossParallels
{
    public static List<SearchToken> Highlights(string text, string phrase)
    {
        var words = SearchText.Tokenize(text); var wanted = SearchText.Tokenize(phrase).Select(t => t.Normalized).ToArray(); var found = new List<SearchToken>();
        if (wanted.Length == 0) return found;
        for (int i = 0; i + wanted.Length <= words.Count; i++)
            if (wanted.Select((key, offset) => words[i + offset].Normalized == key).All(matches => matches)) found.AddRange(words.Skip(i).Take(wanted.Length));
        return found.DistinctBy(t => t.Start).ToList();
    }
    public static bool ContainsPhrase(string text, string phrase)
    {
        string key = SearchText.Normalize(phrase);
        return key.Length > 0 && (" " + SearchText.Normalize(text) + " ").Contains(" " + key + " ", StringComparison.Ordinal);
    }
    public static GlossParallelReport Compare(List<SearchHit> hits, BibleEdition original, BibleEdition translated, List<PortugueseGloss> glosses)
    {
        var rows = hits.DistinctBy(h => (h.Verse.Edition, h.Verse.Book, h.Verse.Chapter, h.Verse.EntryIndex))
            .Select(h => (Hit: h, Target: BibleLibrary.IsSuperscriptionHit(h) && translated.Resource?.ReferenceSystem != "MT" ? null : BibleLibrary.ResolveParallel(original, h.Verse, translated))).ToList();
        return new(glosses.Select(gloss => new GlossParallel(gloss, rows.Where(r => r.Target != null && ContainsPhrase(r.Target.Text, gloss.Text)).Select(r => r.Hit).ToList())).ToList(), rows.Count(r => r.Target != null), rows.Count(r => r.Target == null));
    }
    public static List<string> MatchedCodes(List<SearchHit> hits)
        => hits.SelectMany(hit => hit.Verse.Words.Where(w => hit.Highlights.Any(h => h.Start < w.Start + w.Text.Length && h.Start + h.Length > w.Start)).SelectMany(w => w.Strongs)).Select(OriginalResource.LexiconKey).Distinct().ToList();
}
