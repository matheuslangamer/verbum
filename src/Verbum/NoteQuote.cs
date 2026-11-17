using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Verbum;

internal static class NoteQuote
{
    internal static string Prefix(string? color) => color == null ? "> " : $"> <!--quote:{color}--> ";
    internal static string Unwrap(string line, out string? color)
    {
        var match = Regex.Match(line, @"^>\s?(?:<!--quote:(#[\da-fA-F]{6})-->\s?)?");
        color = match.Groups[1].Success ? match.Groups[1].Value : null;
        return line[match.Length..];
    }
    internal static void Apply(Paragraph p, string? color)
    {
        p.Margin = new(0, 2, 0, 10); p.Padding = new(16, 0, 0, 0);
        p.BorderThickness = new(2, 0, 0, 0);
        p.BorderBrush = color == null ? MainWindow.Green : Theme.Raw(color);
    }
    internal static string MarkdownPrefix(Paragraph p) =>
        Prefix(p.BorderBrush is SolidColorBrush b && !System.Windows.Data.BindingOperations.IsDataBound(b, SolidColorBrush.ColorProperty)
            ? $"#{b.Color.R:X2}{b.Color.G:X2}{b.Color.B:X2}" : null);
}