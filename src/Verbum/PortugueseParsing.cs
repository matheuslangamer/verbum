using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Verbum;

public sealed record UserParsing(string Form, string Portuguese, string Parsing, string Code, string Strong, string English, string Reference);
public sealed record PortugueseGloss(string Text, string English, List<string> Codes, bool Verbal = false);

public sealed class PortugueseParsing
{
    private readonly Dictionary<string, List<UserParsing>> entries = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<PortugueseGloss>> glossCache = [];
    public int Count { get; private set; }
    public static PortugueseParsing Load(string path)
    {
        var catalog = new PortugueseParsing();
        if (!File.Exists(path)) return catalog;
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains(" (posição ")) continue;
            var columns = Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(c => c.Trim().Replace(@"\|", "|").Trim('`')).ToArray();
            if (columns.Length != 7 || !Regex.IsMatch(columns[4], @"^H\d+$")) continue;
            var reference = Regex.Match(columns[6], @"^(.*?) (\d+) \(posição \d+\)$");
            if (!reference.Success) continue;
            string key = SearchText.Normalize(reference.Groups[1].Value) + ":" + reference.Groups[2].Value + ":" + columns[4] + ":" + FormKey(columns[0]);
            if (!catalog.entries.TryGetValue(key, out var list)) catalog.entries[key] = list = [];
            var row = new UserParsing(columns[0], columns[1], columns[2], columns[3], columns[4], columns[5], columns[6]);
            if (!list.Any(r => r.Code == row.Code && r.Portuguese == row.Portuguese && r.English == row.English)) list.Add(row);
            catalog.Count++;
        }
        return catalog;
    }
    // Keep vowels and shin/sin dots; omit cantillation and punctuation only. No consonantal fuzzy match.
    internal static string FormKey(string text)
    {
        var b = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
            if (c is >= '\u0591' and <= '\u05AF' or '\u05BD' or '\u034F' || char.IsWhiteSpace(c)) continue;
            else if (char.IsLetter(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark) b.Append(c);
        return b.ToString().Normalize(NormalizationForm.FormC);
    }
    public List<UserParsing> Find(VerseEntry verse, OriginalWord word)
    {
        var found = new List<UserParsing>();
        var contexts = verse.KjvReferences.Count > 0 ? verse.KjvReferences.Select(r => (r.Book, r.Chapter)).Distinct().ToList() : [(verse.Book, verse.Chapter)];
        foreach (var context in contexts)
        foreach (var strong in word.Strongs.Select(OriginalResource.LexiconKey).Distinct())
        {
            string key = SearchText.Normalize(BibleLibrary.CanonicalNames[context.Book]) + ":" + context.Chapter + ":" + strong + ":" + FormKey(word.Text);
            if (entries.TryGetValue(key, out var matches)) found.AddRange(matches);
        }
        return found.DistinctBy(r => (r.Parsing, r.Code, r.Portuguese, r.English)).ToList();
    }
    public List<PortugueseGloss> Glosses(IEnumerable<string> strongCodes)
    {
        var codes = strongCodes.Select(OriginalResource.LexiconKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return glossCache.GetOrAdd(string.Join("|", codes.Order()), _ => entries.Values.SelectMany(rows => rows).Where(r => codes.Contains(r.Strong) && r.Portuguese.Trim() is not ("" or "—" or "-"))
            .GroupBy(r => r.Portuguese.Trim(), StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Take(32).Select(g => new PortugueseGloss(g.Key, string.Join(" / ", g.Select(r => r.English).Distinct().Take(3)), g.Select(r => r.Strong).Distinct().ToList(), g.Any(r => r.Parsing.Contains("verbo", StringComparison.OrdinalIgnoreCase)))).ToList());
    }
    /// <summary>Portuguese gloss words per Strong code (without homonym letter), for the alignment builder.</summary>
    public Dictionary<string, HashSet<string>> GlossWords()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in entries.Values.SelectMany(rows => rows))
        {
            if (row.Portuguese.Trim() is "" or "—" or "-") continue;
            if (!map.TryGetValue(row.Strong, out var words)) map[row.Strong] = words = [];
            foreach (var token in SearchText.Tokenize(row.Portuguese)) words.Add(token.Normalized);
        }
        return map;
    }
    public string Describe(VerseEntry verse, OriginalWord word)
    {
        var matches = Find(verse, word);
        if (matches.Count == 0) return "Arquivo pessoal: sem análise compatível para esta forma, Strong e capítulo. Cobertura parcial; numeração pode divergir entre fontes.";
        return "Arquivo pessoal · sugestões por forma com vogais, Strong e capítulo; sem vínculo confirmado ao versículo.\n" +
            string.Join("\n\n", matches.Take(5).Select(m => m.Form + " · " + m.Parsing + "\nCódigo: " + m.Code + " · " + m.Strong + "\nGlossa PT provisória: " + m.Portuguese + "\nInglês original: " + m.English + "\nReferência do arquivo: " + m.Reference)) +
            (matches.Count > 5 ? $"\nMais {matches.Count - 5} análises compatíveis no arquivo." : "") +
            "\nAs glossas portuguesas são automáticas e provisórias; posição no capítulo não equivale a versículo.";
    }
}

