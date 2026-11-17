namespace Verbum;

internal enum ReaderToolbarArrangement { Wide, Medium, Compact, Narrow }
internal static class ReaderToolbarLayout
{
    // Minimum usable widths of navigation (~140), selectors (~300), actions (~190).
    // Reflow whole groups rather than hide controls or shrink their click targets.
    public static ReaderToolbarArrangement ForWidth(double width) => width >= 720 ? ReaderToolbarArrangement.Wide
        : width >= 480 ? ReaderToolbarArrangement.Medium : width >= 350 ? ReaderToolbarArrangement.Compact : ReaderToolbarArrangement.Narrow;
}
