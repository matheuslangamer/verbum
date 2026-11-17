using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Verbum;

internal sealed class NoteListView : UserControl
{
    private readonly StackPanel list = new();
    private readonly TextBlock count = MainWindow.Label("", 12, MainWindow.Muted);
    private readonly TextBox search = new() { Padding = new(32, 6, 8, 6), Margin = new(0), MinHeight = 34 };
    private readonly Func<List<TabState>> load;
    private readonly Action<TabState> primary, removeNote;
    private readonly bool deleted;

    internal NoteListView(Func<List<TabState>> load, Action<TabState> primary, Action<TabState> removeNote, bool deleted)
    {
        this.load = load; this.primary = primary; this.removeNote = removeNote; this.deleted = deleted;
        var root = new DockPanel();
        Content = root;
        var header = new StackPanel();
        var heading = MainWindow.Label(deleted ? "Lixeira" : "Notas", 26);
        heading.FontWeight = FontWeights.SemiBold; heading.Margin = new(0, 8, 0, 10);
        header.Children.Add(heading);
        var field = new Grid { MaxWidth = 340, HorizontalAlignment = HorizontalAlignment.Stretch };
        field.Children.Add(search);
        var icon = Icons.Create("search", 15);
        icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new(10, 0, 0, 0); icon.IsHitTestVisible = false; field.Children.Add(icon);
        var hint = MainWindow.Label((deleted ? "Buscar na lixeira" : "Buscar nas notas"), 13, MainWindow.Muted);
        hint.IsHitTestVisible = false; hint.Margin = new(33, 0, 0, 0); hint.VerticalAlignment = VerticalAlignment.Center; field.Children.Add(hint);
        AutomationProperties.SetName(search, (deleted ? "Buscar na lixeira" : "Buscar nas notas"));
        var fieldRow = new Grid();
        fieldRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 340 });
        fieldRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); fieldRow.Children.Add(field); header.Children.Add(fieldRow);
        count.Margin = new(0, 16, 0, 5); header.Children.Add(count);
        header.Children.Add(new Border { Height = 1, Background = MainWindow.Line });
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        search.TextChanged += (_, _) => { hint.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Refresh(); };
        Refresh();
    }

    private void Refresh()
    {
        var notes = load();
        string query = SearchText.Normalize(search.Text);
        var matches = notes.Where(n => SearchText.Normalize(n.NoteTitle + " " + n.Note + " " + string.Join(" ", n.NoteTags)).Contains(query)).ToList();
        count.Text = query.Length == 0 ? (notes.Count == 1 ? "1 nota" : $"{notes.Count} notas") : $"{matches.Count} de {notes.Count} notas";
        list.Children.Clear();
        foreach (var note in matches) list.Children.Add(Row(note));
        if (matches.Count == 0)
        {
            var message = MainWindow.Label(notes.Count == 0 ? (deleted ? "A lixeira está vazia." : "Nenhuma nota salva.") : "Nenhuma nota encontrada.", 14, MainWindow.Muted);
            message.Margin = new(0, 18, 0, 0); list.Children.Add(message);
        }
    }

    private FrameworkElement Row(TabState note)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new() { Height = GridLength.Auto }); grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        string title = string.IsNullOrWhiteSpace(note.NoteTitle) ? "Sem título" : note.NoteTitle;
        var name = MainWindow.Label(title, 15); name.FontWeight = FontWeights.SemiBold; name.ToolTip = title;
        if (!deleted) { var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(title)) { TextDecorations = null, Foreground = MainWindow.Green }; link.Click += (_, _) => primary(note); name.Inlines.Clear(); name.Inlines.Add(link); }
        text.Children.Add(name);
        string snippet = Regex.Replace(note.Note, @"\[((?:\\.|[^\]])*)\]\([^)]*\)", "$1");
        snippet = NoteColors.StripTags(snippet);
        snippet = Regex.Replace(snippet, @"\s+", " ").Trim();
        var excerpt = MainWindow.Label(snippet.Length == 0 ? "Nota vazia" : snippet, 12, MainWindow.Muted);
        excerpt.TextTrimming = TextTrimming.CharacterEllipsis; excerpt.TextWrapping = TextWrapping.NoWrap;
        excerpt.Margin = new(0, 3, 0, 0); text.Children.Add(excerpt); grid.Children.Add(text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var recover = MainWindow.Button(deleted ? "Restaurar" : "Abrir", () => { primary(note); if (deleted) Refresh(); });
        recover.Margin = new(0); recover.Padding = new(10, 4, 10, 4);
        AutomationProperties.SetName(recover, (deleted ? "Restaurar " : "Abrir ") + title); actions.Children.Add(recover);
        var remove = MainWindow.Button("", () => { removeNote(note); Refresh(); }, deleted ? "Excluir definitivamente…" : "Mover para a lixeira…");
        remove.Content = new TextBlock { Text = "\uE74D", FontFamily = new("Segoe MDL2 Assets"), FontSize = 15 };
        remove.Background = Brushes.Transparent; remove.Padding = new(6); remove.Margin = new(4, 0, 0, 0);
        AutomationProperties.SetName(remove, (deleted ? "Excluir definitivamente " : "Mover para a lixeira ") + title); actions.Children.Add(remove);
        grid.Children.Add(actions);
        void Arrange(bool narrow)
        {
            Grid.SetColumnSpan(text, narrow ? 2 : 1);
            Grid.SetRow(actions, narrow ? 1 : 0); Grid.SetColumn(actions, narrow ? 0 : 1); Grid.SetColumnSpan(actions, narrow ? 2 : 1);
            actions.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            actions.Margin = narrow ? new(0, 8, 0, 0) : new(16, 0, 0, 0);
        }
        Arrange(false); grid.SizeChanged += (_, e) => Arrange(e.NewSize.Width < 370);
        if (TextMarkers.ValidFontColor(note.NoteColor)) text.Children.Insert(0, new Border { Width = 28, Height = 4, CornerRadius = new(2), Margin = new(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left, Background = Theme.Raw(note.NoteColor) });
        return new Border { Child = grid, Padding = new(0, 12, 0, 12), BorderThickness = new(0, 0, 0, 1), BorderBrush = MainWindow.Line };
    }
}
