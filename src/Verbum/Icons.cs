using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Verbum;

internal static class Icons
{
    internal static FrameworkElement Create(string name, double size = 19)
    {
        using var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/Icons/{name}.svg"))!.Stream;
        var xml = XDocument.Load(stream); var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var el in xml.Root!.Elements())
        {
            double N(string attr) => double.Parse(el.Attribute(attr)!.Value, System.Globalization.CultureInfo.InvariantCulture);
            Shape? shape = el.Name.LocalName switch
            {
                "path" => new Path { Data = Geometry.Parse(el.Attribute("d")!.Value) },
                "line" => new Line { X1 = N("x1"), Y1 = N("y1"), X2 = N("x2"), Y2 = N("y2") },
                "circle" => new Ellipse { Width = N("r") * 2, Height = N("r") * 2 },
                "rect" => new Rectangle { Width = N("width"), Height = N("height"), RadiusX = el.Attribute("rx") != null ? N("rx") : 0, RadiusY = el.Attribute("rx") != null ? N("rx") : 0 },
                "polyline" => new Polyline { Points = PointCollection.Parse(el.Attribute("points")!.Value) },
                "polygon" => new Polygon { Points = PointCollection.Parse(el.Attribute("points")!.Value) },
                _ => throw new InvalidOperationException("Elemento de ícone não suportado: " + el.Name)
            };
            if (el.Name.LocalName == "circle") { Canvas.SetLeft(shape, N("cx") - N("r")); Canvas.SetTop(shape, N("cy") - N("r")); }
            if (el.Name.LocalName == "rect") { Canvas.SetLeft(shape, el.Attribute("x") != null ? N("x") : 0); Canvas.SetTop(shape, el.Attribute("y") != null ? N("y") : 0); }
            shape.Stroke = Theme.Get("#294A3D"); shape.StrokeThickness = 1.7; shape.StrokeStartLineCap = shape.StrokeEndLineCap = PenLineCap.Round; shape.StrokeLineJoin = PenLineJoin.Round;
            canvas.Children.Add(shape);
        }
        return new Viewbox { Width = size, Height = size, Child = canvas, VerticalAlignment = VerticalAlignment.Center };
    }
    internal static FrameworkElement Label(string icon, string? text = null)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Create(icon));
        if (text != null) panel.Children.Add(new TextBlock { Text = text, Margin = new(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }
}
