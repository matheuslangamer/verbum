using System.Text.RegularExpressions;

namespace Verbum;

public static class WordGlosses
{
    private static bool Available(string value) => value.Trim() is not ("" or "—" or "-" or "–");
    public static string Summary(IEnumerable<UserParsing> matches, OriginalWord word, OriginalResource? resource, IEnumerable<LexicalResource> lexicons)
    {
        var rows = matches.ToList();
        var portuguese = rows.Select(m => m.Portuguese.Trim()).Where(Available).Distinct().Take(2).ToList();
        if (portuguese.Count > 0) return string.Join(" / ", portuguese) + " (glossa PT provisória)";
        var english = rows.Select(m => m.English.Trim()).Where(Available).Distinct().Take(2).ToList();
        if (english.Count > 0) return Compact(string.Join(" / ", english)) + " (inglês · arquivo pessoal)";
        var codes = word.Strongs.Select(OriginalResource.LexiconKey).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var definitions = codes.Select(code => resource?.Lexicon.GetValueOrDefault(code)?.Definition ?? "").Where(Available).Distinct().Take(2).ToList();
        if (definitions.Count > 0) return Compact(string.Join(" / ", definitions)) + " (inglês · Strong; sentidos do léxico)";
        var bdb = lexicons.FirstOrDefault(l => l.Id == "BDB")?.Entries
            .Where(e => e.Codes.Any(c => codes.Contains(OriginalResource.LexiconKey(c))))
            .Select(e => e.Definition).Where(Available).Distinct().Take(2).ToList() ?? [];
        return bdb.Count > 0 ? Compact(string.Join(" / ", bdb)) + " (inglês · BDB; sentidos do léxico)" : "";
    }
    private static string Compact(string value)
    {
        string text = Regex.Replace(value, @"\s+", " ").Trim();
        return text.Length > 320 ? text[..320] + "…" : text;
    }
}
