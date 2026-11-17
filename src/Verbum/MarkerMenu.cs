using System.Windows;
using System.Windows.Controls;

namespace Verbum;

internal static class MarkerMenu
{
    public static void AddFontColors(ContextMenu menu, Action<string?> apply, Func<Window?> owner)
    {
        menu.Items.Add(new Separator());
        var fonts = new MenuItem { Header = "Cor da letra" };
        foreach (var color in TextMarkers.FontColors)
        {
            var item = new MenuItem { Header = color.Key + " · " + color.Value, Icon = new Border { Width = 15, Height = 15, Background = Theme.Raw(color.Value) } };
            item.Click += (_, _) => apply(color.Value); fonts.Items.Add(item);
        }
        fonts.Items.Add(new Separator());
        var custom = new MenuItem { Header = "Outra cor… (#RRGGBB)" };
        custom.Click += (_, _) =>
        {
            var panel = new StackPanel { Margin = new(18) };
            panel.Children.Add(new TextBlock { Text = "Cor da letra · código hexadecimal (#RRGGBB)" });
            var input = new TextBox { Text = "#0000FF", Margin = new(0, 12, 0, 8), MaxLength = 7 };
            panel.Children.Add(input);
            var sample = new TextBlock { Text = "Prévia · Texto / אָב / λόγος", FontSize = 22, Margin = new(0, 4, 0, 8) };
            panel.Children.Add(sample);
            var error = new TextBlock { Text = "Digite # seguido de seis dígitos de 0–9 ou A–F.", TextWrapping = TextWrapping.Wrap };
            panel.Children.Add(error);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 12, 0, 0) };
            var dialog = new Window { Title = "Cor da letra", Content = panel, Width = 390, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
            if (owner() is { } parent) dialog.Owner = parent;
            var ok = MainWindow.Button("Aplicar", () => dialog.DialogResult = true); ok.IsDefault = true;
            var cancel = MainWindow.Button("Cancelar", () => dialog.DialogResult = false); cancel.IsCancel = true;
            actions.Children.Add(ok); actions.Children.Add(cancel); panel.Children.Add(actions);
            void Update()
            {
                var value = input.Text.Trim(); ok.IsEnabled = TextMarkers.ValidFontColor(value);
                if (ok.IsEnabled) sample.Foreground = Theme.Raw(value);
                error.Visibility = ok.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
            }
            input.TextChanged += (_, _) => Update(); Update();
            dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
            if (dialog.ShowDialog() == true) apply(input.Text.Trim().ToUpperInvariant());
        };
        fonts.Items.Add(custom);
        var clear = new MenuItem { Header = "Restaurar cor da letra" }; clear.Click += (_, _) => apply(null);
        fonts.Items.Add(clear); menu.Items.Add(fonts);
    }
}
