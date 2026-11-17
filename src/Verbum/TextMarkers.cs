using System.IO;
using System.Text.Json;

namespace Verbum;

public sealed record TextMarker(string Edition, int Book, int Chapter, int Entry, string Text, int Start, int Length, string Color, string Kind = "background");
public sealed class TextMarkers
{
    public static readonly Dictionary<string, string> Colors = new() { ["Amarelo"] = "#FFF09A", ["Verde"] = "#BDE8BC", ["Azul"] = "#BBDDF8", ["Rosa"] = "#F4BFDC", ["Laranja"] = "#FFD0A0" };
    public static readonly Dictionary<string, string> FontColors = new() { ["Azul"] = "#0000FF", ["Rosa"] = "#FF0066", ["Vermelho"] = "#FF0000", ["Verde"] = "#00FF00", ["Preto"] = "#000000", ["Roxo"] = "#800080", ["Laranja"] = "#FF8000" };
    public static bool ValidFontColor(string? color) => color != null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");
    private readonly string path;
    private List<TextMarker> marks;
    public TextMarkers(string path)
    {
        this.path = path;
        marks = File.Exists(path) ? JsonSerializer.Deserialize<List<TextMarker>>(File.ReadAllText(path)) ?? [] : [];
    }
    public List<TextMarker> For(VerseEntry verse) => marks.Where(m => m.Edition == verse.Edition && m.Book == verse.Book && m.Chapter == verse.Chapter && m.Entry == verse.EntryIndex && m.Text == verse.Text && m.Start >= 0 && m.Length > 0 && m.Start + m.Length <= verse.Text.Length && (m.Kind == "background" && Colors.ContainsValue(m.Color) || m.Kind == "foreground" && ValidFontColor(m.Color))).OrderBy(m => m.Start).ToList();
    public void Apply(IEnumerable<(VerseEntry Verse, int Start, int Length)> ranges, string? color, string kind = "background")
    {
        if (kind != "background" && kind != "foreground") throw new ArgumentException("Tipo de marcação inválido.");
        if (color != null && !(kind == "background" ? Colors.ContainsValue(color) : ValidFontColor(color))) throw new ArgumentException("Cor de marcador inválida.");
        var updated = marks.ToList();
        foreach (var (verse, start, length) in ranges)
        {
            if (start < 0 || length < 1 || start + length > verse.Text.Length) throw new ArgumentException("Seleção fora do texto.");
            int end = start + length;
            foreach (var old in updated.Where(m => m.Kind == kind && m.Edition == verse.Edition && m.Book == verse.Book && m.Chapter == verse.Chapter && m.Entry == verse.EntryIndex && m.Text == verse.Text && m.Start < end && m.Start + m.Length > start).ToList())
            {
                updated.Remove(old);
                if (old.Start < start) updated.Add(old with { Length = start - old.Start });
                if (old.Start + old.Length > end) updated.Add(old with { Start = end, Length = old.Start + old.Length - end });
            }
            if (color != null) updated.Add(new(verse.Edition, verse.Book, verse.Chapter, verse.EntryIndex, verse.Text, start, length, color, kind));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(updated));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".backup"); else File.Move(path + ".tmp", path);
        marks = updated;
    }
}
