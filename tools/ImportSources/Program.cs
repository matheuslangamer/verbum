using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

string root = Path.GetFullPath(args.FirstOrDefault() ?? ".");
string sources = Path.Combine(root, "biblioteca", "fontes"), output = Path.Combine(root, "biblioteca", "biblias");
var revisions = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(sources, "revisions.json")))!;
string oshb = Path.Combine(sources, "OSHB", "morphhb-" + revisions["oshb"]);
string sbl = Path.Combine(sources, "SBLGNT", "SBLGNT-" + revisions["sblgnt"]);
var lexicon = new Dictionary<string, Lex>();
var lexDoc = ReadXml(Path.Combine(sources, "Strong", "StrongHebrewG.xml"));
foreach (var entry in lexDoc.Descendants().Where(e => e.Name.LocalName == "div" && (string?)e.Attribute("type") == "entry"))
{
    var word = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "w");
    if (word == null) continue;
    string id = (string?)word.Attribute("ID") ?? "";
    string Notes(string kind) => string.Join(" ", entry.Elements().Where(e => e.Name.LocalName == "note" && (string?)e.Attribute("type") == kind).Select(e => Regex.Replace(e.Value, @"\s+", " ").Trim()));
    lexicon[id] = new() { Code = id, Lemma = (string?)word.Attribute("lemma") ?? word.Value, Transliteration = (string?)word.Attribute("xlit") ?? "", Definition = Notes("explanation") + "\nKJV: " + Notes("translation"), Derivation = Notes("exegesis") };
}
var bhs = new Resource { Title = "BHS-A · Strong", Language = "he", ReferenceSystem = "MT", SourceUrl = "https://github.com/eliranwong/BHS-Strong-no", Revision = revisions["bhs"], License = "BHSA/ETCBC e mapeamento: CC BY-NC 4.0. Léxico Strong 1894: domínio público.", Attribution = "Texto BHS/BHSA © Deutsche Bibelgesellschaft 1977/1997; dados ETCBC; mapeamento Strong estendido por Eliran Wong. Uso pessoal não comercial. Léxico de James Strong (1894), edição digital David Troidl / David Instone-Brewer, Open Scriptures. Referências MT nativas recuperadas do corpus ETCBC BHSA c; mapa KJV mantido apenas como metadado de origem.", Lexicon = lexicon };
var bhsVerses = new SortedDictionary<(int, int, int), Verse>();
string nativeDirectory = Path.Combine(sources, "BHSA-4c");
var nativeRefs = NativeReferences.Load(nativeDirectory);
var nativeWords = NativeReferences.Feature(Path.Combine(nativeDirectory, "g_word_utf8.tf"));
var nativeQere = NativeReferences.Feature(Path.Combine(nativeDirectory, "qere_utf8.tf"));
int MatchNative(string text, int slot)
{
    string wanted = NativeReferences.Normalize(text); var forms = new List<string> { "" };
    for (int length = 1; length <= 4 && nativeWords.ContainsKey(slot + length - 1); length++)
    {
        int current = slot + length - 1;
        var choices = new[] { nativeWords[current], nativeQere.GetValueOrDefault(current) ?? nativeWords[current] }.Select(NativeReferences.Normalize).Distinct();
        forms = forms.SelectMany(prefix => choices.Select(choice => prefix + choice)).Distinct().ToList();
        if (forms.Contains(wanted)) return length;
        if (forms.All(f => f.Length > wanted.Length)) break;
    }
    return 0;
}
int nativeSlot = 1; var skippedNativeSlots = new List<int>();
foreach (string line in File.ReadLines(Path.Combine(sources, "BHS-Strong-no", "BHS-with-Strong-no-extended.csv")).Skip(1))
{
    var cols = line.Split('\t'); if (cols.Length < 4) throw new InvalidDataException("Linha BHS inválida");
    var reference = Regex.Matches(cols[1], @"\d+").Select(m => int.Parse(m.Value)).ToArray();
    if (reference.Length != 4) throw new InvalidDataException("Referência BHS inválida: " + cols[1]);
    var parts = Regex.Matches(cols[2], @"<H>(.*?)<h>").Select(m => m.Groups[1].Value).ToArray();
    if (parts.Length != 2) throw new InvalidDataException("Segmentação BHS inesperada: " + cols[0]);
    string plain = parts[0], suffix = parts[1];
    int consumed = MatchNative(plain, nativeSlot);
    if (consumed == 0)
    {
        int matched = Enumerable.Range(nativeSlot + 1, 6).FirstOrDefault(i => MatchNative(plain, i) > 0);
        if (matched == 0) throw new InvalidDataException($"BHS/native mismatch at BHSsort {cols[0]}, native {nativeSlot}: {plain} / {nativeWords[nativeSlot]}");
        skippedNativeSlots.AddRange(Enumerable.Range(nativeSlot, matched - nativeSlot)); nativeSlot = matched;
        consumed = MatchNative(plain, nativeSlot);
    }
    var native = nativeRefs[nativeSlot];
    if (nativeRefs[nativeSlot + consumed - 1] != native) throw new InvalidDataException("Segmento BHS atravessa referências nativas: " + cols[0]);
    var key = (reference[1] - 1, native.Chapter, native.Verse);
    if (!bhsVerses.TryGetValue(key, out var verse)) bhsVerses[key] = verse = new() { Book = key.Item1, Chapter = key.Item2, VerseNumber = key.Item3 };
    var kjv = new Ref(reference[1] - 1, reference[2], reference[3]);
    if (!verse.KjvReferences.Contains(kjv)) verse.KjvReferences.Add(kjv);
    int firstNativeSlot = nativeSlot; nativeSlot += consumed;
    var codes = StrongCodes(cols[3]);
    verse.Words.Add(new() { Text = plain, Suffix = suffix, Strongs = codes, Lemma = codes.Select(c => lexicon.GetValueOrDefault(LexKey(c))?.Lemma).FirstOrDefault(l => l != null) ?? "", SourceId = "BHSsort:" + cols[0], Reading = $"BHSA c · slots {firstNativeSlot}–{nativeSlot - 1}; referência KJV da fonte: {reference[2]}:{reference[3]}", Start = verse.Text.Length });
    verse.Text += plain + suffix;
}
bhs.Verses = bhsVerses.Values.ToList();
if (nativeSlot != nativeWords.Keys.Max() + 1) throw new InvalidDataException("Alinhamento nativo incompleto.");
File.WriteAllText(Path.Combine(root, "docs", "bhs-native-alignment.json"), JsonSerializer.Serialize(new { nativeRevision = File.ReadAllText(Path.Combine(nativeDirectory, "revision.txt")).Trim(), matchedSourceWords = bhs.Verses.Sum(v => v.Words.Count), lastNativeSlot = nativeSlot - 1, skippedNativeSlots }));

