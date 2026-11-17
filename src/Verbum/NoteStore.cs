using System.IO;
using System.Text.Json;

namespace Verbum;

internal sealed class NoteStore(string directory)
{
    private string PathFor(string id, bool trash = false, string extension = ".json")
    {
        if (id.Length is < 1 or > 64 || !id.All(char.IsAsciiLetterOrDigit)) throw new ArgumentException("Identidade da nota inválida.");
        return Path.Combine(directory, trash ? "lixeira" : "", id + extension);
    }
    internal bool IsTrashed(string id) => File.Exists(PathFor(id, true)) || File.Exists(PathFor(id, true, ".deleted"));
    internal void Purge(string id)
    {
        string path = PathFor(id, true);
        if (!File.Exists(path)) throw new InvalidOperationException("A nota já saiu da lixeira.");
        // Keep only the identity so a restored session backup cannot recreate the content.
        File.WriteAllText(PathFor(id, true, ".deleted"), "");
        foreach (bool trash in new[] { false, true })
        {
            File.Delete(PathFor(id, trash, ".md"));
            File.Delete(PathFor(id, trash, ".json.tmp"));
            File.Delete(PathFor(id, trash));
        }
    }
    internal void Trash(TabState note)
    {
        if (note.Kind != "notes") throw new ArgumentException("Escolha uma nota.");
        string path = PathFor(note.Id, true); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(note)); File.Move(path + ".tmp", path, true);
        File.WriteAllText(PathFor(note.Id, true, ".md"), note.Note);
        File.Delete(PathFor(note.Id)); File.Delete(PathFor(note.Id, false, ".md"));
    }
    internal List<TabState> Deleted()
    {
        string folder = Path.Combine(directory, "lixeira"); if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder, "*.json").Select(p => JsonSerializer.Deserialize<TabState>(File.ReadAllText(p))).OfType<TabState>().Where(n => n.Kind == "notes").OrderBy(n => n.NoteTitle).ToList();
    }
    internal TabState Restore(string id)
    {
        if (File.Exists(PathFor(id, true, ".deleted"))) throw new InvalidOperationException("Conclua a exclusão definitiva desta nota.");
        string path = PathFor(id, true); var note = JsonSerializer.Deserialize<TabState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Nota inválida.");
        if (note.Id != id || note.Kind != "notes") throw new InvalidDataException("A identidade da nota não corresponde ao arquivo.");
        Directory.CreateDirectory(directory); File.WriteAllText(PathFor(id) + ".tmp", JsonSerializer.Serialize(note)); File.Move(PathFor(id) + ".tmp", PathFor(id), true);
        File.WriteAllText(PathFor(id, false, ".md"), note.Note); File.Delete(path); File.Delete(PathFor(id, true, ".md")); return note;
    }
    internal void RemoveTrashedTabs(LayoutNode layout)
    {
        foreach (var pane in layout.Leaves()) { pane.Tabs.RemoveAll(t => t.Kind == "notes" && IsTrashed(t.Id)); if (!pane.Tabs.Any(t => t.Id == pane.ActiveTab)) pane.ActiveTab = pane.Tabs.FirstOrDefault()?.Id ?? ""; }
    }
}
