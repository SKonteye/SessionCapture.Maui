namespace SessionCapture.Sample;

public partial class FormPage : ContentPage
{
    public FormPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
        {
            await Shell.Current.GoToAsync("//main");
        }
        else
        {
            await Navigation.PopAsync();
        }
    }
}
