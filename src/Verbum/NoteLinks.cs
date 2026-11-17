using System.Text.RegularExpressions;

namespace Verbum;

public sealed record NoteLink(string Label, string Target);
public sealed record NoteDestination(string Kind, string Id, string Reference = "", int Section = 0);
public static class NoteLinks
{
    public static string Bible(string edition, string reference) => "verbum://bible?edition=" + Uri.EscapeDataString(edition) + "&reference=" + Uri.EscapeDataString(reference);
    public static string Note(string id) => "verbum://note/" + Uri.EscapeDataString(id);
    public static string Book(string id, int section) => "verbum://book/" + Uri.EscapeDataString(id) + "?section=" + section;
    public static bool TryParse(string target, out NoteDestination? destination)
    {
        destination = null;
        if (target.Length > 2000 || !Uri.TryCreate(target, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is "https" or "http") { destination = new("web", target); return true; }
        if (uri.Scheme != "verbum" || uri.UserInfo.Length > 0 || !uri.IsDefaultPort || uri.Fragment.Length > 0) return false;
        try
        {
            var args = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length == 2 ? Uri.UnescapeDataString(p[1]) : "");
            string id = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
            if (uri.Host == "bible" && id.Length == 0 && args.TryGetValue("edition", out var edition) && edition.Length is > 0 and < 100 && args.TryGetValue("reference", out var reference) && reference.Length is > 0 and < 250)
                destination = new("bible", edition, reference);
            if (Regex.IsMatch(target, "^verbum://word/[A-Za-z0-9]{1,64}$", RegexOptions.IgnoreCase)) destination = new("word", id);
            if (uri.Host == "note" && Regex.IsMatch(target, "^verbum://note/[A-Za-z0-9]{1,64}$", RegexOptions.IgnoreCase)) destination = new("note", id);
            if (uri.Host == "book" && Regex.IsMatch(target, @"^verbum://book/[A-Za-z0-9_-]{1,128}\?section=\d+$", RegexOptions.IgnoreCase) && args.TryGetValue("section", out var value) && int.TryParse(value, out int section) && section >= 0) destination = new("book", id, Section: section);
            return destination != null;
        }
        catch (ArgumentException) { return false; }
    }
    public static string Markdown(string label, string target) => "[" + label.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]").Replace("\r", " ").Replace("\n", " ") + "](" + target.Replace("(", "%28").Replace(")", "%29").Replace(" ", "%20") + ")";
}
