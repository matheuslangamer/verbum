using System.Text;
internal static class NativeReferences
{
    public static Dictionary<int, string> Feature(string path)
    {
        var result = new Dictionary<int, string>(); int next = 1; bool body = false;
        foreach (var line in File.ReadLines(path))
        {
            if ((line.Length == 0 && !body) || line.StartsWith('@')) continue;
            body = true;
            var cols = line.Split('\t'); int node = cols.Length == 2 ? int.Parse(cols[0]) : next;
            result[node] = cols[^1]; next = node + 1;
        }
        return result;
    }
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormD).Where(char.IsLetter));
    public static Dictionary<int, (int Chapter, int Verse)> Load(string directory)
    {
        var chapters = Feature(Path.Combine(directory, "chapter.tf")).Where(kv => kv.Key is >= 426624 and <= 427552).ToDictionary(); var verses = Feature(Path.Combine(directory, "verse.tf"));
        var chapterSlots = new Dictionary<int, int>(); var verseSlots = new Dictionary<int, int>(); int next = 426585;
        foreach (var line in File.ReadLines(Path.Combine(directory, "oslots.tf")))
        {
            if (line.Length == 0 || line[0] == '@') continue;
            var cols = line.Split('\t'); int node = cols.Length == 2 ? int.Parse(cols[0]) : next; next = node + 1;
            var target = chapters.ContainsKey(node) ? chapterSlots : verses.ContainsKey(node) ? verseSlots : null;
            if (target == null) continue;
            int value = int.Parse(chapters.GetValueOrDefault(node) ?? verses[node]);
            foreach (var range in cols[^1].Split(','))
            {
                var bounds = range.Split('-'); int start = int.Parse(bounds[0]), end = int.Parse(bounds[^1]);
                for (int slot = start; slot <= end; slot++) target.Add(slot, value);
            }
        }
        return verseSlots.ToDictionary(kv => kv.Key, kv => (chapterSlots[kv.Key], kv.Value));
    }
}
