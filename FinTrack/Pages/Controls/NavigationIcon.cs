using Microsoft.Maui.Controls.Shapes;
using ShapePath = Microsoft.Maui.Controls.Shapes.Path;

namespace FinTracker.Pages.Controls;

/// <summary>Code-native vectors without icon-font dependencies.</summary>
public static class NavigationIcon
{
    public static View Create(string name, string colorResource, double size = 24)
    {
        var data = name switch
        {
            "home" => "M3,10 L12,3 L21,10 M5,9 L5,21 L10,21 L10,14 L14,14 L14,21 L19,21 L19,9",
            "reports" => "M4,21 L4,13 L8,13 L8,21 Z M10,21 L10,4 L14,4 L14,21 Z M16,21 L16,9 L20,9 L20,21 Z",
            "add" => "M12,4 L12,20 M4,12 L20,12",
            "calendar" => "M4,5 L20,5 L20,21 L4,21 Z M4,10 L20,10 M8,2 L8,7 M16,2 L16,7 M8,14 L10,14 M14,17 L16,17",
            "more" => "M4,4 L10,4 L10,10 L4,10 Z M14,4 L20,4 L20,10 L14,10 Z M4,14 L10,14 L10,20 L4,20 Z M14,14 L20,14 L20,20 L14,20 Z",
            "menu" => "M3,6 L21,6 M3,12 L21,12 M3,18 L21,18",
            "search" => "M17,10 A7,7 0 1 1 3,10 A7,7 0 1 1 17,10 M15,15 L21,21",
            "notification" => "M5,17 L7,14 L7,9 A5,5 0 0 1 17,9 L17,14 L19,17 Z M10,20 L14,20",
            _ => "M12,12 A4,4 0 1 1 12,4 A4,4 0 1 1 12,12 M4,22 A8,8 0 0 1 20,22"
        };
        var path = new ShapePath
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
            StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Fill = Brush.Transparent, Aspect = Stretch.Uniform, WidthRequest = size, HeightRequest = size,
            InputTransparent = true, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        path.SetDynamicResource(Shape.StrokeProperty, colorResource);
        if (size == 24)
        {
            path.SetDynamicResource(VisualElement.WidthRequestProperty, "SharedIconSize");
            path.SetDynamicResource(VisualElement.HeightRequestProperty, "SharedIconSize");
        }
        return path;
    }

    public static Button HitTarget(string description, EventHandler clicked)
    {
        var button = new Button
        {
            Text = string.Empty, BackgroundColor = Colors.Transparent, Padding = 0, BorderWidth = 0,
            MinimumWidthRequest = 44, MinimumHeightRequest = 44,
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill
        };
        SemanticProperties.SetDescription(button, description);
        button.Clicked += clicked;
        return button;
    }
}
