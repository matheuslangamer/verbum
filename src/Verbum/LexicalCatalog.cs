using System.IO;
using System.Text.Json;

namespace Verbum;

public sealed class LexicalResource
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Attribution { get; set; } = "";
    public string License { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Revision { get; set; } = "";
    public string LocalDocument { get; set; } = "";
    public List<LexicalRecord> Entries { get; set; } = [];
    public override string ToString() => Title;
    public static List<LexicalResource> Load(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.json").Order().Select(path => JsonSerializer.Deserialize<LexicalResource>(File.ReadAllText(path)) ?? throw new InvalidDataException(path)).ToList() : [];
}
public sealed class LexicalRecord
{
    public string Id { get; set; } = "";
    public string Lemma { get; set; } = "";
    public List<string> Codes { get; set; } = [];
    public string Definition { get; set; } = "";
}