public static class OshbPortuguese
{
    private static string Map(char c, string keys, params string[] labels) { int i = keys.IndexOf(c); return i >= 0 ? labels[i] : "código " + c; }
    private static string Features(string value, bool person, bool state)
    {
        var labels = new List<string>(); int i = 0;
        if (person && i < value.Length) labels.Add(Map(value[i++], "123x", "1ª pessoa", "2ª pessoa", "3ª pessoa", "pessoa não especificada"));
        if (i < value.Length) labels.Add(Map(value[i++], "mfbcx", "masculino", "feminino", "gênero comum", "gênero comum", "gênero não especificado"));
        if (i < value.Length) labels.Add(Map(value[i++], "spdx", "singular", "plural", "dual", "número não especificado"));
        if (state && i < value.Length) labels.Add(Map(value[i++], "acdx", "estado absoluto", "estado construto", "estado determinado", "estado não especificado"));
        if (i < value.Length) labels.Add("restante do código: " + value[i..]);
        return string.Join(", ", labels);
    }
    public static string Describe(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Parsing da ocorrência não fornecido por esta fonte.";
        bool aramaic = code[0] == 'A';
        string payload = code[0] is 'H' or 'A' ? code[1..] : code;
        return (aramaic ? "Aramaico" : "Hebraico") + " · " + string.Join(" + ", payload.Split('/').Select(p => Segment(p, aramaic))) + "\nOSHB: " + code;
    }
    internal static string DescribeSegment(string part, bool aramaic) => Segment(part, aramaic);
    private static string Segment(string p, bool aramaic)
    {
        if (p.Length == 0) return "segmento vazio";
        if (p == "C") return "conjunção";
        if (p == "D") return "advérbio";
        if (p[0] == 'R') return p == "Rd" ? "preposição + artigo definido" : p == "R" ? "preposição" : "preposição (" + p + ")";
        if (p.Length < 2) return "código " + p;
        string tail = p.Length > 2 ? p[2..] : "", head;
        switch (p[0])
        {
            case 'N': head = "substantivo, " + Map(p[1], "cgp", "comum", "gentílico", "nome próprio"); break;
            case 'A': head = Map(p[1], "acgo", "adjetivo", "numeral cardinal", "adjetivo gentílico", "numeral ordinal"); break;
            case 'P': return "pronome, " + Map(p[1], "dfipr", "demonstrativo", "indefinido", "interrogativo", "pessoal", "relativo") + (tail.Length > 0 ? ", " + Features(tail, true, false) : "");
            case 'S': return "sufixo, " + Map(p[1], "dhnp", "he direcional", "he paragógico", "nun paragógico", "pronominal") + (tail.Length > 0 ? ", " + Features(tail, true, false) : "");
            case 'T': return "partícula, " + Map(p[1], "adeijmnor", "afirmação", "artigo definido", "exortação", "interrogativa", "interjeição", "demonstrativa", "negação", "marcador de objeto direto", "relativa");
            case 'V':
                if (p.Length < 3) return "verbo (" + p + ")";
                string stem = aramaic
                    ? Map(p[1], "qQupPMahseHitvwozrfbcmlLOG", "Peal", "Peil", "Hitpeel", "Pael", "Itpaal", "Hitpaal", "Aphel", "Haphel", "Saphel", "Shaphel", "Hophal", "Itpeel", "Hishtaphel", "Ishtaphel", "Hitaphel", "Polel", "Itpoel", "Hitpolel", "Hitpalpel", "Hephal", "Tiphel", "Poel", "Palpel", "Itpalpel", "Itpolel", "Ittaphal")
                    : Map(p[1], "qNpPhHtoOrmMkKQlLfDjiucvwyz", "Qal", "Nifal", "Piel", "Pual", "Hifil", "Hofal", "Hitpael", "Polel", "Polal", "Hitpolel", "Poel", "Poal", "Palel", "Pulal", "Qal passivo", "Pilpel", "Polpal", "Hitpalpel", "Nitpael", "Pealal", "Pilel", "Hotpaal", "Tifil", "Hishtafel", "Nitpalel", "Nitpoel", "Hitpoel");
                head = "verbo, " + stem + ", " + Map(p[2], "pq iwhjvr sac".Replace(" ", ""), "perfeito (qatal)", "perfeito sequencial (weqatal)", "imperfeito (yiqtol)", "imperfeito sequencial (wayyiqtol)", "coortativo", "jussivo", "imperativo", "particípio ativo", "particípio passivo", "infinitivo absoluto", "infinitivo construto");
                string ending = p.Length > 3 ? p[3..] : "";
                if (p[2] is 'a' or 'c') return head + (ending.Length > 0 ? " (código complementar: " + ending + ")" : "");
                return head + (ending.Length > 0 ? ", " + Features(ending, p[2] is not ('r' or 's'), p[2] is 'r' or 's') : "");
            default: return "código " + p;
        }
        return head + (tail.Length > 0 ? ", " + Features(tail, false, true) : "");
    }
}
