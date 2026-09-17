using Android.Content;
using Android.OS;
using Android.Provider;
using SessionCapture.Maui.Services.Interfaces;
using Uri = Android.Net.Uri;

namespace SessionCapture.Maui.Platforms.Android.Services;

public sealed class MediaSaveService : IMediaSaveService
{
    private const string MimeType = "image/jpeg";

    public async Task SaveImageToGalleryAsync(byte[] imageData, string fileName, string albumName)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            await Task.Run(() => SaveWithMediaStoreApi29(imageData, fileName, albumName));
            return;
        }

#pragma warning disable CA1416
        var permissionStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
        if (permissionStatus != PermissionStatus.Granted)
        {
            permissionStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
        }
#pragma warning restore CA1416

        if (permissionStatus != PermissionStatus.Granted)
        {
            throw new InvalidOperationException("Storage write permission was denied.");
        }

        await Task.Run(() => SaveWithLegacyApi(imageData, fileName, albumName));
    }

    private static void SaveWithMediaStoreApi29(byte[] imageData, string fileName, string albumName)
    {
        var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                      ?? global::Android.App.Application.Context;
        var resolver = context.ContentResolver
                       ?? throw new InvalidOperationException("ContentResolver is not available.");

        var relativePath = $"Pictures/{SanitizeAlbumName(albumName)}";
        var contentValues = new ContentValues();
        contentValues.Put(MediaStore.IMediaColumns.DisplayName, fileName);
        contentValues.Put(MediaStore.IMediaColumns.MimeType, MimeType);
        contentValues.Put(MediaStore.IMediaColumns.RelativePath, relativePath);
        contentValues.Put(MediaStore.IMediaColumns.IsPending, 1);

        Uri? uri = null;
        try
        {
            uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri, contentValues)
                  ?? throw new InvalidOperationException("Failed to create a MediaStore entry.");

            using var outputStream = resolver.OpenOutputStream(uri)
                                     ?? throw new InvalidOperationException("Failed to open MediaStore output stream.");
            outputStream.Write(imageData, 0, imageData.Length);
            outputStream.Flush();

            contentValues.Clear();
            contentValues.Put(MediaStore.IMediaColumns.IsPending, 0);
            resolver.Update(uri, contentValues, null, null);
        }
        catch
        {
            if (uri != null)
            {
                try
                {
                    resolver.Delete(uri, null, null);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static void SaveWithLegacyApi(byte[] imageData, string fileName, string albumName)
    {
        var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
                      ?? global::Android.App.Application.Context;

#pragma warning disable CA1422
        var picturesDirectory = global::Android.OS.Environment.GetExternalStoragePublicDirectory(
            global::Android.OS.Environment.DirectoryPictures);
#pragma warning restore CA1422

        if (picturesDirectory == null)
        {
            throw new InvalidOperationException("Pictures directory is not available.");
        }

        var albumDirectory = new Java.IO.File(picturesDirectory, SanitizeAlbumName(albumName));
        if (!albumDirectory.Exists())
        {
            albumDirectory.Mkdirs();
        }

        var file = new Java.IO.File(albumDirectory, fileName);
        using var outputStream = new Java.IO.FileOutputStream(file);
        outputStream.Write(imageData);
        outputStream.Flush();

        var mediaScanIntent = new Intent(Intent.ActionMediaScannerScanFile);
        mediaScanIntent.SetData(Uri.FromFile(file));
        context.SendBroadcast(mediaScanIntent);
    }

    private static string SanitizeAlbumName(string albumName)
    {
        return string.IsNullOrWhiteSpace(albumName) ? "SessionCapture" : albumName.Trim();
    }
}
