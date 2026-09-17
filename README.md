# SessionCapture.Maui

Reusable .NET MAUI session capture library for Android and iOS.

## Host setup

1. Add the NuGet package.
2. Register it from `MauiProgram.cs`:

```csharp
using SessionCapture.Maui.Extensions;

builder.UseSessionCapture(
    builder.Configuration.GetSection("Features:SessionCapture"));
```

3. Add configuration:

```json
{
  "Features": {
    "SessionCapture": {
      "Enabled": true,
      "AutoCaptureOnNavigation": true,
      "CaptureQuality": 80,
      "MaxSessionsRetained": 20,
      "MaxScreenshotsPerSession": 200
    }
  }
}
```

## Notes

- Tester name is prompted once on first session start and persisted in `Preferences`.
- iOS hosts must provide `NSPhotoLibraryAddUsageDescription` in `Info.plist` if they use gallery export.
- Android permissions are declared by the library for media save support.
