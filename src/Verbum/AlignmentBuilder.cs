using System.IO;
using System.Text;

namespace Verbum;

/// <summary>Statistical word alignment (IBM Model 1 in both directions with a positional
/// prior) between OSHB words/segments and the tokens of a local translation. Learned only
/// from the two local texts; nothing is copied from another program's alignment data.</summary>
public static class AlignmentBuilder
{
    private sealed class Pair
    {
        public required List<VerseEntry> Sources;
        /// <summary>Global word index → source verse and word inside it.</summary>
        public required List<(int Source, int Word)> Owners;
        public required List<(VerseEntry Entry, SearchToken Token)> Tokens;
        public required List<(int Word, int Segment, int Key)> Units;
        public required int[] TokenKeys;
        public int WordCount;
    }

    private static readonly string[] Suffixes = ["issimos", "issimas", "amentos", "imentos", "amento", "imento", "assem", "essem", "issem", "ariam", "eriam", "iriam", "aremos", "eremos", "iremos", "mente", "aram", "eram", "iram", "avam", "asse", "esse", "isse", "ando", "endo", "indo", "ados", "adas", "idos", "idas", "aste", "este", "iste", "amos", "emos", "imos", "arei", "erei", "irei", "aras", "eras", "iras", "arao", "erao", "irao", "aria", "eria", "iria", "oes", "aes", "ais", "eis", "ara", "era", "ira", "ava", "iam", "ado", "ada", "ido", "ida", "ou", "eu", "iu", "ei", "ar", "er", "ir", "am", "em", "as", "es", "os", "a", "e", "o", "s"];
    private static readonly HashSet<string> Function = ["o", "a", "os", "as", "um", "uma", "uns", "umas", "de", "do", "da", "dos", "das", "em", "no", "na", "nos", "nas", "ao", "aos", "por", "pelo", "pela", "pelos", "pelas", "para", "com", "num", "numa", "dele", "dela", "deles", "delas"];
    /// <summary>Light Portuguese stem for matching inflected forms; not a linguistic lemma.</summary>
    public static string Stem(string normalized)
    {
        if (normalized.Length <= 4) return normalized;
        foreach (string suffix in Suffixes)
            if (normalized.Length - suffix.Length >= 3 && normalized.EndsWith(suffix, StringComparison.Ordinal)) return normalized[..^suffix.Length];
        return normalized;
    }

