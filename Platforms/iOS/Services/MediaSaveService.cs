using Foundation;
using SessionCapture.Maui.Services.Interfaces;
using UIKit;

namespace SessionCapture.Maui.Platforms.iOS.Services;

public sealed class MediaSaveService : IMediaSaveService
{
    public async Task SaveImageToGalleryAsync(byte[] imageData, string fileName, string albumName)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var imageDataObject = NSData.FromArray(imageData);
            var image = UIImage.LoadFromData(imageDataObject);
            if (image == null)
            {
                taskCompletionSource.TrySetException(new InvalidOperationException(
                    $"Failed to create a UIImage for '{fileName}'."));
                return;
            }

            image.SaveToPhotosAlbum((savedImage, error) =>
            {
                if (error != null)
                {
                    taskCompletionSource.TrySetException(new InvalidOperationException(
                        $"Failed to save '{fileName}' to Photos: {error.LocalizedDescription}"));
                    return;
                }

                taskCompletionSource.TrySetResult(true);
            });
        });

        await taskCompletionSource.Task;
    }
}
