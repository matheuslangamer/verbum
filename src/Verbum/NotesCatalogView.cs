using System.Windows;
using System.Windows.Controls;

namespace Verbum;

internal sealed class NotesCatalogView : UserControl
{
    public NotesCatalogView(Func<List<TabState>> load, Action<TabState> open, Action create, Action<TabState> delete,
        Func<List<TabState>> trash, Action<TabState> restore, Action<TabState> purge, string initialPage = "Notas")
    {
        var root = new DockPanel { Margin = new(16) };
        Content = root;
        var menu = new WrapPanel();
        DockPanel.SetDock(menu, Dock.Top);
        root.Children.Add(menu);
        var host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        root.Children.Add(host);
        var pages = new Dictionary<string, Button>();
        string current = initialPage;
        void Show(string page)
        {
            current = page;
            foreach (var (name, button) in pages)
                button.Background = name == page ? Theme.Get("#CEE1D2") : Theme.Surface;
            host.Content = page switch
            {
                "Lixeira" => new NoteListView(trash, restore, purge, deleted: true),
                "Grafo" => new NoteGraphView(load(), open),
                _ => new NoteListView(load, open, delete, deleted: false)
            };
        }
        foreach (var page in new[] { "Notas", "Grafo", "Lixeira" })
        {
            var button = MainWindow.Button(page, () => Show(page));
            pages.Add(page, button); menu.Children.Add(button);
        }
        menu.Children.Add(MainWindow.Button("+ Nova nota", create));
        menu.Children.Add(MainWindow.Button("Atualizar", () => Show(current)));
        Show(current);
    }
}
