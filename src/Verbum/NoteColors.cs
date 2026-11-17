using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Verbum;

internal static class NoteColors
{
    internal const string Pattern = @"<mark style=""background-color:(?<bg>#[0-9A-Fa-f]{6})"">(?<highlight>.*?)</mark>|<span style=""color:(?<fg>#[0-9A-Fa-f]{6})"">(?<ink>.*?)</span>|==(?<defaultMark>.+?)==";
    internal static string StripTags(string text) => Regex.Replace(text, @"<(?:mark style=""background-color:|span style=""color:)#[0-9A-Fa-f]{6}"">|</(?:mark|span)>", "");
    internal static string Wrap(string text, string color, bool background) => background
        ? $"<mark style=\"background-color:{color}\">{text}</mark>"
        : $"<span style=\"color:{color}\">{text}</span>";

    internal static string Serialize(TextElement element, string value)
    {
        if (element.ReadLocalValue(TextElement.ForegroundProperty) is SolidColorBrush ink && ink.Color.A == 255 && !System.Windows.Data.BindingOperations.IsDataBound(ink, SolidColorBrush.ColorProperty))
            value = Wrap(value, $"#{ink.Color.R:X2}{ink.Color.G:X2}{ink.Color.B:X2}", false);
        if (element.ReadLocalValue(TextElement.BackgroundProperty) is SolidColorBrush bg && bg.Color.A == 255 && !System.Windows.Data.BindingOperations.IsDataBound(bg, SolidColorBrush.ColorProperty))
            value = Wrap(value, $"#{bg.Color.R:X2}{bg.Color.G:X2}{bg.Color.B:X2}", true);
        return value;
    }

    internal static Button Palette(string label, Dictionary<string,string> colors, Action<string> apply, Action? reset = null, Action? beforeOpen = null)
    {
        var menu = new ContextMenu();
        foreach (var (name, color) in colors)
        {
            var item = new MenuItem { Header = name, Icon = new Border { Width = 14, Height = 14, Background = Theme.Raw(color), CornerRadius = new(3) } };
            item.Click += (_, _) => apply(color); menu.Items.Add(item);
        }
        if (reset != null)
        {
            var clear = new MenuItem { Header = "Padrão" }; clear.Click += (_, _) => reset();
            menu.Items.Add(new Separator()); menu.Items.Add(clear);
        }
        var button = new Button { Content = label + " ▾", ContextMenu = menu, Padding = new(7, 2, 7, 2), Height = 28, Margin = new(1), Focusable = false };
        button.Click += (_, _) => { beforeOpen?.Invoke(); menu.PlacementTarget = button; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true; };
        return button;
    }
}