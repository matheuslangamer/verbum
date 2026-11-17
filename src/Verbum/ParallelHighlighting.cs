namespace Verbum;

public static class ParallelHighlighting
{
    private static string EnglishHead(string text) => SearchText.Tokenize(text).Select(t => t.Normalized).FirstOrDefault(t => !new[] { "and", "he", "she", "it", "they", "i", "we", "you", "the", "a", "an", "to", "has", "have", "had", "will", "was", "were", "is", "be", "his", "their", "them" }.Contains(t)) ?? "";
    public static List<PortugueseGloss> GlossesForHit(SearchHit hit, PortugueseParsing parsing)
    {
        var glosses = parsing.Glosses(GlossParallels.MatchedCodes([hit]));
        var words = hit.Verse.Words.Where(w => hit.Highlights.Any(h => h.Start < w.Start + w.Text.Length && h.Start + h.Length > w.Start)).ToList();
        var graphics = SearchText.Tokenize(hit.Verse.Text); var heads = new HashSet<string>();
        foreach (var word in words)
        {
            var form = graphics.FirstOrDefault(t => t.Start <= word.Start && t.Start + t.Length >= word.Start + word.Text.Length);
            var lookup = new OriginalWord { Text = form?.Original ?? word.Text, Strongs = word.Strongs };
            foreach (var row in parsing.Find(hit.Verse, lookup)) { string head = EnglishHead(row.English); if (head.Length > 0) heads.Add(head); }
        }
        var contextual = glosses.Where(g => g.English.Split(" / ").Any(english => heads.Contains(EnglishHead(english)))).ToList();
        return contextual.Count > 0 ? contextual : glosses;
    }
    public static List<SearchToken> SuggestForHit(string text, SearchHit hit, PortugueseParsing parsing)
    {
        var contextual = Suggest(text, GlossesForHit(hit, parsing));
        return contextual.Count > 0 ? contextual : Suggest(text, parsing.Glosses(GlossParallels.MatchedCodes([hit])));
    }
    private static readonly HashSet<string> Leading = ["e", "o", "a", "os", "as", "um", "uma", "de", "do", "da", "para", "ele", "ela", "eu"];
    private static readonly HashSet<string> ArEndings = ["ar", "o", "as", "a", "amos", "ais", "am", "ava", "avas", "avamos", "avam", "ei", "aste", "ou", "astes", "aram", "ara", "aras", "arei", "aremos", "arao", "aria", "ariam", "ado", "ada", "ados", "adas", "ando"];
    private static readonly HashSet<string> ErEndings = ["er", "o", "es", "e", "emos", "eis", "em", "ia", "iam", "i", "este", "eu", "eram", "era", "erei", "erao", "eria", "eriam", "ido", "ida", "idos", "idas", "endo"];
    private static readonly HashSet<string> IrEndings = ["ir", "o", "es", "e", "imos", "is", "em", "ia", "iam", "i", "iste", "iu", "iram", "ira", "irei", "irao", "iria", "iriam", "ido", "ida", "idos", "idas", "indo"];
    public static List<SearchToken> Suggest(string portugueseText, IEnumerable<PortugueseGloss> glosses)
    {
        var tokens = SearchText.Tokenize(portugueseText); var highlights = new List<SearchToken>();
        foreach (var gloss in glosses)
        foreach (var alternative in gloss.Text.Split('/'))
        {
            var words = SearchText.Tokenize(alternative); int first = 0;
            while (first < words.Count && Leading.Contains(words[first].Normalized)) first++;
            var meaningful = words.Skip(first).ToList();
            if (meaningful.Count == 0) continue;
            string phrase = string.Join(" ", meaningful.Select(t => t.Original));
            bool infinitivePor = gloss.Verbal && meaningful.Count == 1 && meaningful[0].Normalized == "por" && meaningful[0].Original.Contains('ô');
            if (infinitivePor) highlights.AddRange(tokens.Where(t => t.Normalized == "por" && t.Original.Contains('ô')));
            else highlights.AddRange(GlossParallels.Highlights(portugueseText, phrase));
            if (!gloss.Verbal || meaningful.Count != 1) continue;
            string verb = meaningful[0].Normalized;
            foreach (var token in tokens)
            {
                if (verb == "por" && meaningful[0].Original.Contains('ô'))
                {
                    if (new[] { "ponho", "poe", "poem", "pus", "puseste", "pos", "pusemos", "puseram", "pusera", "posto", "posta", "postos", "postas", "ponha", "ponham", "pondo" }.Contains(token.Normalized)) highlights.Add(token);
                    continue;
                }
                var endings = verb.EndsWith("ar") ? ArEndings : verb.EndsWith("er") ? ErEndings : verb.EndsWith("ir") ? IrEndings : null;
                if (endings == null || verb.Length < 5) continue;
                string stem = verb[..^2];
                if (token.Normalized.StartsWith(stem, StringComparison.Ordinal) && endings.Contains(token.Normalized[stem.Length..])) highlights.Add(token);
            }
        }
        return highlights.DistinctBy(t => t.Start).OrderBy(t => t.Start).ToList();
    }
}
