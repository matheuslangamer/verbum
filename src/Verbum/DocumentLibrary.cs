using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Verbum;

public sealed class ReadingDocument
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Format { get; set; } = "";
    public string Original { get; set; } = "";
    public string Attribution { get; set; } = "";
    public List<DocumentSection> Sections { get; set; } = [];
    public override string ToString() => Title;
}
public sealed class DocumentSection
{
    public string Title { get; set; } = "";
    public List<ReadingBlock> Blocks { get; set; } = [];
    public string PlainText => string.Join("\n", Blocks.Select(b => b.Kind == "table" ? string.Join("\n", b.Rows.Select(r => string.Join(" | ", r))) : b.Text));
    public override string ToString() => Title;
}
public sealed class ReadingBlock
{
    public string Kind { get; set; } = "paragraph";
    public string Text { get; set; } = "";
    public string Image { get; set; } = "";
    public List<List<string>> Rows { get; set; } = [];
}
public sealed record DocumentHit(ReadingDocument Document, int Section, string Query, string Excerpt);
public sealed class DocumentLibrary
{
    public string DirectoryPath { get; }
    public const string FileFilter = "Livros e documentos|*.pdf;*.epub;*.docx;*.doc;*.rtf;*.txt;*.md;*.html;*.htm|Todos os arquivos|*.*";
    public DocumentLibrary(string bibleDirectory) { DirectoryPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(bibleDirectory))!, "livros"); Directory.CreateDirectory(DirectoryPath); }
    public List<ReadingDocument> Load() => Directory.EnumerateFiles(DirectoryPath, "*.json").Select(p => JsonSerializer.Deserialize<ReadingDocument>(File.ReadAllText(p)) ?? throw new InvalidDataException(p)).OrderBy(d => d.Title).ToList();
    public ReadingDocument? Find(string id) => Load().FirstOrDefault(d => d.Id == id);
    public void Save(ReadingDocument document) { string path = Path.Combine(DirectoryPath, document.Id + ".json"); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(document)); File.Move(path + ".tmp", path, true); }
    public void Remove(string id)
    {
        var document = Find(id) ?? throw new InvalidOperationException("Documento não encontrado.");
        string source = AssetPath(document.Id + ".json");
        string archive = Path.Combine(DirectoryPath, "removidos"); Directory.CreateDirectory(archive);
        File.Move(source, Path.Combine(archive, Path.GetFileName(source)), true);
    }
    public void RestoreRemoved()
    {
        string archive = Path.Combine(DirectoryPath, "removidos");
        if (!Directory.Exists(archive)) return;
        foreach (string path in Directory.EnumerateFiles(archive, "*.json"))
        {
            string target = Path.Combine(DirectoryPath, Path.GetFileName(path));
            if (!File.Exists(target)) File.Move(path, target);
        }
    }
    public ReadingDocument Import(string path)
    {
        path = Path.GetFullPath(path); var info = new FileInfo(path); if (info.Length > 512L * 1024 * 1024) throw new InvalidDataException("Limite de importação: 512 MB por arquivo.");
        string ext = info.Extension.ToLowerInvariant(), id;
        using (var stream = File.OpenRead(path)) id = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        var existing = Find(id) ?? Load().FirstOrDefault(d => Path.GetFileNameWithoutExtension(d.Original) == id); if (existing != null) return existing;
        var document = new ReadingDocument { Id = id, Title = Path.GetFileNameWithoutExtension(path), Format = ext.TrimStart('.').ToUpperInvariant(), Attribution = "Arquivo pessoal importado. Original preservado; extração de texto para leitura e pesquisa local." };
        if (ext == ".pdf") document.Sections = PdfDocumentReader.Extract(path);
        else if (ext == ".docx") document.Sections = ReadDocx(path, DirectoryPath, id);
        else if (ext == ".epub") document.Sections = ReadEpub(path, DirectoryPath, id);
        else if (ext is ".doc" or ".rtf") document.Sections = [new() { Title = document.Title, Blocks = [new() { Text = ReadWord(path) }] }];
        else if (ext is ".txt" or ".md" or ".html" or ".htm") document.Sections = [new() { Title = document.Title, Blocks = [new() { Text = ext is ".html" or ".htm" ? PlainHtml(File.ReadAllText(path)) : File.ReadAllText(path) }] }];
        else throw new InvalidDataException("Formato não suportado. Use PDF, EPUB sem DRM, DOCX, DOC, RTF, TXT, Markdown ou HTML.");
        if (document.Sections.Count == 0) throw new InvalidDataException("O documento não possui conteúdo legível.");
        string originalDirectory = Path.Combine(DirectoryPath, "originais"); Directory.CreateDirectory(originalDirectory);
        string destination = Path.Combine(originalDirectory, id + ext); File.Copy(path, destination, true); document.Original = Path.GetRelativePath(DirectoryPath, destination); Save(document); return document;
    }
    public string OriginalPath(ReadingDocument document)
    {
        return AssetPath(document.Original);
    }
    public string AssetPath(string relative)
    {
        string path = Path.GetFullPath(Path.Combine(DirectoryPath, relative));
        if (!path.StartsWith(Path.GetFullPath(DirectoryPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho de documento inválido.");
        return path;
    }
    private static XDocument Xml(Stream stream) { using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 60_000_000 }); return XDocument.Load(reader); }
    private static XDocument EntryXml(ZipArchive zip, string entry) { var item = zip.GetEntry(entry) ?? throw new InvalidDataException("Parte ausente: " + entry); if (item.Length > 60_000_000) throw new InvalidDataException("Parte XML muito grande."); using var stream = item.Open(); return Xml(stream); }
    private static string WordText(XElement element) => string.Concat(element.Descendants().Where(e => e.Name.LocalName is "t" or "tab" or "br").Select(e => e.Name.LocalName == "t" ? e.Value : " "));
    public static List<DocumentSection> ReadDocx(string path, string? libraryDirectory = null, string? documentId = null)
    {
        using var zip = ZipFile.OpenRead(path); var xml = EntryXml(zip, "word/document.xml");
        var relationships = zip.GetEntry("word/_rels/document.xml.rels") != null ? EntryXml(zip, "word/_rels/document.xml.rels").Descendants().Where(e => e.Name.LocalName == "Relationship" && (string?)e.Attribute("TargetMode") != "External").ToDictionary(e => (string)e.Attribute("Id")!, e => (string?)e.Attribute("Target") ?? "") : [];
        var sections = new List<DocumentSection>(); var section = new DocumentSection { Title = "Início" }; sections.Add(section);
        foreach (var element in xml.Descendants().First(e => e.Name.LocalName == "body").Elements())
        {
            if (element.Name.LocalName == "p")
            {
                string text = WordText(element);
                string style = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "pStyle")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "val")?.Value ?? "";
                bool heading = Regex.IsMatch(style, @"(?i)^(Heading|Título|Titulo|Title)[1-3]?$");
                if (heading && section.Blocks.Count > 0) { section = new() { Title = text }; sections.Add(section); }
                if (text.Length > 0) section.Blocks.Add(new() { Kind = heading ? "heading" : "paragraph", Text = text });
                foreach (var image in element.Descendants().Where(e => e.Name.LocalName == "blip"))
                {
                    string rel = image.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value ?? "";
                    if (relationships.TryGetValue(rel, out string? target))
                    {
                        string entry = Uri.UnescapeDataString(new Uri(new Uri("https://local.invalid/word/document.xml"), target).AbsolutePath.TrimStart('/'));
                        var block = ExtractImage(zip, entry, libraryDirectory, documentId); if (block != null) section.Blocks.Add(block);
                    }
                }
            }
            else if (element.Name.LocalName == "tbl") section.Blocks.Add(new() { Kind = "table", Rows = element.Elements().Where(e => e.Name.LocalName == "tr").Select(r => r.Elements().Where(e => e.Name.LocalName == "tc").Select(WordText).ToList()).ToList() });
        }
        return sections.Where(s => s.Blocks.Count > 0).ToList();
    }
    public static List<DocumentSection> ReadEpub(string path, string? libraryDirectory = null, string? documentId = null)
    {
        using var zip = ZipFile.OpenRead(path);
        if (zip.GetEntry("META-INF/encryption.xml") is { } encrypted && encrypted.Length > 0) throw new InvalidDataException("EPUB com recursos criptografados: importe uma edição sem DRM.");
        var container = EntryXml(zip, "META-INF/container.xml"); string opfPath = container.Descendants().First(e => e.Name.LocalName == "rootfile").Attribute("full-path")?.Value ?? throw new InvalidDataException("EPUB sem pacote.");
        var opf = EntryXml(zip, opfPath); var manifest = opf.Descendants().Where(e => e.Name.LocalName == "item").ToDictionary(e => (string)e.Attribute("id")!, e => (string?)e.Attribute("href") ?? "");
        var sections = new List<DocumentSection>();
        foreach (var item in opf.Descendants().Where(e => e.Name.LocalName == "itemref"))
        {
            if (!manifest.TryGetValue((string?)item.Attribute("idref") ?? "", out string? href)) continue;
            var uri = new Uri(new Uri("https://local.invalid/" + opfPath), href); string name = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            var chapter = EntryXml(zip, name); var body = chapter.Descendants().FirstOrDefault(e => e.Name.LocalName == "body"); if (body == null) continue;
            var blocks = new List<ReadingBlock>();
            foreach (var e in body.Descendants().Where(e => e.Name.LocalName is "p" or "h1" or "h2" or "h3" or "li" or "table" or "img"))
            {
                if (e.Ancestors().Any(a => a.Name.LocalName is "table" or "script" or "style")) continue;
                if (e.Name.LocalName == "img")
                {
                    string? imageSource = (string?)e.Attribute("src"); if (imageSource == null) continue;
                    var imageUri = new Uri(new Uri("https://local.invalid/" + name), imageSource); if (imageUri.Host != "local.invalid") continue;
                    var image = ExtractImage(zip, Uri.UnescapeDataString(imageUri.AbsolutePath.TrimStart('/')), libraryDirectory, documentId); if (image != null) { image.Text = (string?)e.Attribute("alt") ?? "Ilustração do livro"; blocks.Add(image); }
                }
                else if (e.Name.LocalName == "table") blocks.Add(new() { Kind = "table", Rows = e.Descendants().Where(r => r.Name.LocalName == "tr").Select(r => r.Elements().Where(c => c.Name.LocalName is "td" or "th").Select(c => c.Value).ToList()).ToList() });
                else blocks.Add(new() { Kind = e.Name.LocalName.StartsWith('h') ? "heading" : "paragraph", Text = e.Value });
            }
            if (blocks.Count > 0) sections.Add(new() { Title = blocks.FirstOrDefault(b => b.Kind == "heading")?.Text ?? $"Capítulo {sections.Count + 1}", Blocks = blocks });
        }
        return sections;
    }
    private static ReadingBlock? ExtractImage(ZipArchive zip, string name, string? directory, string? id)
    {
        if (directory == null || id == null) return null;
        var entry = zip.GetEntry(name); string extension = Path.GetExtension(name).ToLowerInvariant();
        if (entry == null || entry.Length > 32_000_000 || extension is not (".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".tif" or ".tiff")) return null;
        string relative = Path.Combine("imagens", id, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + extension), destination = Path.Combine(directory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!); using (var input = entry.Open()) using (var output = File.Create(destination)) input.CopyTo(output);
        return new() { Kind = "image", Image = relative, Text = "Ilustração do documento" };
    }
    private static string PlainHtml(string text) => System.Net.WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(Regex.Replace(text, @"(?is)<(script|style)\b[^>]*>.*?</\1>", ""), @"(?i)</?(p|div|br|h[1-6]|tr)\b[^>]*>", "\n"), "<[^>]*>", ""));
    private static string ReadWord(string path)
    {
        string? result = null; Exception? failure = null;
        var thread = new Thread(() =>
        {
            object? application = null, document = null;
            try
            {
                var type = Type.GetTypeFromProgID("Word.Application") ?? throw new InvalidOperationException("A leitura de DOC/RTF exige Microsoft Word instalado. Você também pode salvar o arquivo como DOCX e importá-lo.");
                application = Activator.CreateInstance(type); dynamic word = application!; word.Visible = false; word.DisplayAlerts = 0; word.AutomationSecurity = 3;
                document = word.Documents.Open(path, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false, Visible: false); dynamic doc = document; result = (string)doc.Content.Text;
            }
            catch (Exception ex) { failure = ex; }
            finally { if (document != null) { try { ((dynamic)document).Close(false); } catch { } System.Runtime.InteropServices.Marshal.FinalReleaseComObject(document); } if (application != null) { try { ((dynamic)application).Quit(false); } catch { } System.Runtime.InteropServices.Marshal.FinalReleaseComObject(application); } }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (failure != null) throw new InvalidDataException("Não foi possível ler o Word antigo: " + failure.Message, failure); return result ?? "";
    }
    public List<DocumentHit> Search(string query, SearchMode mode, CancellationToken cancellation)
    {
        var found = new List<DocumentHit>();
        foreach (var document in Load())
        for (int i = 0; i < document.Sections.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested(); string text = document.Sections[i].PlainText; var matches = Matches(text, query, mode);
            if (matches.Count == 0) continue;
            int start = Math.Max(0, matches[0].Start - 70), length = Math.Min(320, text.Length - start);
            found.Add(new(document, i, query, (start > 0 ? "…" : "") + text.Substring(start, length) + (start + length < text.Length ? "…" : "")));
        }
        return found;
    }
    public static List<SearchToken> Matches(string text, string query, SearchMode mode, bool requireAll = true)
    {
        if (mode is SearchMode.Strong or SearchMode.Lemma) mode = SearchMode.Intelligent;
        var words = SearchText.Tokenize(text); var wanted = mode == SearchMode.Exact ? SearchText.Tokenize(query).Select(t => t.Original.Normalize().ToUpperInvariant()).Distinct().ToArray() : SearchText.QueryTokens(query);
        bool Match(SearchToken token, string term) => mode == SearchMode.Exact ? token.Original.Normalize().ToUpperInvariant() == term : SearchText.WordMatches(token.Normalized, term, mode);
        return wanted.Length > 0 && (!requireAll || wanted.All(q => words.Any(w => Match(w, q)))) ? words.Where(w => wanted.Any(q => Match(w, q))).ToList() : [];
    }
}
