using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Verbum;

internal static class ReaderToolbarTests
{
    public static void Run(BibleLibrary library, Action<string, bool> check)
    {
        bool layoutValid = true, actionsValid = false; string? error = null;
        Action work = () =>
        {
            try
            {
                var state = new TabState { Edition = "WLC-OSHB", FontSize = 20 };
                var view = new ReaderView(library, state, () => { }, _ => { }, _ => { }, (_, _) => { }, _ => { });
                foreach (double width in new[] { 820d, 600d, 400d, 300d, 230d, 170d, 820d })
                {
                    view.Width = width; view.Height = 700; view.Measure(new Size(width, 700)); view.Arrange(new Rect(0, 0, width, 700)); view.UpdateLayout();
                    var selectors = Find<Grid>(view).Single(g => g.Name == "ReaderSelectors");
                    var controls = selectors.Children.OfType<ComboBox>().ToList();
                    var tools = Find<WrapPanel>(view).Single(g => g.Name == "ReaderActions");
                    layoutValid &= controls.Count == 4 && tools.Children.OfType<Button>().Count() == 7;
                    foreach (var control in controls.Cast<FrameworkElement>().Concat(tools.Children.OfType<Button>()))
                    {
                        var bounds = control.TransformToAncestor(view).TransformBounds(new Rect(0, 0, control.ActualWidth, control.ActualHeight));
                        layoutValid &= control.Visibility == Visibility.Visible && control.ActualWidth >= 28 && bounds.Left >= -1 && bounds.Right <= width + 1 && bounds.Bottom < 230;
                    }
                }
                var verse = new VerseEntry("ARA", 0, "Gênesis", "Gn", 1, 0, 1, 1, "Luz e luz.", true);
                var note = WordAnnotation.Create(verse, 6, 9);
                var sample = new BibleEdition { Info = new("ARA", ""), Books = [new() { Name = "Gênesis", Chapters = [[verse.Text]] }] };
                sample.Chapters[(0, 1)] = [verse];
                var sampleState = new TabState { Edition = "ARA" };
                TabState? opened = null;
                var annotated = new ReaderView(library, sampleState, () => { }, _ => { }, _ => { }, (_, _) => { }, _ => { }, () => [note], _ => { }, n => opened = n);
                typeof(ReaderView).GetField("edition", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(annotated, sample);
                annotated.RefreshAnnotations();
                var viewer = ((Grid)annotated.Content).Children.OfType<FlowDocumentScrollViewer>().Single();
                var para = (System.Windows.Documents.Paragraph)viewer.Document.Blocks.FirstBlock;
                var marker = para.Inlines.OfType<System.Windows.Documents.Hyperlink>().Single(h => Equals(h.ToolTip, note.NoteTitle));
                marker.RaiseEvent(new RoutedEventArgs(System.Windows.Documents.Hyperlink.ClickEvent));
                check("Inline note marker opens the note immediately after the exact word before punctuation", opened == note &&
                    marker.PreviousInline is System.Windows.Documents.Run { Text: "luz" } && marker.NextInline is System.Windows.Documents.Run { Text: "." });
                sampleState.HideNoteMarkers = true; annotated.RefreshAnnotations();
                check("Hiding annotations renders the original verse with no note marker", ((System.Windows.Documents.Paragraph)viewer.Document.Blocks.FirstBlock).Inlines.OfType<System.Windows.Documents.Hyperlink>().Count() == 1);
                var buttons = Find<WrapPanel>(view).Single(g => g.Name == "ReaderActions").Children.OfType<Button>().ToList();
                buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); buttons[6].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); check("Bible note toggle hides markers per reader", state.HideNoteMarkers); bool larger = state.FontSize == 21;
                buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); bool restored = state.FontSize == 20;
                buttons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); bool toggled = state.ShowInterlinear;
                buttons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); actionsValid = larger && restored && toggled && !state.ShowInterlinear;
            }
            catch (Exception ex) { error = ex.GetBaseException().Message; layoutValid = false; }
        };
        System.Windows.Application.Current.Dispatcher.Invoke(work); bool finished = true;
        check("Reader exposes four selectors and seven direct actions at 170–820 px" + (error != null ? " · " + error : ""), finished && layoutValid);
        check("Reader direct font and interlinear actions remain functional", finished && actionsValid);
        check("Reader adapts whole toolbar groups by available width", ReaderToolbarLayout.ForWidth(800) == ReaderToolbarArrangement.Wide && ReaderToolbarLayout.ForWidth(600) == ReaderToolbarArrangement.Medium && ReaderToolbarLayout.ForWidth(400) == ReaderToolbarArrangement.Compact && ReaderToolbarLayout.ForWidth(250) == ReaderToolbarArrangement.Narrow);
    }
    private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); if (child is T value) yield return value;
            foreach (var value2 in Find<T>(child)) yield return value2;
        }
    }
}
