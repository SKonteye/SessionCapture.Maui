using CoreAnimation;
using CoreGraphics;
using Foundation;
using SessionCapture.Maui.Models;
using SessionCapture.Maui.Services;
using SessionCapture.Maui.Services.Interfaces;
using UIKit;

namespace SessionCapture.Maui.Platforms.iOS.Services;

public sealed class SessionCaptureOverlayService : ISessionCaptureOverlayService
{
    private const float FabSize = 56f;
    private const float FabCornerRadius = 28f;
    private const float ShadowRadius = 8f;
    private const float ShadowOpacity = 0.3f;
    private const float EdgeMargin = 24f;
    private const float RecordingBarCornerRadius = 24f;
    private const float RecordingBarHeight = 48f;
    private const float RecordingDotSize = 12f;

    private static readonly UIColor FabIdleColor = UIColor.FromRGB(0xE5, 0x39, 0x35);
    private static readonly UIColor RecordingBarColor = UIColor.FromRGB(0x21, 0x21, 0x21);
    private static readonly UIColor RecordingDotColor = UIColor.FromRGB(0xFF, 0x17, 0x44);
    private static readonly UIColor TextIconColor = UIColor.White;

    private ISessionCaptureService? _sessionCaptureService;
    private UIView? _idleFab;
    private UIView? _recordingBar;
    private UILabel? _captureCountLabel;
    private bool _isRecording;
    private int _captureCount;
    private bool _isTemporarilyHidden;
    private nfloat _dragStartX;
    private nfloat _dragStartY;

