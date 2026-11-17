using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Verbum;

internal static class NoteGraph
{
    internal static IEnumerable<string> Tags(string markdown)
    {
        bool code = false;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```")) { code = !code; continue; }
            if (code) continue;
            var text = Regex.Replace(NoteColors.StripTags(NoteQuote.Unwrap(line, out _)), @"`[^`]*`|\]\([^)]*\)|https?://\S+", " ");
            foreach (Match match in Regex.Matches(text, @"(?<![\\\p{L}\p{M}\d_/#])#[\p{L}\p{Nd}_][\p{L}\p{M}\p{Nd}_/-]*"))
                yield return match.Value.Normalize().ToLowerInvariant();
        }
    }

    internal static Dictionary<string, Point> Positions(List<string> ids, List<(string From, string To)> edges, double size)
    {
        var random = new Random(43);
        var points = ids.Select(_ => new Point(size * (.25 + .5 * random.NextDouble()), size * (.25 + .5 * random.NextDouble()))).ToArray();
        var index = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var pairs = edges.Where(e => e.From != e.To).Select(e => (index[e.From], index[e.To])).ToList();
        double ideal = size / Math.Sqrt(Math.Max(2, ids.Count)) * .58;
        for (int step = 0; step < 100; step++)
        {
            var forces = new Vector[points.Length];
            for (int i = 0; i < points.Length; i++)
            for (int j = 0; j < i; j++)
            {
                var diff = points[i] - points[j];
                var repel = diff * (ideal * ideal / Math.Max(1, diff.LengthSquared));
                forces[i] += repel; forces[j] -= repel;
            }
            foreach (var (a, b) in pairs)
            {
                var diff = points[a] - points[b];
                var pull = diff * (diff.Length / ideal);
                forces[a] -= pull; forces[b] += pull;
            }
            for (int i = 0; i < points.Length; i++)
            {
                var force = forces[i] + (new Point(size / 2, size / 2) - points[i]) * .6;
                var p = points[i] + force * Math.Min(1, (1 + (100 - step) * .16) / Math.Max(1, force.Length));
                points[i] = new(Math.Clamp(p.X, 70, size - 70), Math.Clamp(p.Y, 25, size - 40));
            }
        }
        return index.ToDictionary(x => x.Key, x => points[x.Value]);
    }

    internal static HashSet<(string From, string To)> Edges(IEnumerable<TabState> notes)
    {
        var all = notes.ToList(); var ids = all.Select(n => n.Id).ToHashSet(); var edges = new HashSet<(string, string)>();
        foreach (var note in all)
        {
            bool code = false;
            foreach (string line in note.Note.Split('\n'))
            {
                if (line.TrimStart().StartsWith("```")) { code = !code; continue; } if (code) continue;
                string text = Regex.Replace(line, "`[^`]*`", "");
                foreach (Match match in Regex.Matches(text, @"(?<![\\!])\[(?:\\.|[^\]])*\]\((verbum://note/[A-Za-z0-9]+)\)"))
                    if (NoteLinks.TryParse(match.Groups[1].Value, out var target) && target?.Kind == "note" && ids.Contains(target.Id)) edges.Add((note.Id, target.Id));
            }
        }
        return edges;
    }
}

