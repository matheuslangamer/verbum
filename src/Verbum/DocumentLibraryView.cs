using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Verbum;

public sealed class DocumentLibraryView : UserControl
{
    private readonly DocumentLibrary library;
    private readonly Action<ReadingDocument, int, string> open;
    private readonly ListBox books = new() { MinWidth = 180 };
    private readonly TextBox filter = new() { ToolTip = "Filtrar títulos da biblioteca", Margin = new(0, 8, 0, 8) };
    private readonly TextBlock info = MainWindow.Label("Selecione um livro para abrir.", 14);
    private readonly TextBlock status = MainWindow.Label("", 12, MainWindow.Muted);
    private readonly Button import;
    private List<ReadingDocument> all = [];
    public DocumentLibraryView(DocumentLibrary library, Action<ReadingDocument, int, string> open)
    {
        this.library = library; this.open = open;
        var root = new DockPanel { Margin = new(20) }; Content = root;
        var head = new StackPanel(); head.Children.Add(MainWindow.Label("BIBLIOTECA", 10, MainWindow.Green)); head.Children.Add(MainWindow.Label("Livros e materiais de estudo", 25));
        import = MainWindow.Button("Adicionar livros e documentos…", async () => await Import()); head.Children.Add(import); head.Children.Add(filter); head.Children.Add(status); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new()); grid.Children.Add(books);
        var detail = new StackPanel { Margin = new(20) }; detail.Children.Add(info); detail.Children.Add(MainWindow.Button("Abrir leitura", OpenSelected));
        detail.Children.Add(MainWindow.Button("Excluir da biblioteca…", () =>
        {
            if (books.SelectedItem is not ReadingDocument selected) return;
            if (MessageBox.Show(Window.GetWindow(this), $"Excluir ‘{selected.Title}’ do catálogo?\nO original, as notas e as marcações serão preservados. É possível restaurar depois.", "Excluir leitura", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { library.Remove(selected.Id); Reload(); } catch (Exception ex) { status.Text = ex.Message; }
        }));
        detail.Children.Add(MainWindow.Button("Restaurar leituras excluídas", () => { try { library.RestoreRemoved(); Reload(); } catch (Exception ex) { status.Text = ex.Message; } }));
        detail.Children.Add(MainWindow.Label("PDF conserva as páginas originais. DOCX e EPUB abrem em leitura contínua, com tabelas e imagens comuns. A diagramação e as notas do original podem diferir. DOC e RTF exigem Word instalado. EPUB protegido por DRM não é suportado. PDFs e figuras sem texto reconhecido precisam de OCR para entrar na pesquisa.", 12, MainWindow.Muted));
        var scroll = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 1); grid.Children.Add(scroll); root.Children.Add(grid);
        filter.TextChanged += (_, _) => Filter(); books.SelectionChanged += (_, _) => Describe(); books.MouseDoubleClick += (_, _) => OpenSelected();
        Loaded += (_, _) => Reload();
    }
    private void Reload() { try { all = library.Load(); Filter(); status.Text = $"{all.Count} títulos · leitura e pesquisa locais"; } catch (Exception ex) { status.Text = ex.Message; } }
    private void Filter() { string key = SearchText.Normalize(filter.Text); books.ItemsSource = all.Where(d => SearchText.Normalize(d.Title).Contains(key)).ToList(); if (books.Items.Count > 0) books.SelectedIndex = 0; }
    private void Describe()
    {
        if (books.SelectedItem is not ReadingDocument d) { info.Text = "Nenhum livro nesta seleção."; return; }
        int searchable = d.Sections.Count(s => SearchText.Tokenize(s.PlainText).Count > 0);
        info.Text = d.Title + $"\n\n{d.Format} · {d.Sections.Count} " + (d.Format == "PDF" ? "páginas" : "seções") + $"\n{searchable} com texto pesquisável\n\n" + d.Attribution;
    }
    private void OpenSelected() { if (books.SelectedItem is ReadingDocument d) open(d, 0, ""); }
    private async Task Import()
    {
        var dialog = new OpenFileDialog { Filter = DocumentLibrary.FileFilter, Multiselect = true, Title = "Adicionar à Biblioteca do VERBUM" }; if (dialog.ShowDialog() != true) return;
        import.IsEnabled = false; var failures = new List<string>(); int added = 0;
        try
        {
            foreach (string path in dialog.FileNames)
            {
                status.Text = "Importando " + Path.GetFileName(path) + "…";
                try { await Task.Run(() => library.Import(path)); added++; } catch (Exception ex) { failures.Add(Path.GetFileName(path) + ": " + ex.Message); }
            }
            Reload(); status.Text = $"{added} arquivo(s) disponível(is)." + (failures.Count > 0 ? "\n" + string.Join("\n", failures) : "");
        }
        finally { import.IsEnabled = true; }
    }
}
