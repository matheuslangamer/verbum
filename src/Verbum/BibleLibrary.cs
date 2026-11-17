using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Verbum;

public sealed class BibleLibrary
{
    public string DirectoryPath { get; }
    public List<EditionInfo> Editions { get; }
    public Task<PortugueseParsing> Parsing { get; }
    public ExegesisCatalog Exegesis { get; }
    public Task<List<LexicalResource>> Lexicons { get; }
    public TextMarkers Markers { get; }
    private readonly ConcurrentDictionary<string, Lazy<Task<BibleEdition>>> cache = new(StringComparer.OrdinalIgnoreCase);
    internal static readonly int[] StandardChapterCounts = [50,40,27,36,34,24,21,4,31,24,22,25,29,36,10,13,10,42,150,31,12,8,66,52,5,48,12,14,3,9,1,4,7,3,3,3,2,14,4,28,16,24,21,28,16,16,13,6,6,4,4,5,3,6,4,3,1,13,5,5,3,5,1,1,1,22];
    private static readonly Regex NumberPrefix = new(@"^\s*(\d{1,3})(?:\s*[-–—]\s*(\d{1,3}))?(?:[.:]?\s+)", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, MaxDepth = 12 };
    internal static readonly string[] CanonicalNames = "Gênesis|Êxodo|Levítico|Números|Deuteronômio|Josué|Juízes|Rute|1 Samuel|2 Samuel|1 Reis|2 Reis|1 Crônicas|2 Crônicas|Esdras|Neemias|Ester|Jó|Salmos|Provérbios|Eclesiastes|Cântico dos Cânticos|Isaías|Jeremias|Lamentações|Ezequiel|Daniel|Oseias|Joel|Amós|Obadias|Jonas|Miqueias|Naum|Habacuque|Sofonias|Ageu|Zacarias|Malaquias|Mateus|Marcos|Lucas|João|Atos|Romanos|1 Coríntios|2 Coríntios|Gálatas|Efésios|Filipenses|Colossenses|1 Tessalonicenses|2 Tessalonicenses|1 Timóteo|2 Timóteo|Tito|Filemom|Hebreus|Tiago|1 Pedro|2 Pedro|1 João|2 João|3 João|Judas|Apocalipse".Split('|');
    internal static readonly string[] CanonicalAbbrevs = "Gn Ex Lv Nm Dt Js Jz Rt 1Sm 2Sm 1Rs 2Rs 1Cr 2Cr Ed Ne Et Jó Sl Pv Ec Ct Is Jr Lm Ez Dn Os Jl Am Ob Jn Mq Na Hc Sf Ag Zc Ml Mt Mc Lc Jo At Rm 1Co 2Co Gl Ef Fp Cl 1Ts 2Ts 1Tm 2Tm Tt Fm Hb Tg 1Pe 2Pe 1Jo 2Jo 3Jo Jd Ap".Split(' ');