string[] osisIds = "Gen Exod Lev Num Deut Josh Judg Ruth 1Sam 2Sam 1Kgs 2Kgs 1Chr 2Chr Ezra Neh Esth Job Ps Prov Eccl Song Isa Jer Lam Ezek Dan Hos Joel Amos Obad Jonah Mic Nah Hab Zeph Hag Zech Mal Matt Mark Luke John Acts Rom 1Cor 2Cor Gal Eph Phil Col 1Thess 2Thess 1Tim 2Tim Titus Phlm Heb Jas 1Pet 2Pet 1John 2John 3John Jude Rev".Split(' ');
var wlc = new Resource { Title = "WLC · OSHB · Morfologia", Language = "he", ReferenceSystem = "MT", SourceUrl = "https://github.com/openscriptures/morphhb", Revision = revisions["oshb"], License = "Texto WLC: domínio público. Anotações OSHB: CC BY 4.0. Léxico Strong 1894: domínio público.", Attribution = "Original work of the Open Scriptures Hebrew Bible available at https://github.com/openscriptures/morphhb. Texto Westminster Leningrad Codex (WLC). Anotações de lema/morfologia © Open Scriptures Hebrew Bible Project, CC BY 4.0. Léxico Strong: James Strong (1894), edição digital Open Scriptures. Versificação original MT; não remapeada às traduções.", Lexicon = lexicon };
for (int book = 0; book < 39; book++)
{
    var doc = ReadXml(Path.Combine(oshb, "wlc", osisIds[book] + ".xml"));
    foreach (var sourceVerse in doc.Descendants().Where(e => e.Name.LocalName == "verse" && e.Attribute("osisID") != null))
    {
        var refParts = ((string)sourceVerse.Attribute("osisID")!).Split('.');
        var verse = new Verse { Book = book, Chapter = int.Parse(refParts[1]), VerseNumber = int.Parse(refParts[2]) };
        var referenceNotes = sourceVerse.Elements().Where(e => e.Name.LocalName == "note" && e.Value.StartsWith("KJV:")).ToList();
        foreach (var note in referenceNotes)
        {
            var match = Regex.Match(note.Value, @"^KJV:([A-Za-z0-9]+)\.(\d+)\.(\d+)(?:!([a-z]))?$");
            if (!match.Success || Array.IndexOf(osisIds, match.Groups[1].Value) < 0) throw new InvalidDataException("Referência OSHB desconhecida: " + note.Value);
            var mapped = new Ref(Array.IndexOf(osisIds, match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), match.Groups[4].Value);
            if (!verse.KjvReferences.Contains(mapped)) verse.KjvReferences.Add(mapped);
        }
        if (book == 18 && referenceNotes.Count > 0)
        {
            string prefix = string.Concat(sourceVerse.Elements().TakeWhile(e => e != referenceNotes[0]).Where(e => e.Name.LocalName is "w" or "seg").Select(e => e.Value.Replace("/", "")));
            verse.SuperscriptionLetters = NativeReferences.Normalize(prefix);
        }
        foreach (var element in sourceVerse.Elements())
        {
            if (element.Name.LocalName == "w")
            {
                string text = element.Value.Replace("/", "");
                int segmentOffset = 0; var segments = new List<Segment>();
                foreach (string segment in element.Value.Split('/')) { if (segment.Length > 0) segments.Add(new(segment, segmentOffset)); segmentOffset += segment.Length; }
                string rawLemma = (string?)element.Attribute("lemma") ?? "";
                var codes = StrongCodes(rawLemma);
                verse.Words.Add(new() { Text = text, Suffix = " ", Strongs = codes, Segments = segments, Lemma = codes.Select(c => lexicon.GetValueOrDefault(LexKey(c))?.Lemma).FirstOrDefault(l => l != null) ?? "", Morphology = (string?)element.Attribute("morph") ?? "", SourceId = (string?)element.Attribute("id") ?? "", Reading = ((string?)element.Attribute("type") == "x-ketiv" ? "Ketiv · " : "") + "Lema segmentado da fonte: " + rawLemma });
            }
            else if (element.Name.LocalName == "seg" && verse.Words.Count > 0) verse.Words[^1].Suffix = element.Value + (element.Value == "־" ? "" : " ");
            else if (element.Name.LocalName == "note")
            {
                var readings = element.Descendants().Where(e => e.Name.LocalName == "rdg").ToList();
                foreach (var reading in readings)
                    verse.Apparatus.Add(((string?)reading.Attribute("type") ?? "Leitura") + ": " + string.Join(" ", reading.Descendants().Where(e => e.Name.LocalName == "w").Select(e => e.Value.Replace("/", ""))));
            }
        }
        SetText(verse); wlc.Verses.Add(verse);
    }
}
var nativeIndex = bhs.Verses.ToDictionary(v => (v.Book, v.Chapter, v.VerseNumber));
var psalmHeadingCounts = new Dictionary<int, int>();
foreach (var chapter in wlc.Verses.Where(v => v.Book == 18).GroupBy(v => v.Chapter))
{
    var firstBody = chapter.OrderBy(v => v.VerseNumber).FirstOrDefault(v => v.KjvReferences.Any(r => r.Book == 18 && r.Chapter == chapter.Key && r.Verse == 1));
    int headings = firstBody?.VerseNumber is 2 or 3 ? firstBody.VerseNumber - 1 : 0;
    psalmHeadingCounts[chapter.Key] = headings;
}
foreach (var verse in wlc.Verses)
{
    if (!nativeIndex.TryGetValue((verse.Book, verse.Chapter, verse.VerseNumber), out var bhsVerse)) throw new InvalidDataException("Versículo WLC sem chave nativa BHS.");
    bool headingOnly = verse.Book == 18 && verse.VerseNumber <= psalmHeadingCounts[verse.Chapter];
    verse.IsSuperscription = bhsVerse.IsSuperscription = headingOnly;
    if (headingOnly) { verse.KjvReferences.Clear(); bhsVerse.KjvReferences.Clear(); }
    else if (verse.KjvReferences.Count > 0) bhsVerse.KjvReferences = verse.KjvReferences.ToList();
    else verse.KjvReferences = bhsVerse.KjvReferences.ToList();
    string prefix = headingOnly ? NativeReferences.Normalize(verse.Text) : verse.SuperscriptionLetters;
    foreach (var resourceVerse in new[] { verse, bhsVerse })
    {
        if (headingOnly) resourceVerse.SuperscriptionPrefixLength = resourceVerse.Text.Length;
        else if (prefix.Length > 0)
        {
            if (!NativeReferences.Normalize(resourceVerse.Text).StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException($"Prefixo de superscrição não corresponde: {resourceVerse.Book}/{resourceVerse.Chapter}/{resourceVerse.VerseNumber}");
            int letters = 0;
            foreach (var word in resourceVerse.Words)
            {
                letters += NativeReferences.Normalize(word.Text).Length;
                if (letters <= prefix.Length) resourceVerse.SuperscriptionPrefixLength = word.Start + word.Text.Length;
                if (letters >= prefix.Length) break;
            }
        }
    }
}
File.WriteAllText(Path.Combine(root, "docs", "versification-map-audit.json"), JsonSerializer.Serialize(new { source = "Open Scriptures OSHB inline KJV reference notes + original BHS mapping", psalmHeadingCounts, headingVerses = bhs.Verses.Count(v => v.IsSuperscription), partialHeadingVerses = bhs.Verses.Count(v => !v.IsSuperscription && v.SuperscriptionPrefixLength > 0), multiReferenceVerses = bhs.Verses.Count(v => v.KjvReferences.Count > 1) }, new JsonSerializerOptions { WriteIndented = true }));
WriteResource("BHS-A-Strong", bhs);
WriteResource("WLC-OSHB", wlc);

var greek = new Resource { Title = "SBLGNT · Texto e variantes", Language = "grc", ReferenceSystem = "SBLGNT", SourceUrl = "https://github.com/Faithlife/SBLGNT", Revision = revisions["sblgnt"], License = "Creative Commons Attribution 4.0 International (CC BY 4.0)", Attribution = "The Greek New Testament: SBL Edition, edited by Michael W. Holmes. Copyright 2010 Society of Biblical Literature and Logos Bible Software. CC BY 4.0. Conversão de XML para JSON pelo VERBUM; grafias e notas preservadas. O aparato compara edições publicadas e não é o aparato NA28/UBS nem um inventário de testemunhos manuscritos." };
for (int book = 39; book < osisIds.Length; book++)
{
    string fileName = osisIds[book] + ".xml";
    var doc = ReadXml(Path.Combine(sbl, "data", "sblgnt", "xml", fileName));
    Verse? current = null;
    foreach (var element in doc.Descendants())
    {
        if (element.Name.LocalName == "verse-number")
        {
            var reference = Regex.Match((string?)element.Attribute("id") ?? "", @" (\d+):(\d+)$");
            if (!reference.Success) throw new InvalidDataException("Referência SBLGNT inválida");
            current = new() { Book = book, Chapter = int.Parse(reference.Groups[1].Value), VerseNumber = int.Parse(reference.Groups[2].Value) }; greek.Verses.Add(current);
        }
        else if (current != null && element.Name.LocalName == "w") current.Words.Add(new() { Text = element.Value, SourceId = $"SBLGNT:{osisIds[book]}.{current.Chapter}.{current.VerseNumber}.{current.Words.Count + 1}" });
        else if (current?.Words.Count > 0 && element.Name.LocalName == "suffix") current.Words[^1].Suffix = element.Value.TrimEnd() + " ";
    }
    foreach (var verse in greek.Verses.Where(v => v.Book == book)) SetText(verse);
    var verseMap = greek.Verses.Where(v => v.Book == book).ToDictionary(v => (v.Chapter, v.VerseNumber));
    var appDoc = ReadXml(Path.Combine(sbl, "data", "sblgntapp", "xml", fileName));
    Verse? appVerse = null;
    foreach (var element in appDoc.Root!.Elements())
    {
        if (element.Name.LocalName == "verse")
        {
            var reference = Regex.Match(element.Value, @" (\d+):(\d+)$");
            appVerse = reference.Success ? verseMap.GetValueOrDefault((int.Parse(reference.Groups[1].Value), int.Parse(reference.Groups[2].Value))) : null;
        }
        else if (element.Name.LocalName == "note")
        {
            if (appVerse == null) throw new InvalidDataException("Nota SBLGNT sem referência reconhecida");
            appVerse.Apparatus.Add(element.Value);
        }
    }
}
WriteResource("SBLGNT", greek);
var report = new[] { Report("BHS-A-Strong", bhs), Report("WLC-OSHB", wlc), Report("SBLGNT", greek) };
File.WriteAllText(Path.Combine(root, "docs", "original-resources-import.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

static string LexKey(string code) => Regex.Replace(code.ToUpperInvariant(), @"(?<=[0-9])[A-Z]$", "");
static List<string> StrongCodes(string value) => Regex.Matches(value, @"(?i)([HG])?(\d{1,5})(?:\s*([a-z]))?").Select(m => (m.Groups[1].Success ? m.Groups[1].Value.ToUpperInvariant() : "H") + int.Parse(m.Groups[2].Value) + m.Groups[3].Value.ToLowerInvariant()).Distinct().ToList();
static void SetText(Verse verse)
{
    var text = new StringBuilder();
    foreach (var word in verse.Words) { word.Start = text.Length; text.Append(word.Text); text.Append(word.Suffix); }
    verse.Text = text.ToString().TrimEnd();
}
static XDocument ReadXml(string path)
{
    using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024 });
    return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
}
void WriteResource(string id, Resource resource)
{
    string destination = Path.Combine(output, id + ".json");
    string temp = destination + ".tmp";
    File.WriteAllText(temp, JsonSerializer.Serialize(resource, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    File.Move(temp, destination, true);
}
object Report(string id, Resource r) => new { id, revision = r.Revision, books = r.Verses.Select(v => v.Book).Distinct().Count(), verses = r.Verses.Count, words = r.Verses.Sum(v => v.Words.Count), strongWords = r.Verses.Sum(v => v.Words.Count(w => w.Strongs.Count > 0)), morphologyWords = r.Verses.Sum(v => v.Words.Count(w => w.Morphology.Length > 0)), notes = r.Verses.Sum(v => v.Apparatus.Count), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, id + ".json")))) };

