using System.Globalization;
using System.Text;

namespace Verbum;

/// <summary>Occurrence alignment only: same MT verse, same consonants at the aligned
/// position, complete OSHB segment boundaries and the same vowel points. Cantillation,
/// meteg and the placement of holam on or before waw are encoding differences and are ignored.</summary>
public static class MorphologyLinks
{
    public static int Enrich(BibleEdition target, BibleEdition oshb)
    {
        int count = 0;
        foreach (var verse in target.Entries)
        {
            if (!verse.VerseStart.HasValue) continue;
            var source = BibleLibrary.ResolveVerse(oshb, verse.Book, verse.Chapter, verse.VerseStart.Value);
            if (source == null) continue;
            string own = Letters(verse.Text), other = Letters(source.Text);
            // Ketiv/qere and spelling differences change a few words only: align the rest.
            int[]? map = own == other ? null : Map(own, other);
            var segments = new List<(int Start, int End, string Text, string Morph, char Language, OriginalWord Word, int Index)>();
            int offset = 0;
            foreach (var word in source.Words)
            {
                string raw = word.Morphology;
                char language = raw.StartsWith('A') ? 'A' : 'H';
                var parts = (raw.Length > 0 && raw[0] is 'H' or 'A' ? raw[1..] : raw).Split('/');
                var pieces = word.Segments.Count > 0 ? word.Segments : [new OriginalSegment(word.Text, 0)];
                foreach (var (piece, index) in pieces.Select((s, i) => (s, i)))
                {
                    int length = Letters(piece.Text).Length;
                    segments.Add((offset, offset + length, piece.Text, parts.Length == pieces.Count ? parts[index] : "", language, word, index));
                    offset += length;
                }
            }
            offset = 0; int cursor = 0; var matches = new List<(int Start, int End, string Text, string Morph, char Language, OriginalWord Word, int Index)>();
            foreach (var word in verse.Words)
            {
                int length = Letters(word.Text).Length, begin = offset; offset += length;
                if (length == 0 || begin + length > own.Length) continue;
                int start = map == null ? begin : map[begin];
                if (start < 0) continue;
                bool contiguous = true;
                for (int k = 1; map != null && k < length && contiguous; k++) contiguous = map[begin + k] == start + k;
                if (!contiguous) continue;
                int end = start + length;
                word.ConsonantStart = start;
                // Both lists advance in verse order, so the segments are scanned once per verse.
                while (cursor < segments.Count && segments[cursor].End <= start) cursor++;
                matches.Clear();
                for (int k = cursor; k < segments.Count && segments[k].Start < end; k++)
                    if (segments[k].Start >= start && segments[k].End <= end && segments[k].End > segments[k].Start) matches.Add(segments[k]);
                if (matches.Count == 0 || matches[0].Start != start || matches[^1].End != end) continue;
                if (matches.All(s => s.Word == matches[0].Word)) { word.Whole = matches[0].Word; word.WholeSegment = matches[0].Index; word.WholeSegmentCount = matches.Count; }
                if (word.Morphology.Length == 0 && matches.All(s => s.Morph.Length > 0 && s.Language == matches[0].Language)
                    && PointKey(string.Concat(matches.Select(s => s.Text))) == PointKey(word.Text))
                {
                    word.Morphology = matches[0].Language + string.Join("/", matches.Select(s => s.Morph));
                    word.MorphologySource = "OSHB · " + source.Reference + " · " + string.Join(", ", matches.Select(s => s.Word.SourceId).Distinct()) + "; alinhamento por referência MT, posição consonantal e pontos vocálicos.";
                    count++;
                }
            }
        }
        return count;
    }

    internal static string Letters(string text)
    {
        // Pointed Hebrew is the common case: keep the consonants without normalizing the string.
        int count = 0;
        foreach (char c in text)
        {
            if (c is >= 'א' and <= 'ת') count++;
            else if (c >= 'יִ' || char.IsLetter(c)) return string.Concat(SearchText.Normalize(text).Where(char.IsLetter));
        }
        if (count == text.Length) return text;
        return string.Create(count, text, static (span, source) => { int i = 0; foreach (char c in source) if (c is >= 'א' and <= 'ת') span[i++] = c; });
    }

    /// <summary>Consonants plus the set of vowel points, dagesh and shin/sin dots. The points are
    /// compared as a multiset so that holam written on waw or on the preceding consonant matches.</summary>
    internal static string PointKey(string text)
    {
        var letters = new StringBuilder(); var marks = new List<char>();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (c is >= '֑' and <= '֯' or 'ֽ' or '͏') continue;
            if (char.IsLetter(c)) letters.Append(c);
            else if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark) marks.Add(c == 'ֺ' ? 'ֹ' : c == 'ׇ' ? 'ָ' : c);
        }
        marks.Sort();
        return letters.Append('|').Append(marks.ToArray()).ToString();
    }

    /// <summary>Longest common subsequence: index in <paramref name="a"/> → index in <paramref name="b"/>, or −1.</summary>
    internal static int[] Map(string a, string b)
    {
        int n = a.Length, m = b.Length; var table = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                table[i, j] = a[i] == b[j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
        var map = new int[n]; Array.Fill(map, -1);
        for (int i = 0, j = 0; i < n && j < m;)
        {
            if (a[i] == b[j]) { map[i++] = j++; }
            else if (table[i + 1, j] >= table[i, j + 1]) i++;
            else j++;
        }
        return map;
    }
}
