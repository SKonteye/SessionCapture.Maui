# SessionCapture.Maui

Records what a tester saw. A floating overlay button starts a session, and from
then on the library screenshots every page the user navigates to, stores the
run as reviewable JSON plus JPEGs on the device, and exports it as a ZIP.

A built-in page then plays the session back — every screenshot in order, with
the tester's notes — and exports it, so you ship the whole loop without writing
any review UI.

Built for the gap between "it's broken on my phone" and a reproducible bug
report.

- **Platforms:** Android 26+ and iOS 15+
- **Requires:** .NET 10 / .NET MAUI
- **License:** MIT

| The page the package ships | The sample documentation app |
| --- | --- |
| <img src="https://raw.githubusercontent.com/SKonteye/SessionCapture.Maui/main/docs/session-list.jpg" width="260" alt="The built-in session list: recorded sessions with step counts, timestamps and tester names, and buttons to reload or delete." /> | <img src="https://raw.githubusercontent.com/SKonteye/SessionCapture.Maui/main/docs/docs-app.jpg" width="260" alt="A page of the sample app showing the code needed to push the built-in review page." /> |

Both shots were recorded by the library itself, running in the sample app.

## Install

```
dotnet add package SessionCapture.Maui
```

### If you get NU1605

This package requires **Microsoft.Maui.Controls 10.0.30 or newer**. If your MAUI
workload still generates apps on 10.0.20 you will see:

```
error NU1605: Detected package downgrade: Microsoft.Maui.Controls from 10.0.30 to 10.0.20
```

Set `MauiVersion` in your app's `.csproj` — the MAUI SDK already references
`Microsoft.Maui.Controls` as `$(MauiVersion)`, so overriding the property is
what works. Adding a second `PackageReference` does not:

```xml
<PropertyGroup>
  <MauiVersion>10.0.30</MauiVersion>
</PropertyGroup>
```

The floor is not arbitrary: on 10.0.20 an iOS app crashes at startup with a
`NullReferenceException` in `MauiCALayer.Dispose`, a MAUI bug fixed in 10.0.30.

## Setup

Register it in `MauiProgram.cs`. **`Enabled` defaults to `false`**, so the
library stays completely inert until you turn it on — set it explicitly or you
will see nothing at runtime.

```csharp
using SessionCapture.Maui.Extensions;

builder.UseSessionCapture(configure: options =>
{
    options.Enabled = true;
    options.AutoCaptureOnNavigation = true;
    options.CaptureQuality = 80;
    options.MaxSessionsRetained = 20;
    options.MaxScreenshotsPerSession = 200;
});
```

> `configure:` must be a **named argument** — the first parameter of
> `UseSessionCapture` is an `IConfiguration`, so passing the lambda positionally
> will not compile.

Or bind from configuration:

```csharp
builder.UseSessionCapture(
    builder.Configuration.GetSection("Features:SessionCapture"));
```

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

That is the whole setup. The overlay button appears on its own, and navigation
is hooked automatically — you do not call `AttachToShell` yourself.

## Options

| Option | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` | Master switch. Nothing runs until this is `true`. |
| `AutoCaptureOnNavigation` | `true` | Screenshot each page the user navigates to. |
| `CaptureQuality` | `80` | JPEG quality, 0–100. |
| `MaxSessionsRetained` | `20` | Oldest sessions are pruned beyond this. |
| `MaxScreenshotsPerSession` | `200` | Auto-capture stops at this many steps. |
| `StorageFolderName` | `SessionCapture` | Folder under the app data directory. |
| `GalleryAlbumName` | `SessionCapture` | Album used by gallery export. |

## Capturing from code

Inject `ISessionCaptureService` anywhere you need it.

```csharp
public class CheckoutViewModel(ISessionCaptureService capture)
{
    public Task ReportProblemAsync() =>
        capture.CaptureWithNoteAsync(
            pageName: nameof(CheckoutPage),
            viewModelName: nameof(CheckoutViewModel),
            note: "Total is wrong when a coupon applies");
}
```

Sessions:

```csharp
await capture.StartSessionAsync("Login flow", "Sam");
await capture.StopSessionAsync();

var sessions = await capture.GetAllSessionsAsync();
var zipPath  = await capture.ExportSessionAsZipAsync(sessionId);
await capture.ShareSessionAsync(sessionId);      // native share sheet
await capture.SaveSessionToPhotosAsync(sessionId);
```

Events: `StepCaptured`, `SessionStarted`, `SessionStopped`.

## What gets stored

Each session is a folder under the app data directory containing one JPEG per
step and a `session.json`. Steps are numbered in the order the tester visited
the pages, and each records the page name, the binding context type, a UTC
timestamp, the step type (`AutoNavigation`, `ManualCapture`, `Annotated`) and
any note.

The overlay never appears in its own screenshots.

## Reviewing and replaying a session

The package ships the review screen, so you do not have to build one:

```csharp
using SessionCapture.Maui.Views;

await Navigation.PushAsync(new SessionCapturePage());
```

That gives your testers the full loop: every recorded session, tap one to page
through its screenshots with the notes and timings, then export it as a ZIP,
share it, save it to the gallery, or delete it. It resolves the service from
the app's container, so nothing else is needed as long as you called
`UseSessionCapture`. Present it inside a `NavigationPage` or Shell — it pushes a
detail page. Pass the service explicitly if you resolve your own dependencies:

```csharp
await Navigation.PushAsync(new SessionCapturePage(capture));
```

### Building your own instead

`CapturedStep` stores a file name, not a path. `GetStepImagePath` resolves it
against the session folder so you can show the screenshot back to the tester.

```csharp
var session = await capture.GetSessionAsync(sessionId);

foreach (var step in session.Steps.OrderBy(s => s.StepNumber))
{
    string? path = capture.GetStepImagePath(session.Id, step);

    // null when the step has no screenshot, or the file is gone
    var source = path is null ? null : ImageSource.FromFile(path);

    Console.WriteLine($"{step.StepNumber}. {step.PageName} {step.UserNote}");
}
```

Bound into a `CollectionView`, that is a scrollable replay of the run: every
page the tester visited, in order, with the screenshot and any note they left.

```csharp
StepList.ItemsSource = session.Steps
    .OrderBy(s => s.StepNumber)
    .Select(s => new
    {
        s.StepNumber,
        s.PageName,
        s.UserNote,
        ImagePath = capture.GetStepImagePath(session.Id, s)
    })
    .ToList();
```

Handle the `null`: a session the app never closed cleanly is recovered at
startup with a final marker step that carries no screenshot. Binding a `null`
straight to an `Image` shows an empty box with no hint why.

This is what `SessionCapturePage` does internally, so reach for it only when you
want different styling or a different layout.

## Platform notes

- **Auto-capture requires Shell.** Apps with a plain page root still get the
  overlay and manual capture; only navigation hooking needs `Shell`.
- **iOS:** add `NSPhotoLibraryAddUsageDescription` to `Info.plist` if you use
  gallery export.
- **Android:** the library declares its own media permissions.

## Sample

`samples/SessionCapture.Sample` is a documentation app: seven pages, each
explaining one concept with the exact code and a live control that calls the
real library. The eighth flyout entry, **Stored sessions**, is not a sample page
at all — it is `SessionCapturePage` from the package, so you can see what one
line of integration actually gets you.

## Status

Verified on the iOS 26.5 simulator: sessions record, screenshots are written,
rapid navigation captures every page, and the library survives a root-page
swap. Android compiles and packages correctly but has not yet been exercised
on a device or emulator.