    public BibleLibrary(string path)
    {
        DirectoryPath = path;
        Exegesis = new(path);
        Lexicons = Task.Run(() => LexicalResource.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "lexicos")));
        Markers = new(Path.Combine(SessionStore.DataDirectory, "text-markers.json"));
        Parsing = Task.Run(() => PortugueseParsing.Load(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, "parsing", "hebraico-usuario.md")));
        Editions = Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new EditionInfo(Path.GetFileNameWithoutExtension(x), x)).ToList() : [];
    }

    public bool IsLoaded(string id) => cache.TryGetValue(id, out var entry) && entry.IsValueCreated && entry.Value.IsCompleted;
    public Task<BibleEdition> LoadAsync(string id) => cache.GetOrAdd(id, key => new(() => Task.Run(() =>
    {
        var info = Editions.FirstOrDefault(x => x.Id.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("Bíblia não encontrada: " + key);
        // Both large files are read at the same time; the OSHB parsing is linked afterwards.
        var oshb = info.Id == "BHS-A-Strong" && Editions.Any(e => e.Id == "WLC-OSHB") ? LoadAsync("WLC-OSHB") : null;
        var edition = ReadEdition(info);
        if (oshb != null) MorphologyLinks.Enrich(edition, oshb.GetAwaiter().GetResult());
        return edition;
    }))).Value;

    private readonly ConcurrentDictionary<string, Lazy<Task<TranslationAlignment?>>> alignments = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Automatic word alignment of a translation with the Hebrew text, when its file exists.</summary>
    public Task<TranslationAlignment?> Alignment(string translation) => alignments.GetOrAdd(translation, key => new(() => Task.Run(() =>
    {
        try
        {
            var info = Editions.FirstOrDefault(x => x.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
            return info == null ? null : TranslationAlignment.Load(TranslationAlignment.PathFor(DirectoryPath, info.Id), new FileInfo(info.Path).Length);
        }
        catch { return null; }
    }))).Value;
    /// <summary>Translation shown beside original words: ARA when aligned, otherwise the first aligned one.</summary>
    public string? AlignedTranslation()
    {
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(DirectoryPath))!, "alinhamento");
        if (!Directory.Exists(folder)) return null;
        var ids = Directory.EnumerateFiles(folder, "*.json.gz").Select(f => Path.GetFileName(f)[..^8]).Where(id => Editions.Any(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).Order(StringComparer.OrdinalIgnoreCase).ToList();
        return ids.FirstOrDefault(id => id.Equals("ARA", StringComparison.OrdinalIgnoreCase)) ?? ids.FirstOrDefault();
    }

    internal static BibleEdition ReadEdition(EditionInfo info)
    {
        var bytes = File.ReadAllBytes(info.Path);
        if (bytes.Length > 256 * 1024 * 1024) throw new InvalidDataException("Arquivo excede o limite de 256 MB.");
        if (System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 128)).TrimStart('\uFEFF', ' ', '\r', '\n', '\t').StartsWith('{')) return ReadOriginal(info, bytes);
        var books = JsonSerializer.Deserialize<List<BibleBook>>(bytes, JsonOptions) ?? throw new InvalidDataException("Lista de livros vazia.");
        if (books.Count is < 1 or > 100 || books.Any(b => b == null || string.IsNullOrWhiteSpace(b.Name) || b.Chapters == null || b.Chapters.Count > 200 || b.Chapters.Any(c => c == null || c.Count > 500 || c.Any(t => t == null))))
            throw new InvalidDataException("Formato inválido. Esperado: [{abbrev, name, chapters: [[texto]]}].");
        var edition = new BibleEdition { Info = info, Books = books };
        int allCount = books.Sum(b => b.Chapters.Sum(c => c.Count));
        int prefixed = books.Sum(b => b.Chapters.Sum(c => c.Count(t => NumberPrefix.IsMatch(t))));
        bool explicitNumbers = prefixed > allCount * .25;
        if (books.Count != 66) edition.Warnings.Add($"{books.Count} livros. A validação da contagem de capítulos aplica-se apenas à coleção de 66 livros.");
        if (explicitNumbers) edition.Warnings.Add("Texto com numeração explícita e intervalos. Entradas sem número permanecem sem correspondência automática.");
        for (int b = 0; b < books.Count; b++)
        {
            var book = books[b];
            bool valid = books.Count == 66 && book.Chapters.Count == StandardChapterCounts[b];
            if (!valid) edition.Warnings.Add($"{book.Name}: {book.Chapters.Count} capítulos no arquivo; sincronização desativada neste livro até revisão da estrutura.");
            for (int c = 0; c < book.Chapters.Count; c++)
            {
                var entries = new List<VerseEntry>();
                for (int e = 0; e < book.Chapters[c].Count; e++)
                {
                    string text = book.Chapters[c][e].Trim();
                    int? start = explicitNumbers ? null : e + 1, end = start;
                    if (explicitNumbers)
                    {
                        var match = NumberPrefix.Match(text);
                        if (match.Success)
                        {
                            start = int.Parse(match.Groups[1].Value);
                            end = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : start;
                            if (start < 1 || end < start || end > 250) { start = null; end = null; }
                            else text = text[match.Length..].Trim();
                        }
                    }
                    entries.Add(new(info.Id, b, book.Name, book.Abbrev, c + 1, e, start, end, text, valid));
                }
                edition.Chapters[(b, c + 1)] = entries;
                edition.Entries.AddRange(entries);
            }
        }
        return edition;
    }

    private static BibleEdition ReadOriginal(EditionInfo info, byte[] bytes)
    {
        var resource = JsonSerializer.Deserialize<OriginalResource>(bytes, JsonOptions) ?? throw new InvalidDataException("Recurso original vazio.");
        if (resource.Schema != "verbum.resource.v1" || resource.Verses.Count == 0 || resource.Verses.Count > 100000) throw new InvalidDataException("Esquema de recurso original inválido.");
        var books = CanonicalNames.Select((name, i) => new BibleBook { Name = name, Abbrev = CanonicalAbbrevs[i] }).ToList();
        var edition = new BibleEdition { Info = info, Books = books, Resource = resource };
        var unique = new HashSet<(int, int, int)>();
        foreach (var verse in resource.Verses.OrderBy(v => v.Book).ThenBy(v => v.Chapter).ThenBy(v => v.Verse))
        {
            if (verse.Book is < 0 or >= 66 || verse.Chapter is < 1 or > 200 || verse.Verse is < 0 or > 250 || !unique.Add((verse.Book, verse.Chapter, verse.Verse))) throw new InvalidDataException("Referência repetida ou inválida no recurso original.");
            foreach (var word in verse.Words)
            {
                if (word.Start < 0 || word.Start + word.Text.Length > verse.Text.Length || verse.Text.Substring(word.Start, word.Text.Length) != word.Text) throw new InvalidDataException("Segmento de palavra não corresponde ao texto original.");
                foreach (var segment in word.Segments)
                    if (segment.Start < 0 || segment.Start + segment.Text.Length > word.Text.Length || word.Text.Substring(segment.Start, segment.Text.Length) != segment.Text) throw new InvalidDataException("Subsegmento não corresponde à grafia original.");
            }
            var book = books[verse.Book];
            while (book.Chapters.Count < verse.Chapter) book.Chapters.Add([]);
            book.Chapters[verse.Chapter - 1].Add(verse.Text);
            var key = (verse.Book, verse.Chapter);
            if (!edition.Chapters.TryGetValue(key, out var chapter)) edition.Chapters[key] = chapter = [];
            var entry = new VerseEntry(info.Id, verse.Book, book.Name, book.Abbrev, verse.Chapter, chapter.Count, verse.Verse, verse.Verse, verse.Text, true) { Words = verse.Words, Apparatus = verse.Apparatus, KjvReferences = verse.KjvReferences, IsSuperscription = verse.IsSuperscription, SuperscriptionPrefixLength = verse.SuperscriptionPrefixLength };
            chapter.Add(entry); edition.Entries.Add(entry);
            if (!entry.IsSuperscription)
            foreach (var mapped in entry.KjvReferences)
            {
                var mappedKey = (mapped.Book, mapped.Chapter, mapped.Verse);
                if (!edition.FromKjv.TryGetValue(mappedKey, out var targets)) edition.FromKjv[mappedKey] = targets = [];
                targets.Add(entry);
            }
        }
        if (resource.ReferenceSystem == "MT") edition.Warnings.Add("Versificação hebraica MT preservada. Sincronização com sistemas diferentes é suspensa para evitar referências incorretas.");
        return edition;
    }

    public static bool CanSynchronize(BibleEdition source, BibleEdition target)
        => source.Resource?.ReferenceSystem != "MT" && target.Resource?.ReferenceSystem != "MT"
           || source.Resource?.ReferenceSystem == "MT" && target.Resource?.ReferenceSystem == "MT";
    public static VerseEntry? ResolveParallel(BibleEdition source, VerseEntry verse, BibleEdition target)
    {
        var linked = ResolveLinks(source, verse, target); return linked.Targets.Count == 1 ? linked.Targets[0] : null;
    }
    public static bool IsSuperscriptionHit(SearchHit hit) => hit.Verse.IsSuperscription || hit.Verse.SuperscriptionPrefixLength > 0 && hit.Highlights.Count > 0 && hit.Highlights.All(h => h.Start + h.Length <= hit.Verse.SuperscriptionPrefixLength);
    public static VerseLink ResolveLinks(BibleEdition source, VerseEntry verse, BibleEdition target)
    {
        bool sourceMt = source.Resource?.ReferenceSystem == "MT", targetMt = target.Resource?.ReferenceSystem == "MT";
        if (!verse.IsMapped) return new([], "Entrada sem referência validada.");
        if (sourceMt && !targetMt && verse.IsSuperscription) return new([], "Superscrição hebraica: esta biblioteca portuguesa não inclui o seu texto.");
        var targets = new List<VerseEntry>();
        if (CanSynchronize(source, target))
        {
            for (int v = verse.VerseStart!.Value; v <= (verse.VerseEnd ?? verse.VerseStart.Value); v++)
            { var mapped = ResolveVerse(target, verse.Book, verse.Chapter, v); if (mapped != null) targets.Add(mapped); }
        }
        else if (sourceMt)
        {
            foreach (var reference in verse.KjvReferences)
            {
                var mapped = ResolveVerse(target, reference.Book, reference.Chapter, reference.Verse);
                if (mapped == null) return new([], "Mapa de referência sem correspondência completa nesta edição.");
                targets.Add(mapped);
            }
        }
        else if (targetMt)
        {
            for (int v = verse.VerseStart!.Value; v <= (verse.VerseEnd ?? verse.VerseStart.Value); v++)
                if (target.FromKjv.TryGetValue((verse.Book, verse.Chapter, v), out var mapped)) targets.AddRange(mapped);
        }
        var unique = targets.DistinctBy(v => (v.Book, v.Chapter, v.EntryIndex)).OrderBy(v => v.Book).ThenBy(v => v.Chapter).ThenBy(v => v.EntryIndex).ToList();
        return new(unique, unique.Count == 0 ? "Sem correspondência explícita nesta edição; posição anterior preservada." : verse.Reference + " → " + string.Join("; ", unique.Select(v => v.Reference)) + (verse.KjvReferences.Any(r => r.Part.Length > 0) ? " (a fonte indica parte de versículo)" : ""));
    }

    public static VerseEntry? ResolveVerse(BibleEdition edition, int book, int chapter, int verse)
        => edition.Chapters.TryGetValue((book, chapter), out var entries)
            ? entries.FirstOrDefault(x => x.IsMapped && x.VerseStart <= verse && x.VerseEnd >= verse) : null;

    public static bool TryReference(BibleEdition edition, string query, out int book, out int chapter, out int? verse)
    {
        book = 0; chapter = 1; verse = null;
        var match = Regex.Match(query.Trim(), @"^(.+?)\s+(\d{1,3})(?:\s*[:.]\s*(\d{1,3}))?$");
        if (!match.Success) return false;
        string key = SearchText.Normalize(match.Groups[1].Value).Replace(" ", "").TrimEnd('.');
        book = edition.Books.FindIndex(b => SearchText.Normalize(b.Name).Replace(" ", "") == key || SearchText.Normalize(b.Abbrev).Replace(" ", "").TrimEnd('.') == key);
        if (book < 0) return false;
        chapter = int.Parse(match.Groups[2].Value);
        if (chapter < 1 || chapter > edition.Books[book].Chapters.Count) return false;
        if (match.Groups[3].Success)
        {
            verse = int.Parse(match.Groups[3].Value);
            if (ResolveVerse(edition, book, chapter, verse.Value) == null) return false;
        }
        return true;
    }
}
