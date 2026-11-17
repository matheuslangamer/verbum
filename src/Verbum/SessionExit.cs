using System.Text.Json;
using System.Windows;

namespace Verbum;

internal static class SessionExit
{
    public static StudySession Clone(StudySession session) => JsonSerializer.Deserialize<StudySession>(JsonSerializer.Serialize(session))!;
    public static bool Complete(MessageBoxResult choice, Func<bool> saveCurrent, Func<bool> savePrevious) => choice switch
    {
        MessageBoxResult.Yes => saveCurrent(),
        MessageBoxResult.No => savePrevious(),
        _ => false
    };
    public static void PreserveNotes(StudySession previous, IEnumerable<TabState> latest)
    {
        var notes = latest.Where(n => n.Kind == "notes").GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.Last());
        foreach (var tab in previous.Layout.Leaves().SelectMany(p => p.Tabs).Where(t => t.Kind == "notes"))
            if (notes.TryGetValue(tab.Id, out var note)) { tab.Note = note.Note; tab.NoteTitle = note.NoteTitle; tab.NoteColor = note.NoteColor; tab.NoteCreated = note.NoteCreated; tab.NoteEdited = note.NoteEdited; tab.NoteTags = [..note.NoteTags]; }
    }
}