class Resource
{
    public string Schema { get; set; } = "verbum.resource.v1";
    public string Title { get; set; } = "";
    public string Language { get; set; } = "";
    public string ReferenceSystem { get; set; } = "";
    public string Attribution { get; set; } = "";
    public string License { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Revision { get; set; } = "";
    public List<Verse> Verses { get; set; } = [];
    public Dictionary<string, Lex> Lexicon { get; set; } = [];
}
class Verse
{
    public bool IsSuperscription { get; set; }
    public int SuperscriptionPrefixLength { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string SuperscriptionLetters { get; set; } = "";
    public List<Ref> KjvReferences { get; set; } = [];
    public int Book { get; set; }
    public int Chapter { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("Verse")]
    public int VerseNumber { get; set; }
    public string Text { get; set; } = "";
    public List<Word> Words { get; set; } = [];
    public List<string> Apparatus { get; set; } = [];
}
record Ref(int Book, int Chapter, int Verse, string Part = "");
class Word
{
    public List<Segment> Segments { get; set; } = [];
    public string Text { get; set; } = "";
    public string Suffix { get; set; } = " ";
    public string Lemma { get; set; } = "";
    public List<string> Strongs { get; set; } = [];
    public string Morphology { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string Reading { get; set; } = "";
    public int Start { get; set; }
}
record Segment(string Text, int Start);
class Lex
{
    public string Code { get; set; } = "";
    public string Lemma { get; set; } = "";
    public string Transliteration { get; set; } = "";
    public string Definition { get; set; } = "";
    public string Derivation { get; set; } = "";
}

