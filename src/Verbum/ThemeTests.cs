using System.Windows.Media;

namespace Verbum;

internal static class ThemeTests
{
    internal static void Run(Action<string, bool> check)
    {
        bool switched = false, rawPreserved = false, iconsValid = false;
        string? error = null;
        Action work = () =>
        {
            bool initial = Theme.IsDark;
            try
            {
                Theme.Apply(false, false);
                var surface = (SolidColorBrush)Theme.Surface; var text = (SolidColorBrush)Theme.Get("#2D3730");
                var light = surface.Color; var lightText = text.Color;
                var ink = (SolidColorBrush)Theme.Raw("#0000FF");
                Theme.Apply(true, false);
                switched = ReferenceEquals(surface, Theme.Surface) && surface.Color != light && text.Color != lightText && !surface.IsFrozen;
                rawPreserved = ink.Color == Colors.Blue;
                Theme.Apply(false, false);
                switched &= surface.Color == light && text.Color == lightText;
                foreach (string icon in new[] { "book-open", "library", "panel-left", "notebook-pen", "sun", "moon", "columns-3", "search", "settings-2", "layers", "languages" }) Icons.Create(icon);
                iconsValid = true;
            }
            catch (Exception ex) { error = ex.GetBaseException().Message; }
            finally { Theme.Apply(initial, false); }
        };
        System.Windows.Application.Current.Dispatcher.Invoke(work); bool finished = true;
        check("Theme switches existing brushes light dark light without recreating reader" + (error == null ? "" : " · " + error), finished && switched);
        check("Theme does not change custom text marker colors", finished && rawPreserved);
        check("All bundled Lucide icons load offline as WPF vectors", finished && iconsValid);
        static double L(Color c)
        {
            static double V(byte channel) { double n = channel / 255d; return n <= .04045 ? n / 12.92 : Math.Pow((n + .055) / 1.055, 2.4); }
            return .2126 * V(c.R) + .7152 * V(c.G) + .0722 * V(c.B);
        }
        static double Contrast(Color a, Color b) { double x = L(a), y = L(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
        check("Both themes provide readable body muted text and primary button contrast", new[] { false, true }.All(dark =>
            Contrast(Theme.ColorFor("#2D3730", dark), Theme.ColorFor("#FFFFFF", dark)) >= 7 &&
            Contrast(Theme.ColorFor("#768379", dark), Theme.ColorFor("#FFFFFF", dark)) >= 4.5 &&
            Contrast(Theme.ColorFor("OnPrimary", dark), Theme.ColorFor("Primary", dark)) >= 4.5));
    }
}
