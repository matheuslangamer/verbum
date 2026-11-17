using System.IO;
using System.Text.Json;

namespace Verbum;

public sealed record TranslationLink(string QueryKey, string SourceEdition, int Book, int Chapter, int EntryIndex,
    string SourceText, string Translation, int TargetBook, int TargetChapter, int TargetVerse, string TargetText, string Phrase, int Start);

public sealed class TranslationLinks
{
    private readonly string path;
    private List<TranslationLink> links = [];
    public TranslationLinks(string path)
    {
        this.path = path;
        if (File.Exists(path)) links = JsonSerializer.Deserialize<List<TranslationLink>>(File.ReadAllText(path)) ?? [];
    }
    public TranslationLink? Find(string queryKey, VerseEntry source, string translation)
        => links.FirstOrDefault(l => l.QueryKey == queryKey && l.SourceEdition == source.Edition && l.Book == source.Book && l.Chapter == source.Chapter && l.EntryIndex == source.EntryIndex && l.SourceText == source.Text && l.Translation == translation);
    public static bool Valid(TranslationLink link, VerseEntry target) => target.Book == link.TargetBook && target.Chapter == link.TargetChapter && target.VerseStart == link.TargetVerse && target.Text == link.TargetText && link.Start >= 0 && link.Start + link.Phrase.Length <= target.Text.Length && target.Text.Substring(link.Start, link.Phrase.Length) == link.Phrase;
    public void Save(string queryKey, VerseEntry source, VerseEntry target, string phrase, int start)
    {
        if (!target.VerseStart.HasValue || phrase.Length == 0 || start < 0 || start + phrase.Length > target.Text.Length || target.Text.Substring(start, phrase.Length) != phrase) throw new ArgumentException("Selecione uma expressão existente no versículo paralelo.");
        var link = new TranslationLink(queryKey, source.Edition, source.Book, source.Chapter, source.EntryIndex, source.Text, target.Edition, target.Book, target.Chapter, target.VerseStart.Value, target.Text, phrase, start);
        var updated = links.Where(l => !(l.QueryKey == queryKey && l.SourceEdition == source.Edition && l.Book == source.Book && l.Chapter == source.Chapter && l.EntryIndex == source.EntryIndex && l.Translation == target.Edition)).Append(link).ToList();
        Persist(updated);
    }
    public void Remove(string queryKey, VerseEntry source, string translation)
    {
        Persist(links.Where(l => !(l.QueryKey == queryKey && l.SourceEdition == source.Edition && l.Book == source.Book && l.Chapter == source.Chapter && l.EntryIndex == source.EntryIndex && l.Translation == translation)).ToList());
    }
    private void Persist(List<TranslationLink> updated)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string json = JsonSerializer.Serialize(updated, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path + ".tmp", json);
        if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup"); else File.Move(path + ".tmp", path);
        links = updated;
    }
    public static string Key(string query, SearchMode mode) => ((int)mode) + ":" + (mode == SearchMode.Exact ? query.Normalize().ToLowerInvariant() : SearchText.Normalize(query));
}