    /// <summary>Alignment units of one OSHB word: prefixes, the lemma-bearing segment and suffixes.</summary>
    internal static List<(int Segment, string Key)> Units(OriginalWord word)
    {
        string raw = word.Morphology;
        var parts = (raw.Length > 0 && raw[0] is 'H' or 'A' ? raw[1..] : raw).Split('/');
        var pieces = word.Segments.Count > 0 ? word.Segments.Select(s => s.Text).ToList() : [word.Text];
        string lemma = word.Strongs.Count > 0 ? word.Strongs[0] : "L:" + MorphologyLinks.Letters(word.Text);
        if (parts.Length != pieces.Count || raw.Length == 0) return [(-1, lemma)];
        int main = parts.Length - 1; while (main > 0 && parts[main].StartsWith('S')) main--;
        var units = new List<(int, string)>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (MorphologyLinks.Letters(pieces[i]).Length == 0) continue;
            string part = parts[i];
            units.Add((i, i == main ? lemma : i > main ? "S:" + part : part is "C" or "Td" or "Ti" ? "P:" + part : "P:" + (part.Length > 0 ? part[..1] : "") + ":" + MorphologyLinks.Letters(pieces[i])));
        }
        return units.Count > 0 ? units : [(-1, lemma)];
    }

    public static AlignmentFile Build(BibleEdition source, BibleEdition target, Dictionary<string, HashSet<string>>? glossWords = null, int iterations = 6)
    {
        var sourceIds = new Dictionary<string, int> { [""] = 0 }; var targetIds = new Dictionary<string, int> { [""] = 0 };
        var sourceNames = new List<string> { "" };
        int Id(Dictionary<string, int> ids, string key) { if (!ids.TryGetValue(key, out int id)) { ids[key] = id = ids.Count; if (ReferenceEquals(ids, sourceIds)) sourceNames.Add(key); } return id; }
        var pairs = new List<Pair>();
        // Source verses that share a translation entry (verse ranges, merged or split verses)
        // form one passage, so each translation word is aligned once against all of them.
        var groupSources = new List<VerseEntry>(); var groupTargets = new List<VerseEntry>();
        void Flush()
        {
            if (groupSources.Count == 0) return;
            var tokens = groupTargets.OrderBy(e => e.Book).ThenBy(e => e.Chapter).ThenBy(e => e.EntryIndex).SelectMany(entry => SearchText.Tokenize(entry.Text).Select(token => (entry, token))).ToList();
            var units = new List<(int, int, int)>(); var owners = new List<(int, int)>();
            for (int s = 0; s < groupSources.Count; s++)
                for (int w = 0; w < groupSources[s].Words.Count; w++)
                {
                    foreach (var unit in Units(groupSources[s].Words[w])) units.Add((owners.Count, unit.Segment, Id(sourceIds, unit.Key)));
                    owners.Add((s, w));
                }
            if (tokens.Count > 0 && units.Count > 0)
                pairs.Add(new() { Sources = [.. groupSources], Owners = owners, Tokens = tokens, Units = units, WordCount = owners.Count, TokenKeys = tokens.Select(t => Id(targetIds, Stem(t.token.Normalized))).ToArray() });
            groupSources.Clear(); groupTargets.Clear();
        }
        foreach (var verse in source.Entries)
        {
            if (verse.Words.Count == 0 || verse.IsSuperscription || !verse.IsMapped) continue;
            var link = BibleLibrary.ResolveLinks(source, verse, target);
            if (link.Targets.Count == 0) continue;
            if (!link.Targets.Any(groupTargets.Contains)) Flush();
            groupSources.Add(verse); foreach (var entry in link.Targets) if (!groupTargets.Contains(entry)) groupTargets.Add(entry);
        }
        Flush();

        // One parameter per co-occurring (unit, token) type, shared by both directions.
        var parameters = new Dictionary<long, int>();
        var grids = new int[pairs.Count][];
        for (int p = 0; p < pairs.Count; p++)
        {
            var pair = pairs[p]; int rows = pair.Units.Count + 1, columns = pair.TokenKeys.Length + 1; var grid = grids[p] = new int[rows * columns];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < columns; j++)
                {
                    if (i == 0 && j == 0) { grid[0] = -1; continue; }
                    long key = ((long)(i == 0 ? 0 : pair.Units[i - 1].Key) << 32) | (uint)(j == 0 ? 0 : pair.TokenKeys[j - 1]);
                    if (!parameters.TryGetValue(key, out int index)) parameters[key] = index = parameters.Count;
                    grid[i * columns + j] = index;
                }
        }
        int size = parameters.Count; var sourceOf = new int[size]; var targetOf = new int[size];
        foreach (var (key, index) in parameters) { sourceOf[index] = (int)(key >> 32); targetOf[index] = (int)(key & 0xFFFFFFFF); }
        var forward = new float[size]; var backward = new float[size]; Array.Fill(forward, 1f); Array.Fill(backward, 1f);
        var forwardCounts = new float[size]; var backwardCounts = new float[size];
        const double Tension = 4, NullPrior = .08;
        double Prior(Pair pair, int unit, int token) => Math.Exp(-Tension * Math.Abs((pair.Units[unit].Word + .5) / pair.WordCount - (token + .5) / pair.TokenKeys.Length));

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            Array.Clear(forwardCounts); Array.Clear(backwardCounts);
            for (int p = 0; p < pairs.Count; p++)
            {
                var pair = pairs[p]; var grid = grids[p]; int rows = pair.Units.Count + 1, columns = pair.TokenKeys.Length + 1;
                var weights = new double[Math.Max(rows, columns)];
                for (int j = 1; j < columns; j++)
                {   // each translation token chooses an original unit or nothing
                    double total = weights[0] = NullPrior * forward[grid[j]];
                    for (int i = 1; i < rows; i++) total += weights[i] = Prior(pair, i - 1, j - 1) * forward[grid[i * columns + j]];
                    if (total <= 0) continue;
                    for (int i = 0; i < rows; i++) forwardCounts[grid[i * columns + j]] += (float)(weights[i] / total);
                }
                for (int i = 1; i < rows; i++)
                {   // each original unit chooses a translation token or nothing
                    double total = weights[0] = NullPrior * backward[grid[i * columns]];
                    for (int j = 1; j < columns; j++) total += weights[j] = Prior(pair, i - 1, j - 1) * backward[grid[i * columns + j]];
                    if (total <= 0) continue;
                    for (int j = 0; j < columns; j++) backwardCounts[grid[i * columns + j]] += (float)(weights[j] / total);
                }
            }
            var sourceTotals = new double[sourceIds.Count]; var targetTotals = new double[targetIds.Count];
            for (int k = 0; k < size; k++) { sourceTotals[sourceOf[k]] += forwardCounts[k]; targetTotals[targetOf[k]] += backwardCounts[k]; }
            for (int k = 0; k < size; k++)
            {
                forward[k] = (float)((forwardCounts[k] + 1e-4) / (sourceTotals[sourceOf[k]] + 1e-2));
                backward[k] = (float)((backwardCounts[k] + 1e-4) / (targetTotals[targetOf[k]] + 1e-2));
            }
        }

        var file = new AlignmentFile
        {
            Translation = target.Info.Id, Source = source.Info.Id, Generated = DateTimeOffset.Now.ToString("yyyy-MM-dd"),
            TranslationBytes = File.Exists(target.Info.Path) ? new FileInfo(target.Info.Path).Length : 0,
            Method = "Alinhamento estatístico automático (IBM Modelo 1 nos dois sentidos, com posição relativa no versículo e glossas como reforço), calculado a partir dos textos locais. Não é um interlinear reverso revisado."
        };
        int linked = 0, strong = 0, medium = 0, weak = 0, tokenTotal = 0;
        for (int p = 0; p < pairs.Count; p++)
        {
            var pair = pairs[p]; var grid = grids[p]; int rows = pair.Units.Count + 1, columns = pair.TokenKeys.Length + 1; tokenTotal += columns - 1;
            bool Glossed(int unit, int token)
            {
                string key = sourceNames[pair.Units[unit].Key];
                return glossWords != null && key.Length > 1 && key[0] is 'H' or 'G' && char.IsDigit(key[1])
                    && glossWords.TryGetValue(OriginalResource.LexiconKey(key), out var words) && words.Contains(pair.Tokens[token].Token.Normalized);
            }
            // Posterior of each cell in both directions. Their product must be high for a link:
            // a rare original word cannot collect translation words that prefer another unit.
            var toUnit = new double[rows, columns]; var toToken = new double[rows, columns];
            for (int j = 1; j < columns; j++)
            {
                double total = NullPrior * forward[grid[j]];
                for (int i = 1; i < rows; i++) total += toUnit[i, j] = Prior(pair, i - 1, j - 1) * forward[grid[i * columns + j]] * (Glossed(i - 1, j - 1) ? 4 : 1);
                for (int i = 1; i < rows; i++) toUnit[i, j] = total > 0 ? toUnit[i, j] / total : 0;
            }
            for (int i = 1; i < rows; i++)
            {
                double total = NullPrior * backward[grid[i * columns]];
                for (int j = 1; j < columns; j++) total += toToken[i, j] = Prior(pair, i - 1, j - 1) * backward[grid[i * columns + j]] * (Glossed(i - 1, j - 1) ? 4 : 1);
                for (int j = 1; j < columns; j++) toToken[i, j] = total > 0 ? toToken[i, j] / total : 0;
            }
            var alignedVerses = new List<AlignedVerse>();
            foreach (var verse in pair.Sources)
            {
                var aligned = new AlignedVerse { B = verse.Book, C = verse.Chapter, V = verse.VerseStart!.Value };
                int consonants = 0;
                foreach (var word in verse.Words)
                {
                    int count = MorphologyLinks.Letters(word.Text).Length;
                    aligned.W.Add(new() { T = word.Segments.Count > 1 ? string.Join("/", word.Segments.Select(s => s.Text)) : word.Text, L = word.Lemma, S = string.Join(" ", word.Strongs), M = word.Morphology, O = consonants, N = count });
                    consonants += count;
                }
                alignedVerses.Add(aligned);
            }
            var unitOf = new int[columns]; var confidenceOf = new int[columns];
            for (int j = 1; j < columns; j++)
            {
                double best = 0; int unit = 0;
                for (int i = 1; i < rows; i++) { double score = Math.Sqrt(toUnit[i, j] * toToken[i, j]); if (score > best) { best = score; unit = i; } }
                if (unit == 0) continue;
                bool glossed = Glossed(unit - 1, j - 1);
                int confidence = best >= .5 || glossed && best >= .3 ? 3 : best >= .25 ? 2 : best >= .12 ? 1 : 0;
                if (confidence > 0) { unitOf[j] = unit; confidenceOf[j] = confidence; }
            }
            // Articles and prepositions translate no separate word of their own in most cases:
            // they follow the next linked word when their best unit is part of that same word.
            for (int j = columns - 2; j >= 1; j--)
            {
                if (unitOf[j] != 0 || !Function.Contains(pair.Tokens[j - 1].Token.Normalized) || pair.Tokens[j - 1].Entry != pair.Tokens[j].Entry) continue;
                int next = unitOf[j + 1]; if (next == 0) continue;
                int word = pair.Units[next - 1].Word, unit = 0; double best = .1;
                for (int i = 1; i < rows; i++) if (pair.Units[i - 1].Word == word && toUnit[i, j] > best) { best = toUnit[i, j]; unit = i; }
                if (unit != 0) { unitOf[j] = unit; confidenceOf[j] = Math.Min(confidenceOf[j + 1], 2); }
            }
            for (int j = 1; j < columns; j++)
            {
                if (unitOf[j] == 0) continue;
                var (entry, token) = pair.Tokens[j - 1]; int confidence = confidenceOf[j];
                var owner = pair.Owners[pair.Units[unitOf[j] - 1].Word];
                alignedVerses[owner.Source].L.Add([entry.Book, entry.Chapter, entry.EntryIndex, token.Start, token.Length, owner.Word, pair.Units[unitOf[j] - 1].Segment, confidence]);
                linked++; if (confidence == 3) strong++; else if (confidence == 2) medium++; else weak++;
            }
            file.Verses.AddRange(alignedVerses);
        }
        file.Stats = new() { ["versiculos"] = file.Verses.Count, ["passagens"] = pairs.Count, ["palavrasDaTraducao"] = tokenTotal, ["ligadas"] = linked, ["provaveis"] = strong, ["plausiveis"] = medium, ["incertas"] = weak };
        return file;
    }

    /// <summary>Readable sample for review: translation word → original segment.</summary>
    public static string Sample(AlignmentFile file, BibleEdition target, IEnumerable<(int Book, int Chapter, int Verse)> references)
    {
        var text = new StringBuilder();
        text.AppendLine(file.Translation + " ↔ " + file.Source + " · " + file.Method).AppendLine(string.Join(" · ", file.Stats.Select(s => s.Key + ": " + s.Value))).AppendLine();
        foreach (var reference in references)
        {
            var verse = file.Verses.FirstOrDefault(v => v.B == reference.Book && v.C == reference.Chapter && v.V == reference.Verse);
            if (verse == null) continue;
            text.AppendLine(BibleLibrary.CanonicalNames[verse.B] + " " + verse.C + ":" + verse.V + " (MT)  " + string.Join(" ", verse.W.Select(w => w.Text)));
            foreach (var link in verse.L.OrderBy(l => l[2]).ThenBy(l => l[3]))
            {
                if (!target.Chapters.TryGetValue((link[0], link[1]), out var entries) || link[2] >= entries.Count) continue;
                var word = verse.W[link[5]];
                text.AppendLine("   " + entries[link[2]].Text.Substring(link[3], link[4]).PadRight(18) + " → " + (link[6] >= 0 && link[6] < word.Segments.Length ? word.Segments[link[6]] + "  [" + word.Text + "]" : word.Text) + "  " + word.S + "  " + new string('●', link[7]));
            }
            text.AppendLine();
        }
        return text.ToString();
    }

    /// <summary>Command line: --build-alignment ARA,NAA --library PATH.</summary>
    public static int Run(string libraryPath, string translations)
    {
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(libraryPath))!, "alinhamento");
        Directory.CreateDirectory(folder);
        try
        {
            var library = new BibleLibrary(libraryPath);
            var source = library.LoadAsync("WLC-OSHB").GetAwaiter().GetResult();
            Dictionary<string, HashSet<string>>? glosses = null;
            try { glosses = library.Parsing.GetAwaiter().GetResult().GlossWords(); } catch { }
            foreach (string id in translations.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var target = library.LoadAsync(id).GetAwaiter().GetResult();
                if (target.Resource != null) throw new InvalidOperationException(id + " é um texto original; escolha uma tradução.");
                var file = Build(source, target, glosses);
                TranslationAlignment.Save(TranslationAlignment.PathFor(libraryPath, target.Info.Id), file);
                (int, int, int)[] sample = [(0, 1, 1), (0, 1, 2), (0, 1, 3), (0, 12, 1), (1, 20, 2), (4, 6, 4), (4, 6, 5), (8, 17, 45), (18, 23, 1), (18, 51, 3), (22, 53, 5), (28, 3, 1), (31, 2, 1)];
                File.WriteAllText(Path.Combine(folder, target.Info.Id + ".amostra.txt"), Sample(file, target, sample));
            }
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "erro.txt"), ex.ToString()); return 1; }
    }
}
