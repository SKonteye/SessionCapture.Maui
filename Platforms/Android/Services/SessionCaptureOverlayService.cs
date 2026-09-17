using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Google.Android.Material.FloatingActionButton;
using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services;
using SessionCapture.Maui.Services.Interfaces;
using Color = Android.Graphics.Color;
using Paint = Android.Graphics.Paint;
using View = Android.Views.View;

namespace SessionCapture.Maui.Platforms.Android.Services;

public sealed class SessionCaptureOverlayService : ISessionCaptureOverlayService
{
    private const int FabSizeDp = 56;
    private const int ElevationDp = 8;
    private const int MarginDp = 24;
    private const int BarHeightDp = 48;
    private const int BarPaddingDp = 12;
    private const int DotSizeDp = 12;
    private const int ButtonSizeDp = 36;

    private static readonly Color ColorFabRed = Color.ParseColor("#E53935");
    private static readonly Color ColorBarDark = Color.ParseColor("#212121");
    private static readonly Color ColorRecDot = Color.ParseColor("#FF1744");
    private static readonly Color ColorWhite = Color.ParseColor("#FFFFFF");
    private static readonly Color ColorWhiteSemiTransparent = Color.ParseColor("#B3FFFFFF");

    private ISessionCaptureService? _sessionCaptureService;
    private ViewGroup? _rootView;
    private FloatingActionButton? _fab;
    private LinearLayout? _recordingBar;
    private TextView? _captureCountLabel;
    private bool _isRecording;
    private int _captureCount;
    private bool _isTemporarilyHidden;
    private DragTouchListener? _dragListener;

