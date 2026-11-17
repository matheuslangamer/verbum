using System.IO;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media;

namespace Verbum;

internal static class Theme
{
    [ThreadStatic] private static Dictionary<string, (SolidColorBrush Brush, Tone Tone)>? brushes;
    private sealed class Tone : INotifyPropertyChanged
    {
        public Color Color { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        public void Set(Color value) { Color = value; PropertyChanged?.Invoke(this, new(nameof(Color))); }
    }
    internal static bool IsDark { get; private set; }
    private static readonly Dictionary<string, (string Light, string Dark)> palette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#F4F4EF"] = ("#F1F3F5", "#151A20"),
        ["#FFFFFF"] = ("#FFFFFF", "#1D242C"),
        ["#FAFAF5"] = ("#F6F8FA", "#222B35"),
        ["#EAEDE5"] = ("#E5EBEF", "#111820"),
        ["#F0F3EC"] = ("#F0F4F6", "#202A34"),
        ["#EBF0E8"] = ("#E7EDF0", "#2B3743"),
        ["#DCE2D8"] = ("#D4DDE4", "#384653"),
        ["#D7DED5"] = ("#CCD6DF", "#465361"),
        ["#24332F"] = ("#263640", "#DFE6ED"),
        ["#2D3730"] = ("#28353E", "#E0E5EA"),
        ["#294A3D"] = ("#304D60", "#D0DCE6"),
        ["#386754"] = ("#345E78", "#8EC4DF"),
        ["#214B39"] = ("#244D69", "#A9D5EC"),
        ["#768379"] = ("#667887", "#9AAAB9"),
        ["#65736B"] = ("#647481", "#A4B3C0"),
        ["#EDF1EA"] = ("#EAF0F5", "#2D3A47"),
        ["#EAEFE7"] = ("#EAF0F5", "#273440"),
        ["#E0EEE4"] = ("#DCEBF5", "#2B4659"),
        ["#F2F1DD"] = ("#F1F3E7", "#303C42"),
        ["#F6E4A1"] = ("#F6E4A1", "#66572B"),
        ["#FFF4DD"] = ("#FFF4DD", "#3D3425"),
        ["#8B5D25"] = ("#8B5D25", "#E9C58C"),
        ["#ACCEBA"] = ("#B7D5EB", "#456A87"),
        ["#CEE1D2"] = ("#CADFEA", "#385164"),
        ["#CAD1CA"] = ("#CAD1CA", "#465461"),
        ["Primary"] = ("#345E78", "#406F8D"),
        ["OnPrimary"] = ("#FFFFFF", "#FFFFFF")
    };
    internal static Brush Surface => Get("#FFFFFF");
    internal static Brush Get(string key)
    {
        brushes ??= new(StringComparer.OrdinalIgnoreCase);
        if (!brushes.TryGetValue(key, out var pair))
        {
            var tone = new Tone(); tone.Set(ColorFor(key, IsDark));
            var brush = new SolidColorBrush();
            BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(Tone.Color)) { Source = tone });
            brushes[key] = pair = (brush, tone);
        }
        return pair.Brush;
    }
    internal static Brush Raw(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    internal static Color ColorFor(string key, bool dark)
    {
        if (palette.TryGetValue(key, out var pair)) key = dark ? pair.Dark : pair.Light;
        return (Color)ColorConverter.ConvertFromString(key);
    }
    internal static void Apply(bool dark, bool updateResources = true)
    {
        IsDark = dark;
        if (brushes != null) foreach (var (key, pair) in brushes) pair.Tone.Set(ColorFor(key, dark));
        if (updateResources && Application.Current is { } app)
        {
            foreach (string key in palette.Keys) app.Resources["Theme" + key] = Get(key);
            app.Resources[SystemColors.WindowBrushKey] = Surface;
            app.Resources[SystemColors.WindowTextBrushKey] = Get("#24332F");
            app.Resources[SystemColors.ControlBrushKey] = Get("#EBF0E8");
            app.Resources[SystemColors.ControlTextBrushKey] = Get("#24332F");
            app.Resources[SystemColors.HighlightBrushKey] = Get("#ACCEBA");
            app.Resources[SystemColors.HighlightTextBrushKey] = Get("#24332F");
        }
    }
    private static string PreferencePath => Path.Combine(SessionStore.DataDirectory, "appearance.txt");
    internal static void Load() { try { Apply(File.Exists(PreferencePath) && File.ReadAllText(PreferencePath).Trim() == "dark"); } catch { Apply(false); } }
    internal static void Save() { Directory.CreateDirectory(SessionStore.DataDirectory); File.WriteAllText(PreferencePath, IsDark ? "dark" : "light"); }
}
