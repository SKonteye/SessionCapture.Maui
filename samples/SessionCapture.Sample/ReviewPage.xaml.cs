using SessionCapture.Maui.Views;

namespace SessionCapture.Sample;

public partial class ReviewPage : DocPage
{
    public ReviewPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Exactly what a consuming app writes: one push, no UI of its own.
    /// </summary>
    private async void OnOpenBuiltInClicked(object? sender, EventArgs e)
        => await RunDemoAsync(ResultLabel, async () =>
        {
            await Navigation.PushAsync(new SessionCapturePage());
            return "opened the page the package ships";
        });
}