    public void ShowOverlay(ISessionCaptureService sessionCaptureService)
    {
        _sessionCaptureService = sessionCaptureService;
        _sessionCaptureService.StepCaptured += OnStepCaptured;
        _sessionCaptureService.SessionStarted += OnSessionStarted;
        _sessionCaptureService.SessionStopped += OnSessionStopped;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var rootView = GetRootView();
            if (rootView != null)
            {
                CreateIdleFab(rootView);
            }
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

        MainThread.BeginInvokeOnMainThread(RemoveCurrentOverlay);
        _sessionCaptureService = null;
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

            var rootView = GetRootView();
            if (rootView == null)
            {
                return;
            }

            if (isRecording)
            {
                if (_recordingBar != null && _captureCountLabel != null)
                {
                    _captureCountLabel.Text = captureCount.ToString();
                    return;
                }

                RemoveCurrentOverlay();
                CreateRecordingBar(rootView, captureCount);
                return;
            }

            if (_idleFab != null)
            {
                return;
            }

            RemoveCurrentOverlay();
            CreateIdleFab(rootView);
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
            var rootView = GetRootView();
            if (rootView == null)
            {
                return;
            }

            if (_isRecording)
            {
                CreateRecordingBar(rootView, _captureCount);
            }
            else
            {
                CreateIdleFab(rootView);
            }
        });
    }

    private void CreateIdleFab(UIView rootView)
    {
        _idleFab = new UIView
        {
            BackgroundColor = FabIdleColor,
            UserInteractionEnabled = true
        };
        _idleFab.Layer.CornerRadius = FabCornerRadius;
        _idleFab.Layer.ShadowColor = UIColor.Black.CGColor;
        _idleFab.Layer.ShadowOffset = new CGSize(2, 4);
        _idleFab.Layer.ShadowRadius = ShadowRadius;
        _idleFab.Layer.ShadowOpacity = ShadowOpacity;
        _idleFab.ClipsToBounds = false;

        var symbolConfiguration = UIImageSymbolConfiguration.Create(20, UIImageSymbolWeight.Medium);
        var cameraImage = UIImage.GetSystemImage("camera.fill", symbolConfiguration);
        var iconView = new UIImageView(cameraImage)
        {
            TintColor = TextIconColor,
            ContentMode = UIViewContentMode.ScaleAspectFit,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _idleFab.AddSubview(iconView);

        var position = GetPersistedPosition(rootView, FabSize, FabSize);
        _idleFab.Frame = new CGRect(position.X, position.Y, FabSize, FabSize);
        _idleFab.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin;

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            iconView.CenterXAnchor.ConstraintEqualTo(_idleFab.CenterXAnchor),
            iconView.CenterYAnchor.ConstraintEqualTo(_idleFab.CenterYAnchor),
            iconView.WidthAnchor.ConstraintEqualTo(24),
            iconView.HeightAnchor.ConstraintEqualTo(24)
        });

        _idleFab.AddGestureRecognizer(new UITapGestureRecognizer(HandleIdleFabTap));
        _idleFab.AddGestureRecognizer(new UIPanGestureRecognizer(HandlePanGesture));

        rootView.AddSubview(_idleFab);
        rootView.BringSubviewToFront(_idleFab);
    }

    private void CreateRecordingBar(UIView rootView, int captureCount)
    {
        _recordingBar = new UIView
        {
            BackgroundColor = RecordingBarColor,
            UserInteractionEnabled = true
        };
        _recordingBar.Layer.CornerRadius = RecordingBarCornerRadius;
        _recordingBar.Layer.ShadowColor = UIColor.Black.CGColor;
        _recordingBar.Layer.ShadowOffset = new CGSize(2, 4);
        _recordingBar.Layer.ShadowRadius = ShadowRadius;
        _recordingBar.Layer.ShadowOpacity = ShadowOpacity;
        _recordingBar.ClipsToBounds = false;

        var stackView = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Alignment = UIStackViewAlignment.Center,
            Distribution = UIStackViewDistribution.Fill,
            Spacing = 8,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var redDot = new UIView
        {
            BackgroundColor = RecordingDotColor,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        redDot.Layer.CornerRadius = RecordingDotSize / 2f;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            redDot.WidthAnchor.ConstraintEqualTo(RecordingDotSize),
            redDot.HeightAnchor.ConstraintEqualTo(RecordingDotSize)
        });
        AddPulseAnimation(redDot);

        var recLabel = new UILabel
        {
            Text = "REC",
            TextColor = RecordingDotColor,
            Font = UIFont.BoldSystemFontOfSize(12),
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        _captureCountLabel = new UILabel
        {
            Text = captureCount.ToString(),
            TextColor = TextIconColor,
            Font = UIFont.SystemFontOfSize(14, UIFontWeight.Semibold),
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var captureButton = CreateBarButton("camera.fill", HandleManualCaptureTap);
        var stopButton = CreateBarButton("stop.fill", HandleStopTap);

        stackView.AddArrangedSubview(redDot);
        stackView.AddArrangedSubview(recLabel);
        stackView.AddArrangedSubview(_captureCountLabel);
        stackView.AddArrangedSubview(captureButton);
        stackView.AddArrangedSubview(stopButton);

        _recordingBar.AddSubview(stackView);
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stackView.LeadingAnchor.ConstraintEqualTo(_recordingBar.LeadingAnchor, 16),
            stackView.TrailingAnchor.ConstraintEqualTo(_recordingBar.TrailingAnchor, -16),
            stackView.TopAnchor.ConstraintEqualTo(_recordingBar.TopAnchor),
            stackView.BottomAnchor.ConstraintEqualTo(_recordingBar.BottomAnchor)
        });

        const float barWidth = 220f;
        var position = GetPersistedPosition(rootView, barWidth, RecordingBarHeight);
        _recordingBar.Frame = new CGRect(position.X, position.Y, barWidth, RecordingBarHeight);
        _recordingBar.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin;
        _recordingBar.AddGestureRecognizer(new UIPanGestureRecognizer(HandlePanGesture));

        rootView.AddSubview(_recordingBar);
        rootView.BringSubviewToFront(_recordingBar);
    }

    private UIButton CreateBarButton(string systemImageName, Action tapHandler)
    {
        var button = new UIButton(UIButtonType.Custom);
        var symbolConfiguration = UIImageSymbolConfiguration.Create(16, UIImageSymbolWeight.Medium);
        var image = UIImage.GetSystemImage(systemImageName, symbolConfiguration);
        button.SetImage(image, UIControlState.Normal);
        button.TintColor = TextIconColor;
        button.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            button.WidthAnchor.ConstraintEqualTo(32),
            button.HeightAnchor.ConstraintEqualTo(32)
        });
        button.TouchUpInside += (_, _) => tapHandler();
        return button;
    }

    private async void HandleIdleFabTap()
    {
        if (_sessionCaptureService == null)
        {
            return;
        }

        try
        {
            var testerName = await EnsureTesterNameAsync();
            var sessionName = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mainPage = ApplicationPageResolver.TryGetMainPage();
                if (mainPage == null)
                {
                    return null;
                }

                return await mainPage.DisplayPromptAsync(
                    "Start Capture Session",
                    "Enter a session name (optional):",
                    "Start",
                    "Cancel",
                    placeholder: "e.g. Login flow");
            });

            if (sessionName == null)
            {
                return;
            }

            await _sessionCaptureService.StartSessionAsync(
                string.IsNullOrWhiteSpace(sessionName) ? null : sessionName.Trim(),
                testerName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Error starting session: {ex.Message}");
        }
    }

    private async void HandleManualCaptureTap()
    {
        if (_sessionCaptureService == null || !_sessionCaptureService.IsSessionActive)
        {
            return;
        }

        try
        {
            var currentPage = Shell.Current?.CurrentPage;
            var pageName = currentPage?.GetType().Name ?? "Unknown";
            var viewModelName = currentPage?.BindingContext?.GetType().Name ?? "Unknown";

            var note = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var mainPage = ApplicationPageResolver.TryGetMainPage();
                if (mainPage == null)
                {
                    return null;
                }

                return await mainPage.DisplayPromptAsync(
                    "Manual Capture",
                    "Add a note (optional):",
                    "Capture",
                    "Skip",
                    placeholder: "e.g. User entered VIN manually");
            });

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
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Error capturing manually: {ex.Message}");
        }
    }

    private async void HandleStopTap()
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
            System.Diagnostics.Debug.WriteLine($"SessionCaptureOverlay: Error stopping session: {ex.Message}");
        }
    }

    private void HandlePanGesture(UIPanGestureRecognizer recognizer)
    {
        var view = recognizer.View;
        if (view?.Superview == null)
        {
            return;
        }

        var translation = recognizer.TranslationInView(view.Superview);
        switch (recognizer.State)
        {
            case UIGestureRecognizerState.Began:
                _dragStartX = view.Frame.X;
                _dragStartY = view.Frame.Y;
                break;

            case UIGestureRecognizerState.Changed:
                var newX = _dragStartX + translation.X;
                var newY = _dragStartY + translation.Y;
                var parentBounds = view.Superview.Bounds;
                newX = (nfloat)Math.Max(0, Math.Min((double)newX, (double)(parentBounds.Width - view.Frame.Width)));
                newY = (nfloat)Math.Max(0, Math.Min((double)newY, (double)(parentBounds.Height - view.Frame.Height)));
                view.Frame = new CGRect(newX, newY, view.Frame.Width, view.Frame.Height);
                break;

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
                PersistPosition(view.Frame.X, view.Frame.Y);
                break;
        }
    }

    private void OnStepCaptured(object? sender, CapturedStep step)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_captureCountLabel != null && _sessionCaptureService?.CurrentSession != null)
            {
                _captureCountLabel.Text = _sessionCaptureService.CurrentSession.StepCount.ToString();
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

    private static async Task<string?> EnsureTesterNameAsync()
    {
        var savedName = Preferences.Default.Get(SessionCaptureKeys.TesterName, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedName))
        {
            return savedName.Trim();
        }

        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var mainPage = ApplicationPageResolver.TryGetMainPage();
            if (mainPage == null)
            {
                return null;
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
        });
    }

    private static UIView? GetRootView()
    {
        var windowScene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault();

        var window = windowScene?.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow);
        return window?.RootViewController?.View;
    }

    private static nfloat GetSafeAreaBottom(UIView rootView)
    {
        var windowScene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault();

        var window = windowScene?.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow);
        return window?.SafeAreaInsets.Bottom ?? 0;
    }

    private void RemoveCurrentOverlay()
    {
        _idleFab?.RemoveFromSuperview();
        _idleFab = null;
        _recordingBar?.RemoveFromSuperview();
        _recordingBar = null;
        _captureCountLabel = null;
    }

    private static void AddPulseAnimation(UIView view)
    {
        var animation = CABasicAnimation.FromKeyPath("opacity");
        animation.From = NSNumber.FromFloat(1.0f);
        animation.To = NSNumber.FromFloat(0.3f);
        animation.Duration = 0.8;
        animation.AutoReverses = true;
        animation.RepeatCount = float.MaxValue;
        animation.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseInEaseOut);
        view.Layer.AddAnimation(animation, "pulse");
    }

    private static CGPoint GetPersistedPosition(UIView rootView, nfloat width, nfloat height)
    {
        var safeAreaBottom = GetSafeAreaBottom(rootView);
        var defaultX = rootView.Bounds.Width - width - EdgeMargin;
        var defaultY = rootView.Bounds.Height - height - EdgeMargin - safeAreaBottom;

        var x = (nfloat)Preferences.Default.Get(SessionCaptureKeys.OverlayX, (double)defaultX);
        var y = (nfloat)Preferences.Default.Get(SessionCaptureKeys.OverlayY, (double)defaultY);

        if (x < 0 || x > rootView.Bounds.Width - width)
        {
            x = defaultX;
        }

        if (y < 0 || y > rootView.Bounds.Height - height)
        {
            y = defaultY;
        }

        return new CGPoint(x, y);
    }

    private static void PersistPosition(nfloat x, nfloat y)
    {
        Preferences.Default.Set(SessionCaptureKeys.OverlayX, (double)x);
        Preferences.Default.Set(SessionCaptureKeys.OverlayY, (double)y);
    }
}
