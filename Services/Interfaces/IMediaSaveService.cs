namespace SessionCapture.Maui.Services.Interfaces;

public interface IMediaSaveService
{
    Task SaveImageToGalleryAsync(byte[] imageData, string fileName, string albumName);
}
