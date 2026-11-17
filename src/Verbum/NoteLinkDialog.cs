using System.Windows;
using System.Windows.Controls;

namespace Verbum;

internal sealed class NoteLinkDialog : Window
{
    internal NoteLink? Result { get; private set; }
    internal NoteLinkDialog(Window owner, BibleLibrary library, List<TabState> notes, List<ReadingDocument> books, string selected, string defaultEdition, bool bibleOnly = false)
    {
        Owner = owner; Title = "Inserir vínculo na nota"; Width = 490; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new(20) }; Content = panel;
        void Label(string value) => panel.Children.Add(MainWindow.Label(value, 12, MainWindow.Muted));
        Label("Texto do link (opcional)"); var label = new TextBox { Text = selected, Margin = new(0, 4, 0, 12) }; panel.Children.Add(label);
        Label("Vincular a"); var kind = new ComboBox { ItemsSource = new[] { "Passagem bíblica", "Nota / comentário pessoal", "Livro / comentário da biblioteca", "Página web" }, SelectedIndex = 0, Margin = new(0, 4, 0, 10) }; kind.IsEnabled = !bibleOnly; panel.Children.Add(kind);
        var bible = new ComboBox { ItemsSource = library.Editions.Select(e => e.Id).ToList(), SelectedItem = library.Editions.Any(e => e.Id == defaultEdition) ? defaultEdition : library.Editions.FirstOrDefault()?.Id, Margin = new(0, 3, 0, 6) }; panel.Children.Add(bible);
        var reference = new TextBox { Text = "João 3:16", ToolTip = "Referência existente na edição escolhida, incluindo intervalos como João 3:16-18", Margin = new(0, 3, 0, 8) }; panel.Children.Add(reference);
        var note = new ComboBox { ItemsSource = notes, DisplayMemberPath = "NoteTitle", SelectedIndex = notes.Count > 0 ? 0 : -1, Margin = new(0, 3, 0, 8) }; panel.Children.Add(note);
        var book = new ComboBox { ItemsSource = books, DisplayMemberPath = "Title", SelectedIndex = books.Count > 0 ? 0 : -1, Margin = new(0, 3, 0, 8) }; panel.Children.Add(book);
        var section = new ComboBox { Margin = new(0, 3, 0, 8) }; panel.Children.Add(section);
        void Sections() { section.ItemsSource = (book.SelectedItem as ReadingDocument)?.Sections; section.SelectedIndex = section.HasItems ? 0 : -1; } book.SelectionChanged += (_, _) => Sections(); Sections();
        var hint = MainWindow.Label("", 12, MainWindow.Muted); hint.Margin = new(0, 5, 0, 8); panel.Children.Add(hint);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; panel.Children.Add(actions);
        var ok = MainWindow.Button("Inserir vínculo", () => { }); ok.IsDefault = true; actions.Children.Add(ok); var cancel = MainWindow.Button("Cancelar", () => DialogResult = false); cancel.IsCancel = true; actions.Children.Add(cancel);
        void Update()
        {
            bible.Visibility = kind.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            reference.Visibility = kind.SelectedIndex is 0 or 3 ? Visibility.Visible : Visibility.Collapsed;
            note.Visibility = kind.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            book.Visibility = section.Visibility = kind.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            reference.Text = kind.SelectedIndex == 3 ? "https://" : "João 3:16";
            hint.Text = kind.SelectedIndex switch { 0 => "O vínculo guarda a edição e a referência. Na prévia, clique para abrir a Bíblia.", 1 => notes.Count == 0 ? "Crie uma nota ou comentário antes de vinculá-lo." : "Abre a nota existente, sem criar uma cópia.", 2 => books.Count == 0 ? "Adicione livros ou comentários pela Biblioteca." : "Escolha o livro e a página/seção que deseja citar.", _ => "Endereços http/https abrem no navegador ao clicar na prévia." };
        }
        kind.SelectionChanged += (_, _) => Update(); Update();
        ok.Click += async (_, _) =>
        {
            ok.IsEnabled = false;
            try
            {
                string target, caption;
                if (kind.SelectedIndex == 0)
                {
                    string edition = bible.SelectedItem as string ?? ""; var source = await library.LoadAsync(edition);
                    if (!ExegesisData.TryPassage(source, reference.Text, out var passage) || passage == null) throw new InvalidOperationException("Referência não encontrada nesta edição. Ex.: João 3:16 ou Joel 1:1-3.");
                    target = NoteLinks.Bible(edition, passage.Label); caption = passage.Label + " · " + edition;
                }
                else if (kind.SelectedIndex == 1 && note.SelectedItem is TabState chosen) { target = NoteLinks.Note(chosen.Id); caption = chosen.NoteTitle; }
                else if (kind.SelectedIndex == 2 && book.SelectedItem is ReadingDocument chosenBook && section.SelectedIndex >= 0) { target = NoteLinks.Book(chosenBook.Id, section.SelectedIndex); caption = chosenBook.Title + " · " + chosenBook.Sections[section.SelectedIndex].Title; }
                else if (kind.SelectedIndex == 3 && NoteLinks.TryParse(reference.Text.Trim(), out var destination) && destination?.Kind == "web") { target = reference.Text.Trim(); caption = target; }
                else throw new InvalidOperationException("Escolha um destino válido para o vínculo.");
                Result = new(label.Text.Trim().Length > 0 ? label.Text.Trim() : caption, target); DialogResult = true;
            }
            catch (Exception ex) { hint.Text = ex.Message; }
            finally { ok.IsEnabled = true; }
        };
    }
}