    public void ShowOverlay(ISessionCaptureService sessionCaptureService)
    {
        _sessionCaptureService = sessionCaptureService;
        _sessionCaptureService.StepCaptured += OnStepCaptured;
        _sessionCaptureService.SessionStarted += OnSessionStarted;
        _sessionCaptureService.SessionStopped += OnSessionStopped;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            _rootView = activity?.FindViewById<ViewGroup>(global::Android.Resource.Id.Content);
            if (_rootView == null)
            {
                return;
            }

            CreateIdleFab();
        });
    }

    public void HideOverlay()
    {
        if (_sessionCaptureService != null)
        {
            _sessionCaptureService.StepCaptured -= OnStepCaptured;
            _sessionCaptureService.SessionStarted -= OnSessionStarted;
            _sessionCaptureService.SessionStopped -= OnSessionStopped;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            RemoveCurrentOverlay();
            _rootView = null;
            _sessionCaptureService = null;
        });
    }

    public void UpdateState(bool isRecording, int captureCount)
    {
        _isRecording = isRecording;
        _captureCount = captureCount;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_isTemporarilyHidden)
            {
                return;
            }

            if (isRecording && _recordingBar != null && _captureCountLabel != null)
            {
                _captureCountLabel.Text = captureCount.ToString();
                return;
            }

            if (!isRecording && _fab != null)
            {
                return;
            }

            RemoveCurrentOverlay();

            if (_rootView == null)
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                _rootView = activity?.FindViewById<ViewGroup>(global::Android.Resource.Id.Content);
            }

            if (_rootView == null)
            {
                return;
            }

            if (isRecording)
            {
                CreateRecordingBar();
            }
            else
            {
                CreateIdleFab();
            }
        });
    }

    public void SuspendOverlay()
    {
        _isTemporarilyHidden = true;
        MainThread.BeginInvokeOnMainThread(RemoveCurrentOverlay);
    }

    public void ResumeOverlay()
    {
        _isTemporarilyHidden = false;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_rootView == null)
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                _rootView = activity?.FindViewById<ViewGroup>(global::Android.Resource.Id.Content);
            }

            if (_rootView == null)
            {
                return;
            }

            if (_isRecording)
            {
                CreateRecordingBar();
            }
            else
            {
                CreateIdleFab();
            }
        });
    }

    private void CreateIdleFab()
    {
        if (_rootView == null)
        {
            return;
        }

        var context = _rootView.Context;
        if (context == null)
        {
            return;
        }

        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var fabSizePx = (int)(FabSizeDp * density);
        var marginPx = (int)(MarginDp * density);
        var elevationPx = ElevationDp * density;

        _fab = new FloatingActionButton(context);
        _fab.Size = FloatingActionButton.SizeMini;
        _fab.CustomSize = fabSizePx;
        _fab.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(ColorFabRed);
        _fab.Elevation = elevationPx;
        _fab.CompatElevation = elevationPx;
        _fab.RippleColor = ColorWhiteSemiTransparent;
        _fab.SetImageDrawable(CreateTextDrawable(context, "\U0001F4F7", 20, ColorWhite));
        _fab.SetScaleType(ImageView.ScaleType.Center!);

        var layoutParams = new FrameLayout.LayoutParams(fabSizePx, fabSizePx)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.End
        };
        layoutParams.SetMargins(marginPx, marginPx, marginPx, marginPx + (int)(60 * density));

        _fab.Click += OnFabClicked;

        _dragListener?.Dispose();
        _dragListener = new DragTouchListener(_rootView, SessionCaptureKeys.OverlayX, SessionCaptureKeys.OverlayY);
        _fab.SetOnTouchListener(_dragListener);

        _rootView.AddView(_fab, layoutParams);
        _fab.BringToFront();

        var savedX = Preferences.Default.Get(SessionCaptureKeys.OverlayX, -1f);
        var savedY = Preferences.Default.Get(SessionCaptureKeys.OverlayY, -1f);
        if (savedX >= 0 && savedY >= 0)
        {
            _fab.Post(() =>
            {
                _fab?.SetX(savedX);
                _fab?.SetY(savedY);
            });
        }
    }

    private void CreateRecordingBar()
    {
        if (_rootView == null)
        {
            return;
        }

        var context = _rootView.Context;
        if (context == null)
        {
            return;
        }

        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var barHeightPx = (int)(BarHeightDp * density);
        var marginPx = (int)(MarginDp * density);
        var paddingPx = (int)(BarPaddingDp * density);
        var dotSizePx = (int)(DotSizeDp * density);
        var buttonSizePx = (int)(ButtonSizeDp * density);
        var cornerRadiusPx = barHeightPx / 2f;
        var elementSpacingPx = (int)(8 * density);

        _recordingBar = new LinearLayout(context)
        {
            Orientation = global::Android.Widget.Orientation.Horizontal
        };
        _recordingBar.SetGravity(GravityFlags.CenterVertical);
        _recordingBar.SetPadding(paddingPx, 0, paddingPx, 0);

        var background = new GradientDrawable();
        background.SetShape(ShapeType.Rectangle);
        background.SetCornerRadius(cornerRadiusPx);
        background.SetColor(ColorBarDark);
        _recordingBar.Background = background;
        _recordingBar.Elevation = ElevationDp * density;

        var dotView = new View(context);
        var dotDrawable = new GradientDrawable();
        dotDrawable.SetShape(ShapeType.Oval);
        dotDrawable.SetColor(ColorRecDot);
        dotView.Background = dotDrawable;
        var dotParams = new LinearLayout.LayoutParams(dotSizePx, dotSizePx);
        dotParams.SetMargins(0, 0, elementSpacingPx, 0);
        dotParams.Gravity = GravityFlags.CenterVertical;
        _recordingBar.AddView(dotView, dotParams);

        var pulseAnimation = new global::Android.Views.Animations.AlphaAnimation(1f, 0.3f)
        {
            Duration = 800,
            RepeatCount = global::Android.Views.Animations.Animation.Infinite,
            RepeatMode = global::Android.Views.Animations.RepeatMode.Reverse
        };
        dotView.StartAnimation(pulseAnimation);

        var recLabel = new TextView(context)
        {
            Text = "REC",
            TextSize = 12
        };
        recLabel.SetTextColor(ColorWhite);
        recLabel.SetTypeface(null, TypefaceStyle.Bold);
        var recParams = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        recParams.SetMargins(0, 0, elementSpacingPx * 2, 0);
        recParams.Gravity = GravityFlags.CenterVertical;
        _recordingBar.AddView(recLabel, recParams);

        _captureCountLabel = new TextView(context)
        {
            Text = _captureCount.ToString(),
            TextSize = 12
        };
        _captureCountLabel.SetTextColor(ColorWhiteSemiTransparent);
        var countParams = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WrapContent,
            LinearLayout.LayoutParams.WrapContent);
        countParams.SetMargins(0, 0, elementSpacingPx * 2, 0);
        countParams.Gravity = GravityFlags.CenterVertical;
        _recordingBar.AddView(_captureCountLabel, countParams);

        var captureButton = new TextView(context)
        {
            Text = "\U0001F4F8",
            TextSize = 18
        };
        captureButton.SetTextColor(ColorWhite);
        captureButton.Clickable = true;
        captureButton.Focusable = true;
        captureButton.Gravity = GravityFlags.Center;
        captureButton.Click += OnManualCaptureClicked;
        var captureParams = new LinearLayout.LayoutParams(buttonSizePx, buttonSizePx);
        captureParams.SetMargins(0, 0, elementSpacingPx, 0);
        captureParams.Gravity = GravityFlags.CenterVertical;
        _recordingBar.AddView(captureButton, captureParams);

        var stopButton = new TextView(context)
        {
            Text = "\u25A0",
            TextSize = 18
        };
        stopButton.SetTextColor(ColorFabRed);
        stopButton.Clickable = true;
        stopButton.Focusable = true;
        stopButton.Gravity = GravityFlags.Center;
        stopButton.Click += OnStopClicked;
        var stopParams = new LinearLayout.LayoutParams(buttonSizePx, buttonSizePx);
        stopParams.Gravity = GravityFlags.CenterVertical;
        _recordingBar.AddView(stopButton, stopParams);

        var layoutParams = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.WrapContent,
            barHeightPx)
        {
            Gravity = GravityFlags.Bottom | GravityFlags.End
        };
        layoutParams.SetMargins(marginPx, marginPx, marginPx, marginPx + (int)(60 * density));

        _dragListener?.Dispose();
        _dragListener = new DragTouchListener(_rootView, SessionCaptureKeys.OverlayX, SessionCaptureKeys.OverlayY);
        _recordingBar.SetOnTouchListener(_dragListener);

        _rootView.AddView(_recordingBar, layoutParams);
        _recordingBar.BringToFront();

        var savedX = Preferences.Default.Get(SessionCaptureKeys.OverlayX, -1f);
        var savedY = Preferences.Default.Get(SessionCaptureKeys.OverlayY, -1f);
        if (savedX >= 0 && savedY >= 0)
        {
            _recordingBar.Post(() =>
            {
                _recordingBar?.SetX(savedX);
                _recordingBar?.SetY(savedY);
            });
        }
    }

    private void RemoveCurrentOverlay()
    {
        if (_fab != null)
        {
            _fab.Click -= OnFabClicked;
            _fab.SetOnTouchListener(null);
            (_fab.Parent as ViewGroup)?.RemoveView(_fab);
            _fab.Dispose();
            _fab = null;
        }

        if (_recordingBar != null)
        {
            _recordingBar.SetOnTouchListener(null);
            (_recordingBar.Parent as ViewGroup)?.RemoveView(_recordingBar);
            _recordingBar.Dispose();
            _recordingBar = null;
            _captureCountLabel = null;
        }

        if (_dragListener != null)
        {
            _dragListener.Dispose();
            _dragListener = null;
        }
    }

    private async void OnFabClicked(object? sender, EventArgs e)
    {
        if (_dragListener?.IsDragging == true || _sessionCaptureService == null)
        {
            return;
        }

        var mainPage = ApplicationPageResolver.TryGetMainPage();
        if (mainPage == null)
        {
            return;
        }

        var testerName = await EnsureTesterNameAsync(mainPage);
        var sessionName = await mainPage.DisplayPromptAsync(
            "Start Capture Session",
            "Enter a session name (optional):",
            "Start",
            "Cancel",
            placeholder: "e.g. Login flow");

        if (sessionName == null)
        {
            return;
        }

        try
        {
            await _sessionCaptureService.StartSessionAsync(
                string.IsNullOrWhiteSpace(sessionName) ? null : sessionName.Trim(),
                testerName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Failed to start session: {ex.Message}");
        }
    }

    private async void OnManualCaptureClicked(object? sender, EventArgs e)
    {
        if (_sessionCaptureService == null || !_sessionCaptureService.IsSessionActive)
        {
            return;
        }

        try
        {
            var page = Shell.Current?.CurrentPage;
            var pageName = page?.GetType().Name ?? "Unknown";
            var viewModelName = page?.BindingContext?.GetType().Name ?? "Unknown";

            var mainPage = ApplicationPageResolver.TryGetMainPage();
            if (mainPage == null)
            {
                return;
            }

            var note = await mainPage.DisplayPromptAsync(
                "Manual Capture",
                "Add a note (optional):",
                "Capture",
                "Skip",
                placeholder: "e.g. User entered VIN manually");

            if (string.IsNullOrWhiteSpace(note))
            {
                await _sessionCaptureService.CaptureScreenshotAsync(
                    pageName,
                    viewModelName,
                    CaptureStepType.ManualCapture);
            }
            else
            {
                await _sessionCaptureService.CaptureWithNoteAsync(pageName, viewModelName, note.Trim());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Manual capture failed: {ex.Message}");
        }
    }

    private async void OnStopClicked(object? sender, EventArgs e)
    {
        if (_sessionCaptureService == null || !_sessionCaptureService.IsSessionActive)
        {
            return;
        }

        try
        {
            await _sessionCaptureService.StopSessionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Failed to stop session: {ex.Message}");
        }
    }

    private void OnStepCaptured(object? sender, CapturedStep step)
    {
        _captureCount = _sessionCaptureService?.CurrentSession?.StepCount ?? (_captureCount + 1);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_captureCountLabel != null)
            {
                _captureCountLabel.Text = _captureCount.ToString();
            }
        });
    }

    private void OnSessionStarted(object? sender, CapturedSession session)
    {
        UpdateState(true, session.StepCount);
    }

    private void OnSessionStopped(object? sender, CapturedSession session)
    {
        UpdateState(false, 0);
    }

    private static async Task<string?> EnsureTesterNameAsync(Page mainPage)
    {
        var savedName = Preferences.Default.Get(SessionCaptureKeys.TesterName, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedName))
        {
            return savedName.Trim();
        }

        var testerName = await mainPage.DisplayPromptAsync(
            "Your Name",
            "Enter your name once. It will be saved on this device for future sessions.",
            "Continue",
            "Skip",
            placeholder: "e.g. QA Tester");

        if (string.IsNullOrWhiteSpace(testerName))
        {
            return null;
        }

        testerName = testerName.Trim();
        Preferences.Default.Set(SessionCaptureKeys.TesterName, testerName);
        return testerName;
    }

    private static Drawable CreateTextDrawable(Context context, string text, int textSizeSp, Color color)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        var sizePx = (int)(FabSizeDp * density);
        var bitmap = Bitmap.CreateBitmap(sizePx, sizePx, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap!);

        var paint = new Paint(PaintFlags.AntiAlias)
        {
            TextSize = textSizeSp * density,
            TextAlign = Paint.Align.Center
        };
        paint.SetColor(color);

        var xPosition = sizePx / 2f;
        var yPosition = (sizePx / 2f) - ((paint.Descent() + paint.Ascent()) / 2f);
        canvas.DrawText(text, xPosition, yPosition, paint);

        return new BitmapDrawable(context.Resources, bitmap);
    }

    private sealed class DragTouchListener : Java.Lang.Object, View.IOnTouchListener
    {
        private const float DragThresholdDp = 8f;

        private readonly ViewGroup _parentView;
        private readonly string _prefKeyX;
        private readonly string _prefKeyY;
        private float _deltaX;
        private float _deltaY;
        private float _startRawX;
        private float _startRawY;

        public DragTouchListener(ViewGroup parentView, string prefKeyX, string prefKeyY)
        {
            _parentView = parentView;
            _prefKeyX = prefKeyX;
            _prefKeyY = prefKeyY;
        }

        public bool IsDragging { get; private set; }

        public bool OnTouch(View? view, MotionEvent? motionEvent)
        {
            if (view == null || motionEvent == null)
            {
                return false;
            }

            switch (motionEvent.Action)
            {
                case MotionEventActions.Down:
                    _deltaX = view.GetX() - motionEvent.RawX;
                    _deltaY = view.GetY() - motionEvent.RawY;
                    _startRawX = motionEvent.RawX;
                    _startRawY = motionEvent.RawY;
                    IsDragging = false;
                    return false;

                case MotionEventActions.Move:
                    var deltaX = Math.Abs(motionEvent.RawX - _startRawX);
                    var deltaY = Math.Abs(motionEvent.RawY - _startRawY);
                    var density = view.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
                    var threshold = DragThresholdDp * density;

                    if (deltaX > threshold || deltaY > threshold)
                    {
                        IsDragging = true;
                        var newX = Math.Max(0, Math.Min(motionEvent.RawX + _deltaX, _parentView.Width - view.Width));
                        var newY = Math.Max(0, Math.Min(motionEvent.RawY + _deltaY, _parentView.Height - view.Height));
                        view.SetX(newX);
                        view.SetY(newY);
                    }

                    return IsDragging;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (IsDragging)
                    {
                        Preferences.Default.Set(_prefKeyX, view.GetX());
                        Preferences.Default.Set(_prefKeyY, view.GetY());
                        IsDragging = false;
                        return true;
                    }

                    IsDragging = false;
                    return false;

                default:
                    return false;
            }
        }
    }
}