internal sealed class NoteGraphView : UserControl
{
    internal NoteGraphView(List<TabState> notes, Action<TabState> open)
    {
        var root = new DockPanel(); Content = root;
        var tools = new WrapPanel { Margin = new(0, 5, 0, 8) }; var find = new TextBox { Width = 180, ToolTip = "Filtrar notas e seus vizinhos no grafo" }; tools.Children.Add(find);
        tools.Children.Add(MainWindow.Label(" Zoom ", 12)); var zoom = new Slider { Minimum = .5, Maximum = 2, Value = 1, Width = 110, VerticalAlignment = VerticalAlignment.Center }; tools.Children.Add(zoom);
        var summary = MainWindow.Label("", 11, MainWindow.Muted); tools.Children.Add(summary); DockPanel.SetDock(tools, Dock.Top); root.Children.Add(tools);
        var canvas = new Canvas { Background = Theme.Surface }; var scale = new ScaleTransform(); canvas.LayoutTransform = scale;
        root.Children.Add(new ScrollViewer { Content = canvas, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var edges = NoteGraph.Edges(notes);
        var labels = notes.ToDictionary(n => n.Id, n => n.NoteTitle);
        foreach (var note in notes)
        foreach (var tag in NoteGraph.Tags(note.Note).Concat(note.NoteTags).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            labels[tag] = tag; edges.Add((note.Id, tag));
        }
        var nodeTemplate = new ControlTemplate(typeof(Button)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        var lookup = notes.ToDictionary(n => n.Id);
        void Draw()
        {
            string query = SearchText.Normalize(find.Text);
            string tagFilter = find.Text.Trim().Normalize().ToLowerInvariant();
            bool exactTag = tagFilter.StartsWith('#') && labels.ContainsKey(tagFilter);
            var selected = labels.Where(n => exactTag ? n.Key == tagFilter : query.Length == 0 || SearchText.Normalize(n.Value).Contains(query)).Select(n => n.Key).ToHashSet();
            if (query.Length > 0) foreach (var edge in edges.Where(e => selected.Contains(e.From) || selected.Contains(e.To)).ToList()) { selected.Add(edge.From); selected.Add(edge.To); }
            var visible = selected.OrderByDescending(id => query.Length > 0 && SearchText.Normalize(labels[id]).Contains(query)).Take(120).ToList();
            double size = Math.Max(360, Math.Sqrt(visible.Count) * 145);
            canvas.Width = canvas.Height = size; canvas.Children.Clear();
            var shown = visible.ToHashSet();
            var links = edges.Where(e => shown.Contains(e.From) && shown.Contains(e.To)).ToList();
            var positions = NoteGraph.Positions(visible, links, size);
            foreach (var edge in links)
            {
                var a = positions[edge.From]; var b = positions[edge.To]; canvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = MainWindow.Green, Opacity = .4, StrokeThickness = 1.5, ToolTip = "Vínculo entre notas" });
            }
            foreach (var id in visible)
            {
                bool tag = id.StartsWith('#');
                double diameter = 10 + Math.Min(14, links.Count(e => e.To == id || e.From == id) * 2);
                var node = new StackPanel { Width = 134, Background = Brushes.Transparent };
                node.Children.Add(new Ellipse { Width = diameter, Height = diameter, Fill = tag ? Theme.Raw("#BC813F") : TextMarkers.ValidFontColor(lookup[id].NoteColor) ? Theme.Raw(lookup[id].NoteColor) : MainWindow.Green });
                node.Children.Add(new TextBlock { Text = labels[id], TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = MainWindow.Muted, FontSize = 11 });
                var button = new Button { Content = node, ToolTip = labels[id], Padding = new(0), BorderThickness = new(0), Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
                button.Template = nodeTemplate;
                button.Click += (_, _) => { if (tag) find.Text = id; else open(lookup[id]); };
                Canvas.SetLeft(button, positions[id].X - 67); Canvas.SetTop(button, positions[id].Y - diameter / 2); canvas.Children.Add(button);
            }
            summary.Text = $" {visible.Count} nós · {links.Count} vínculos" + (selected.Count > 120 ? " · limite de 120: refine o filtro" : "");
            if (notes.Count == 0) canvas.Children.Add(MainWindow.Label("Crie notas e conecte-as com Link… para formar seu grafo.", 16, MainWindow.Muted));
        }
        find.TextChanged += (_, _) => Draw(); zoom.ValueChanged += (_, _) => scale.ScaleX = scale.ScaleY = zoom.Value; Draw();
    }
}
