using Microsoft.Maui.Controls.Shapes;

namespace SessionCapture.Maui.Views;

/// <summary>
/// Styling for the built-in session viewer.
///
/// Deliberately self-contained rather than resolved from the host app's
/// resources: these pages are pushed into somebody else's application, where a
/// <c>StaticResource</c> lookup for a key we invented would throw, and a style
/// we registered globally would leak into their UI.
///
/// Colours are declared as light/dark pairs and applied per element with
/// <c>SetAppThemeColor</c>, so the viewer follows the device theme and keeps
/// following it when the user changes it mid-session. A tester on a dark-mode
/// phone would otherwise get a white screen in the middle of a dark app.
/// Per element rather than through a <see cref="Style"/> because MAUI's
/// <c>AppThemeBinding</c> is internal and reachable only from XAML; the shapes
/// and sizes that carry no colour still come from shared styles below.
/// </summary>
internal static class SessionCaptureTheme
{
    // Light / dark pairs. Dark values keep text contrast at or above the
    // WCAG AA 4.5:1 ratio against their own backgrounds.
    private static readonly Color PrimaryLight = Color.FromArgb("#512BD4");
    private static readonly Color PrimaryDark = Color.FromArgb("#B9A3FF");

    private static readonly Color DangerLight = Color.FromArgb("#C2185B");
    private static readonly Color DangerDark = Color.FromArgb("#F06292");

    private static readonly Color MutedLight = Color.FromArgb("#6E6E6E");
    private static readonly Color MutedDark = Color.FromArgb("#AAAAAF");

    private static readonly Color CardBackgroundLight = Color.FromArgb("#F4F4F6");
    private static readonly Color CardBackgroundDark = Color.FromArgb("#2A2A30");

    private static readonly Color ThumbBackgroundLight = Color.FromArgb("#DDDDE3");
    private static readonly Color ThumbBackgroundDark = Color.FromArgb("#3A3A42");

    private static readonly Color NoteAccentLight = Color.FromArgb("#B26A00");
    private static readonly Color NoteAccentDark = Color.FromArgb("#E0A44A");

    private static readonly Color PageBackgroundLight = Colors.White;
    private static readonly Color PageBackgroundDark = Color.FromArgb("#1C1C1F");

    private static readonly Color TextLight = Color.FromArgb("#1A1A1A");
    private static readonly Color TextDark = Color.FromArgb("#ECECEF");

    /// <summary>Page background that follows the device theme.</summary>
    public static T ThemedPage<T>(this T page) where T : VisualElement
    {
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, PageBackgroundLight, PageBackgroundDark);
        return page;
    }

    /// <summary>Primary text colour that follows the device theme.</summary>
    public static T ThemedText<T>(this T label) where T : Label
    {
        label.SetAppThemeColor(Label.TextColorProperty, TextLight, TextDark);
        return label;
    }

    /// <summary>Muted caption colour that follows the device theme.</summary>
    public static T ThemedMuted<T>(this T label) where T : Label
    {
        label.SetAppThemeColor(Label.TextColorProperty, MutedLight, MutedDark);
        return label;
    }

    /// <summary>Note-accent colour that follows the device theme.</summary>
    public static T ThemedNote<T>(this T label) where T : Label
    {
        label.SetAppThemeColor(Label.TextColorProperty, NoteAccentLight, NoteAccentDark);
        return label;
    }

    /// <summary>Primary accent colour that follows the device theme.</summary>
    public static T ThemedPrimary<T>(this T label) where T : Label
    {
        label.SetAppThemeColor(Label.TextColorProperty, PrimaryLight, PrimaryDark);
        return label;
    }

    /// <summary>Card background that follows the device theme.</summary>
    public static T ThemedCard<T>(this T border) where T : Border
    {
        border.SetAppThemeColor(VisualElement.BackgroundColorProperty, CardBackgroundLight, CardBackgroundDark);
        return border;
    }

    /// <summary>Thumbnail placeholder background that follows the device theme.</summary>
    public static T ThemedThumb<T>(this T image) where T : Image
    {
        image.SetAppThemeColor(VisualElement.BackgroundColorProperty, ThumbBackgroundLight, ThumbBackgroundDark);
        return image;
    }

    /// <summary>Destructive-action background that follows the device theme.</summary>
    public static T ThemedDanger<T>(this T button) where T : Button
    {
        button.SetAppThemeColor(VisualElement.BackgroundColorProperty, DangerLight, DangerDark);
        button.TextColor = Colors.White;
        return button;
    }

    // Looked up by name from two other files, so the names live here once and
    // are reached through the accessors below: a mistyped key would otherwise
    // be a runtime KeyNotFoundException with no compile-time signal.
    private const string CardKey = "SessionCaptureCard";
    private const string HeadingKey = "SessionCaptureHeading";
    private const string CaptionKey = "SessionCaptureCaption";
    private const string NoteKey = "SessionCaptureNote";
    private const string StepNumberKey = "SessionCaptureStepNumber";
    private const string ThumbKey = "SessionCaptureThumb";

    /// <summary>The card style for a session or step row.</summary>
    public static Style Card(this ResourceDictionary resources) => Get(resources, CardKey);

    /// <summary>The page-title style.</summary>
    public static Style Heading(this ResourceDictionary resources) => Get(resources, HeadingKey);

    /// <summary>The small secondary-text style.</summary>
    public static Style Caption(this ResourceDictionary resources) => Get(resources, CaptionKey);

    /// <summary>The style for a tester's note.</summary>
    public static Style Note(this ResourceDictionary resources) => Get(resources, NoteKey);

    /// <summary>The style for a step's number and type.</summary>
    public static Style StepNumber(this ResourceDictionary resources) => Get(resources, StepNumberKey);

    /// <summary>The style for a step's screenshot thumbnail.</summary>
    public static Style Thumb(this ResourceDictionary resources) => Get(resources, ThumbKey);

    private static Style Get(ResourceDictionary resources, string key) => (Style)resources[key];

    /// <summary>
    /// Shapes and sizes only. Everything that carries a colour is applied per
    /// element through the extensions above.
    /// </summary>
    public static ResourceDictionary Build()
    {
        var resources = new ResourceDictionary();

        resources.Add(CardKey, new Style(typeof(Border))
        {
            Setters =
            {
                new Setter { Property = Border.PaddingProperty, Value = new Thickness(14) },
                new Setter { Property = Border.StrokeThicknessProperty, Value = 0d },
                new Setter { Property = Border.StrokeShapeProperty, Value = new RoundRectangle { CornerRadius = 10 } }
            }
        });

        resources.Add(HeadingKey, new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 22d },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold }
            }
        });

        resources.Add(CaptionKey, new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 13d }
            }
        });

        resources.Add(NoteKey, new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 13d }
            }
        });

        resources.Add(StepNumberKey, new Style(typeof(Label))
        {
            Setters =
            {
                new Setter { Property = Label.FontSizeProperty, Value = 12d },
                new Setter { Property = Label.FontAttributesProperty, Value = FontAttributes.Bold }
            }
        });

        // Screenshots are portrait phone captures; this keeps the ratio so the
        // thumbnail is not letterboxed.
        resources.Add(ThumbKey, new Style(typeof(Image))
        {
            Setters =
            {
                new Setter { Property = Image.WidthRequestProperty, Value = 64d },
                new Setter { Property = Image.HeightRequestProperty, Value = 114d },
                new Setter { Property = Image.AspectProperty, Value = Aspect.AspectFit }
            }
        });

        return resources;
    }
}
