using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Verbum;

if (args.Length == 2 && args[0] == "--counts")
{
    foreach (string edition in new[] { "WLC-OSHB", "SBLGNT" })
    {
        var counts = new Dictionary<string, int>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(args[1], edition), "*.json.gz"))
        {
            using var file = File.OpenRead(path); using var gzip = new GZipStream(file, CompressionMode.Decompress);
            var chapter = JsonSerializer.Deserialize<ExegesisChapter>(gzip)!;
            foreach (var word in chapter.Verses.SelectMany(v => v.Words).Where(w => w.Lemma.Length > 0)) counts[word.Lemma] = counts.GetValueOrDefault(word.Lemma) + 1;
        }
        WriteGzip(Path.Combine(args[1], edition + "-lemma-counts.json.gz"), counts); Console.WriteLine(edition + ": " + counts.Count + " lemas.");
    }
    return;
}
if (args.Length != 4) throw new ArgumentException("Uso: ImportExegesis hebrew.zip greek.zip HOTTP.XML pasta-saida");
string destination = Path.GetFullPath(args[3]); Directory.CreateDirectory(destination);
var manifest = new List<object>();
string[] abbreviations = "GEN EXO LEV NUM DEU JOS JDG RUT 1SA 2SA 1KI 2KI 1CH 2CH EZR NEH EST JOB PSA PRO ECC SNG ISA JER LAM EZK DAN HOS JOL AMO OBA JON MIC NAM HAB ZEP HAG ZEC MAL MAT MRK LUK JHN ACT ROM 1CO 2CO GAL EPH PHP COL 1TH 2TH 1TI 2TI TIT PHM HEB JAS 1PE 2PE 1JN 2JN 3JN JUD REV".Split(' ');
var bookIds = abbreviations.Select((a, i) => (a, i)).ToDictionary(x => x.a, x => x.i, StringComparer.OrdinalIgnoreCase);
foreach (var (path, lane, edition, revision) in new[] { (args[0], "/WLC/lowfat/", "WLC-OSHB", "47db250bd55d0d8577f2a94fba114ef16c35b23c"), (args[1], "/SBLGNT/lowfat/", "SBLGNT", "8423afe47b9e8f24b7772e808af45c7159a6fe7e") })
{
    using var zip = ZipFile.OpenRead(path); int chapters = 0, words = 0, groups = 0; var outputs = new HashSet<string>();
    foreach (var file in zip.Entries.Where(e => e.FullName.Contains(lane, StringComparison.Ordinal) && e.Name.EndsWith(".xml")).OrderBy(e => e.FullName))
    {
        using var stream = file.Open(); var xml = ReadXml(stream); var byReference = new Dictionary<(int Book, int Chapter, int Verse), ExegesisVerse>();
        var elements = xml.Descendants().Where(e => e.Name.LocalName == "w").ToList();
        var positions = new Dictionary<XElement, (int Book, int Chapter, int Verse)>();
        foreach (var w in elements)
        {
            var match = Regex.Match(A(w, "ref"), @"^(\S+)\s+(\d+):(\d+)!?(\d*)");
            if (!match.Success || !bookIds.TryGetValue(match.Groups[1].Value, out int book)) throw new InvalidDataException("Referência desconhecida: " + A(w, "ref"));
            var key = (book, int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)); positions[w] = key;
            if (!byReference.TryGetValue(key, out var verse)) byReference[key] = verse = new() { Verse = key.Item3 };
            string id = A(w, "id"); if (id.Length == 0) throw new InvalidDataException("Palavra sem identificador: " + file.Name);
            // Deliberately omit MARBLE/SDBH semantic fields labelled 'used with permission'.
            var item = new ExegesisWord { Id = id, Order = match.Groups[4].Success && match.Groups[4].Length > 0 ? int.Parse(match.Groups[4].Value) : verse.Words.Count,
                Text = w.Value, Lemma = A(w, "lemma"), English = A(w, "english"), Morphology = A(w, "morph"), PartOfSpeech = A(w, "pos").Length > 0 ? A(w, "pos") : A(w, "class"), Role = A(w, "role"), Strong = A(w, "strongnumberx").Length > 0 ? A(w, "strongnumberx") : A(w, "strong") };
            foreach (string feature in new[] { "gender", "number", "person", "state", "stem", "type", "tense", "voice", "mood", "case", "degree" })
                if (A(w, feature).Length > 0) item.Features[feature] = A(w, feature);
            if (verse.Words.Any(x => x.Id == id)) throw new InvalidDataException("ID duplicado: " + id);
            verse.Words.Add(item); words++;
        }
        foreach (var g in xml.Descendants().Where(e => e.Name.LocalName == "wg"))
        {
            var members = g.Descendants().Where(positions.ContainsKey).ToList();
            foreach (var part in members.GroupBy(w => positions[w]))
            {
                var item = new ExegesisGroup { Kind = A(g, "class"), Role = A(g, "role"), Rule = A(g, "rule"), Depth = g.Ancestors().Count(a => a.Name.LocalName == "wg"),
                    Sentence = g.Ancestors().FirstOrDefault(a => a.Name.LocalName == "sentence") is { } sentence ? A(sentence, "id") : "", Words = part.Select(w => A(w, "id")).Distinct().ToList() };
                byReference[part.Key].Groups.Add(item); groups++;
            }
        }
        foreach (var chapter in byReference.GroupBy(p => (p.Key.Book, p.Key.Chapter)))
        {
            var data = new ExegesisChapter { Source = edition == "SBLGNT" ? "MACULA Greek" : "MACULA Hebrew", Revision = revision, Edition = edition, Book = chapter.Key.Book, Chapter = chapter.Key.Chapter, Verses = chapter.OrderBy(p => p.Key.Verse).Select(p => p.Value).ToList() };
            foreach (var verse in data.Verses) verse.Words = verse.Words.OrderBy(w => w.Order).ThenBy(w => w.Id, StringComparer.Ordinal).ToList();
            string folder = Path.Combine(destination, edition); Directory.CreateDirectory(folder); string output = Path.Combine(folder, $"{data.Book:D2}-{data.Chapter:D3}.json.gz");
            if (!outputs.Add(output)) throw new InvalidDataException("Capítulo duplicado no corpus: " + output);
            WriteGzip(output, data); chapters++;
        }
    }
    if (chapters == 0) throw new InvalidDataException("Nenhum arquivo lowfat: " + lane);
    manifest.Add(new { source = edition == "SBLGNT" ? "MACULA Greek" : "MACULA Hebrew", revision, archiveSha256 = Hash(path), chapters, words, groups, license = "CC BY 4.0 for selected layers; component-specific credits", omitted = "MARBLE/SDBH semantic fields; no Portuguese alignment generated" });
    Console.WriteLine($"{edition}: {chapters} capítulos, {words} palavras/segmentos, {groups} grupos.");
}
using (var input = File.OpenRead(args[2]))
{
    var xml = ReadXml(input); var comments = new List<TextualComment>();
    foreach (var entry in xml.Root!.Elements("HOTTP_Entry"))
    {
        var item = new TextualComment { Id = A(entry, "Id"), Remark = Clean(entry.Element("Remark")?.Value ?? ""), Suggestion = Clean(entry.Element("Suggestion")?.Value ?? "") };
        foreach (string reference in entry.Element("References")!.Elements("Reference").Select(e => e.Value))
        {
            if (!Regex.IsMatch(reference, @"^\d{14}$")) throw new InvalidDataException("Referência HOTTP: " + reference);
            var location = new TextualReference(int.Parse(reference[..3]) - 1, int.Parse(reference.Substring(3, 3)), int.Parse(reference.Substring(6, 3)));
            if (!item.References.Contains(location)) item.References.Add(location);
        }
        foreach (var alternative in entry.Element("Alternatives")?.Elements("Alternative") ?? [])
            item.Alternatives.Add(new() { Text = Clean(alternative.Element("Source")?.Value ?? ""), Rating = alternative.Element("Rating")?.Value ?? "", Literal = Clean(alternative.Element("Literal")?.Value ?? ""),
                Factors = alternative.Element("Factors")?.Elements("Factor").Select(f => f.Value).ToList() ?? [],
                Versions = alternative.Element("Versions")?.Elements("Version").Select(v => (v.Element("Text")?.Value ?? "") + ": " + Clean(v.Element("Content")?.Value ?? "")).ToList() ?? [] });
        comments.Add(item);
    }
    WriteGzip(Path.Combine(destination, "hottp.json.gz"), comments);
    manifest.Add(new { source = "UBS HOTTP", revision = "a15a96cfaa4ca18a6c0339016645ba1e534db3c8", sha256 = Hash(args[2]), entries = comments.Count, license = "CC BY-SA 4.0", language = "English", references = "BBBCCCVVVSSWWW; reference scope retained, word positions not mapped" });
    Console.WriteLine($"HOTTP: {comments.Count} comentários.");
}
File.WriteAllText(Path.Combine(destination, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
static XDocument ReadXml(Stream stream) { using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_000_000 }); return XDocument.Load(reader); }
static string A(XElement e, string name) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value ?? "";
static string Clean(string text) => System.Net.WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]*>", ""));
static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant(); }
static void WriteGzip<T>(string path, T value) { using var file = File.Create(path); using var gzip = new GZipStream(file, CompressionLevel.SmallestSize); JsonSerializer.Serialize(gzip, value); }
