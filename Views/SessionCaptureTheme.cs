using Microsoft.Maui.Controls.Shapes;

namespace SessionCapture.Maui.Views;

/// <summary>
/// Styles for the built-in session viewer.
///
/// Deliberately self-contained rather than resolved from the host app's
/// resources: these pages are pushed into somebody else's application, where a
/// <c>StaticResource</c> lookup for a key we invented would throw, and a style
/// we registered globally would leak into their UI. Everything the viewer needs
/// is defined here and merged into the page's own resource dictionary.
/// </summary>
internal static class SessionCaptureTheme
{
    public static readonly Color Primary = Color.FromArgb("#512BD4");
    public static readonly Color Danger = Color.FromArgb("#C2185B");
    public static readonly Color Muted = Color.FromArgb("#6E6E6E");
    public static readonly Color CardBackground = Color.FromArgb("#F4F4F6");
    public static readonly Color ThumbBackground = Color.FromArgb("#DDDDE3");
    public static readonly Color NoteAccent = Color.FromArgb("#B26A00");
    public static readonly Color PageBackground = Colors.White;

    public static ResourceDictionary Build()
    {
        var resources = new ResourceDictionary();

        resources.Add("SessionCaptureCard", new Style(typeof(Border))
        {
            Setters =
            {
                new Setter { Property = Border.BackgroundColorProperty, Value = CardBackground },
                new Setter { Property = Border.PaddingProperty, Value = new Thickness(14) },
                new Setter { Property = Border.StrokeThicknessProperty, Value = 0d },
                new Setter { Property = Border.StrokeShapeProperty, Value = new RoundRectangle { CornerRadius = 10 } }
            }
        });

        resources.Add("SessionCaptureHeading", new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 22d },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold }
            }
        });

        resources.Add("SessionCaptureCaption", new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 13d },
                new Setter { Property = Label.TextColorProperty, Value = Muted }
            }
        });

        resources.Add("SessionCaptureNote", new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 13d },
                new Setter { Property = Label.TextColorProperty, Value = NoteAccent }
            }
        });

        resources.Add("SessionCaptureStepNumber", new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 12d },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold },
                new Setter { Property = Label.TextColorProperty, Value = Primary }
            }
        });

        // Screenshots are portrait phone captures; this keeps the ratio so the
        // thumbnail is not letterboxed.
        resources.Add("SessionCaptureThumb", new Style(typeof(Image))
        {
            Setters =
            {
                new Setter { Property = Image.WidthRequestProperty, Value = 64d },
                new Setter { Property = Image.HeightRequestProperty, Value = 114d },
                new Setter { Property = Image.AspectProperty, Value = Aspect.AspectFit },
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = ThumbBackground }
            }
        });

        resources.Add("SessionCaptureDangerButton", new Style(typeof(Button))
        {
            Setters =
            {
                new Setter { Property = Button.BackgroundColorProperty, Value = Danger },
                new Setter { Property = Button.TextColorProperty, Value = Colors.White }
            }
        });

        return resources;
    }
}
