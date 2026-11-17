namespace Verbum;

public enum DockRegion { Center, Left, Right, Top, Bottom }

public static class StudyLayout
{
    public static DockRegion Region(double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0) return DockRegion.Center;
        double left = x / width, right = 1 - left, top = y / height, bottom = 1 - top;
        double nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (nearest > .22) return DockRegion.Center;
        return nearest == left ? DockRegion.Left : nearest == right ? DockRegion.Right : nearest == top ? DockRegion.Top : DockRegion.Bottom;
    }

    public static bool Dock(ref LayoutNode root, string tabId, string targetId, DockRegion region, out string active)
    {
        active = targetId;
        var source = root.Leaves().FirstOrDefault(p => p.Tabs.Any(t => t.Id == tabId));
        var target = root.Leaves().FirstOrDefault(p => p.Id == targetId);
        var tab = source?.Tabs.FirstOrDefault(t => t.Id == tabId);
        if (source == null || target == null || tab == null) return false;
        if (source == target && (region == DockRegion.Center || source.Tabs.Count == 1)) return false;
        if (region != DockRegion.Center && root.Leaves().Count() >= 8 && source.Tabs.Count > 1) return false;
        source.Tabs.Remove(tab);
        if (source.ActiveTab == tab.Id) source.ActiveTab = source.Tabs.FirstOrDefault()?.Id;
        if (region == DockRegion.Center) { target.Tabs.Add(tab); target.ActiveTab = tab.Id; }
        else
        {
            var previous = new LayoutNode { Tabs = target.Tabs, ActiveTab = target.ActiveTab };
            var added = new LayoutNode { Tabs = [tab], ActiveTab = tab.Id };
            target.Kind = "split"; target.Direction = region is DockRegion.Left or DockRegion.Right ? "horizontal" : "vertical";
            target.Ratio = .5; target.Tabs = []; target.ActiveTab = null;
            target.Children = region is DockRegion.Left or DockRegion.Top ? [added, previous] : [previous, added];
            active = added.Id;
        }
        root = Prune(root);
        return true;
    }
    private static LayoutNode Prune(LayoutNode node)
    {
        if (node.Kind == "pane") return node;
        node.Children = node.Children.Select(Prune).ToList();
        if (node.Children[0].Kind == "pane" && node.Children[0].Tabs.Count == 0) return node.Children[1];
        if (node.Children[1].Kind == "pane" && node.Children[1].Tabs.Count == 0) return node.Children[0];
        return node;
    }
}

public sealed record BookFilter(string Title, int Book = -1)
{
    public override string ToString() => Title;
    public bool Matches(SearchHit hit) => Book switch { -1 => true, -2 => hit.Verse.Book < 39, -3 => hit.Verse.Book >= 39, _ => hit.Verse.Book == Book };
}

public sealed record ResultSpan(string Text, bool Match);
public static class SearchPresentation
{
    public static List<ResultSpan> Spans(SearchHit hit)
    {
        var spans = new List<ResultSpan>(); int cursor = 0;
        foreach (var token in hit.Highlights.OrderBy(h => h.Start))
        {
            int start = Math.Max(cursor, Math.Clamp(token.Start, 0, hit.Verse.Text.Length));
            int end = Math.Clamp(token.Start + token.Length, start, hit.Verse.Text.Length);
            if (start > cursor) spans.Add(new(hit.Verse.Text[cursor..start], false));
            if (end > start) spans.Add(new(hit.Verse.Text[start..end], true));
            cursor = end;
        }
        if (cursor < hit.Verse.Text.Length) spans.Add(new(hit.Verse.Text[cursor..], false));
        return spans;
    }
}
