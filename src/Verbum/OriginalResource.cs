using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Verbum;

public sealed class OriginalWord
{
    public List<OriginalSegment> Segments { get; set; } = [];
    public string Text { get; set; } = "";
    public string Suffix { get; set; } = " ";
    public string Lemma { get; set; } = "";
    public List<string> Strongs { get; set; } = [];
    public string Morphology { get; set; } = "";
    public string MorphologySource { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Reading { get; set; } = "";
    public int Start { get; set; }
    public int Length => Text.Length;
    // Runtime-only occurrence links to the OSHB graphic word (never saved in sessions).
    [System.Text.Json.Serialization.JsonIgnore] public OriginalWord? Whole { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int WholeSegment { get; set; } = -1;
    [System.Text.Json.Serialization.JsonIgnore] public int WholeSegmentCount { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public int ConsonantStart { get; set; } = -1;
}
public sealed record OriginalSegment(string Text, int Start);

public sealed class ResourceVerse
{
    public bool IsSuperscription { get; set; }
    public int SuperscriptionPrefixLength { get; set; }
    public List<SourceReference> KjvReferences { get; set; } = [];
    public int Book { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string Text { get; set; } = "";
    public List<OriginalWord> Words { get; set; } = [];
    public List<string> Apparatus { get; set; } = [];
}
public sealed record SourceReference(int Book, int Chapter, int Verse, string Part = "");

public sealed class LexiconEntry
{
    public string Code { get; set; } = "";
    public string Lemma { get; set; } = "";
    public string Transliteration { get; set; } = "";
    public string Definition { get; set; } = "";
    public string Derivation { get; set; } = "";
}

public sealed class OriginalResource
{
    public string Schema { get; set; } = "";
    public string Title { get; set; } = "";
    public string Language { get; set; } = "";
    public string ReferenceSystem { get; set; } = "";
    public string Attribution { get; set; } = "";
    public string License { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Revision { get; set; } = "";
    public List<ResourceVerse> Verses { get; set; } = [];
    public Dictionary<string, LexiconEntry> Lexicon { get; set; } = [];

    public static List<string> ParseStrong(string value, char language = 'H')
    {
        return Regex.Matches(value, @"(?i)([HG])?(\d{1,5})([a-z])?").Select(m =>
            (m.Groups[1].Success ? m.Groups[1].Value.ToUpperInvariant() : language.ToString()) + int.Parse(m.Groups[2].Value).ToString(CultureInfo.InvariantCulture) + m.Groups[3].Value.ToLowerInvariant()).Distinct().ToList();
    }
    public static string LexiconKey(string code) => Regex.Replace(code.ToUpperInvariant(), @"(?<=[0-9])[A-Z]$", "");
    public static string HebrewTransliteration(string value)
    {
        // Consonantal aid only: does not reconstruct vowels or a scholarly vocalized transliteration.
        var map = new Dictionary<char, string> { ['א']="ʾ", ['ב']="b", ['ג']="g", ['ד']="d", ['ה']="h", ['ו']="w", ['ז']="z", ['ח']="ḥ", ['ט']="ṭ", ['י']="y", ['כ']="k", ['ך']="k", ['ל']="l", ['מ']="m", ['ם']="m", ['נ']="n", ['ן']="n", ['ס']="s", ['ע']="ʿ", ['פ']="p", ['ף']="p", ['צ']="ṣ", ['ץ']="ṣ", ['ק']="q", ['ר']="r", ['ש']="š/ś", ['ת']="t" };
        var result = new StringBuilder();
        foreach (char c in SearchText.Normalize(value)) result.Append(map.GetValueOrDefault(c, c.ToString()));
        return result.ToString();
    }
    public static string MorphologySummary(string code)
    {
        return OshbPortuguese.Describe(code);
    }
}
