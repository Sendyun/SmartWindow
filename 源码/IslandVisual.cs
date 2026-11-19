using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SmartWindow
{
    internal enum MediaCommandAction
    {
        TogglePlayPause,
        Previous,
        Next
    }

    internal sealed class IslandForm : Form
    {
        private const int SeedSize = 42;
        private const int CompactWidth = 320;
        private const int CompactHeight = 50;
        private const int DetailWidth = 450;
        private const int DetailHeight = 92;
        private const int MusicWidth = 380;
        private const int MusicHeight = 62;
        private const int MusicExpandedHeight = 222;
        private const int SideCompactWidth = 210;
        private const int SideCompactHeight = 120;
        private const int SideDetailWidth = 300;
        private const int SideDetailHeight = 178;
        private const int SideMusicWidth = 300;
        private const int SideMusicHeight = 190;
        private const int SideMusicExpandedHeight = 356;
        private const int TimerWidth = 330;
        private const int TimerHeight = 74;
        private const int TimerExpandedHeight = 184;
        private const int TimerMultiWidth = 700;

        private enum DockEdge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private enum VisualPhase
        {
            Hidden,
            Seed,
            Compact,
            Detail,
            Music,
            Timer,
            Collapsing
        }

        private readonly Queue<NotificationItem> queue =
            new Queue<NotificationItem>();
        private readonly System.Threading.Timer sequenceTimer;
        private readonly System.Threading.Timer controlsTimer;
        private readonly object sequenceSync = new object();
        private readonly GlobalMouseHook mouseHook;
        private readonly WinEventDelegate desktopChangeCallback;
        private IntPtr desktopChangeHook;
        private readonly IslandPerformanceMonitor performanceMonitor =
            new IslandPerformanceMonitor();
        private readonly DockPreviewForm dockPreview;
        private Font microFont;
        private Font titleFont;
        private Font bodyFont;
        private Font tinyFont;
        private Font timerTimeFont;
        private readonly ManualResetEvent frameStopEvent =
            new ManualResetEvent(false);

        public event EventHandler<MediaCommandAction> MediaCommandRequested;
        public event EventHandler<double> VolumeChangeRequested;
        public event EventHandler<double> SeekRequested;
        public event EventHandler SoundSettingsRequested;
        public event EventHandler MediaSourceOpenRequested;
        public event EventHandler MusicWindowCloseRequested;
        public event EventHandler<SmartTimerMode> TimerPauseToggleRequested;
        public event EventHandler<SmartTimerMode> TimerCancelRequested;
        public event EventHandler<SmartTimerMode> TimerLapRequested;
        public event EventHandler DockEdgeChanged;

        private NotificationItem current;
        private VisualPhase phase = VisualPhase.Hidden;
        private int currentWidth;
        private int currentHeight;
        private double animatedWidth;
        private double animatedHeight;
        private double widthVelocity;
        private double heightVelocity;
        private int springTargetWidth = SeedSize;
        private int springTargetHeight = SeedSize;
        private double detailBlend;
        private double detailBlendVelocity;
        private double detailBlendTarget;
        private long lastFrameTimestamp;
        private IntPtr frameWindowHandle;
        private Thread frameThread;
        private int frameMessagePending;
        private volatile bool frameClockRunning;
        private Graphics layeredGraphics;
        private IntPtr layeredMemoryDc;
        private IntPtr layeredDib;
        private IntPtr layeredOriginalBitmap;
        private int layeredSurfaceWidth;
        private int layeredSurfaceHeight;
        private bool renderingLayered;
        private bool paused;
        private double userScale = 1.0;
        private float baseFontDpi = 96f;
        private bool glassModeEnabled;
        private double glassBlend;
        private double glassBlendVelocity;
        private double glassBlendTarget;
        private bool qualityEnhancementEnabled;
        private double qualityBlend;
        private double qualityBlendVelocity;
        private double qualityBlendTarget;
        private bool frostedGlassEnabled;
        private double frostedIntensity = 0.55;
        private double frostedBlend;
        private double frostedBlendVelocity;
        private double frostedBlendTarget;
        private Bitmap frostedNoiseTile;
        private TextureBrush frostedNoiseBrush;
        private Bitmap frostedBackdrop;
        private readonly object frostedBackdropSync = new object();
        private Rectangle frostedBackdropSourceBounds;
        private int frostedBackdropVersion;
        private Bitmap frostedShellCache;
        private int frostedShellWidth;
        private int frostedShellHeight;
        private int frostedShellRadius;
        private int frostedShellVersion;
        private double frostedShellPresence;
        private DateTime nextFrostedBackdropCapture = DateTime.MinValue;
        private DateTime lastFrostedBackdropCapture = DateTime.MinValue;
        private int frostedBackdropCapturePending;
        private volatile bool frostedBackdropDirty = true;
        private double qualityParticleSeconds;
        private double collapseDissolveProgress;
        private DateTime notificationCollapseAt = DateTime.MaxValue;
        private bool movementEnabled;
        private bool clickThroughEnabled;
        private bool draggingWindow;
        private bool dragMoved;
        private bool mouseDownForDrag;
        private bool snappingToDock;
        private Point dragStartScreen;
        private Point dragLastScreen;
        private Point dragGrabOffset;
        private DockEdge dockEdge = DockEdge.Top;
        private int dockPosition = 1;
        private string dockScreenDevice = "";
        private double dockSnapBlend = 1.0;
        private double dockSnapVelocity;
        private DateTime suppressClickUntil = DateTime.MinValue;
        private bool musicModeEnabled;
        private bool musicPinned;
        private bool currentIsMedia;
        private bool musicDismissed;
        private bool musicEnding;
        private bool musicPaused;
        private bool musicPauseEndPending;
        private bool musicControlsVisible;
        private bool currentIsTimer;
        private bool timerExpanded;
        private double timerAnimationSeconds;
        private TimerSnapshot timerSnapshot;
        private bool draggingVolume;
        private int hoveredMusicButton = -1;
        private MediaPlaybackState mediaState;
        private Image albumArtwork;
        private string albumArtworkPath;
        private string albumArtworkSignature;
        private string pendingArtworkPath;
        private string pendingArtworkSignature;
        private int artworkRequestGeneration;
        private bool artworkLoadInProgress;
        private DateTime artworkRetryAfter = DateTime.MinValue;
        private Color musicAccent = Color.FromArgb(240, 218, 151);
        private double waveformMotion = 1.0;
        private double waveformMotionVelocity;
        private double waveformMotionTarget = 1.0;
        private double waveformSeconds;
        private double volumeLevel = 0.5;
        private Action queuedAction;
        private int sequenceGeneration;
        private bool resourcesDisposed;

        private const uint FrameMessage =
            0x8000 + 0x173;
        private const int ActiveFrameRate = 165;
        private const int WaveformFrameRate = 30;
        private const int IdleFrameRate = 30;
        private int requestedFrameRate = ActiveFrameRate;
        private int frameRateLimit = 120;

        public IslandForm()
        {
            Text = Localization.Get("appName");
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(14, 14, 16);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;

            float fontDpi;
            using (Graphics graphics = CreateGraphics())
            {
                fontDpi = graphics.DpiX;
            }
            baseFontDpi = fontDpi;
            Width = Scale(SeedSize);
            Height = Scale(SeedSize);
            Cursor = Cursors.Hand;
            Opacity = 1.0;

            CreateFonts(fontDpi * (float)userScale);

            sequenceTimer = new System.Threading.Timer(
                OnSequenceTimer,
                null,
                Timeout.Infinite,
                Timeout.Infinite);

            controlsTimer = new System.Threading.Timer(
                OnControlsTimer,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
            mouseHook = new GlobalMouseHook(OnGlobalMouseDown);
            dockPreview = new DockPreviewForm();
            desktopChangeCallback = new WinEventDelegate(
                OnDesktopWindowChanged);
            desktopChangeHook = SetWinEventHook(
                0x8002,
                0x800B,
                IntPtr.Zero,
                desktopChangeCallback,
                0,
                0,
                0x0002);

            MouseEnter += delegate
            {
                RequestRender();
            };
            MouseLeave += delegate
            {
                RequestRender();
            };
            MouseClick += OnIslandClick;
            MouseDoubleClick += OnIslandDoubleClick;
            MouseDown += OnIslandMouseDown;
            MouseMove += OnIslandMouseMove;
            MouseUp += OnIslandMouseUp;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                parameters.ExStyle |= 0x00080000; // WS_EX_LAYERED
                if (clickThroughEnabled)
                {
                    parameters.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                }
                return parameters;
            }
        }

        public void DemonstrateStartup(bool bannersMuted)
        {
            NotificationItem item = new NotificationItem();
            item.AppName = Localization.Get("appName");
            item.Title = Localization.Get("startupTitle");
            item.Body = bannersMuted
                ? Localization.Get("startupBody")
                : Localization.Get("startupDemo");
            Receive(item);
        }

        public void Demonstrate()
        {
            NotificationItem item = new NotificationItem();
            item.AppName = Localization.Get("appName");
            item.Title = Localization.Get("demoTitle");
            item.Body = Localization.Get("demoBody");
            Receive(item);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (paused)
            {
                CancelQueuedAction();
                StopFrameClock();
                StopControlsTimeout();
                queue.Clear();
                current = null;
                currentIsMedia = false;
                musicControlsVisible = false;
                CancelNotificationCollapse();
                phase = VisualPhase.Hidden;
                Hide();
            }
            else if (musicModeEnabled &&
                mediaState != null &&
                mediaState.IsPlaying &&
                current == null &&
                phase == VisualPhase.Hidden)
            {
                StartMusic();
            }
        }

        public void DismissNotificationForPolicy()
        {
            if (current != null && !currentIsMedia)
            {
                DismissCurrent();
            }
        }

        public void Receive(NotificationItem item)
        {
            if (item == null || paused || IsDisposed)
            {
                return;
            }

            if (currentIsMedia && current != null &&
                phase != VisualPhase.Collapsing)
            {
                queue.Enqueue(item);
                musicEnding = true;
                waveformMotionTarget = 0.0;
                CollapseNow();
                return;
            }

            queue.Enqueue(item);
            if (current == null && phase == VisualPhase.Hidden)
            {
                StartNext();
            }
        }

        public void SetMusicMode(bool enabled)
        {
            musicModeEnabled = enabled;
            if (!enabled)
            {
                mediaState = null;
                musicDismissed = false;
                StopControlsTimeout();
                if (currentIsMedia && current != null)
                {
                    musicEnding = true;
                    waveformMotionTarget = 0.0;
                    CollapseNow();
                }
                return;
            }

            musicDismissed = false;
            if (current == null &&
                phase == VisualPhase.Hidden &&
                (musicPinned ||
                 (mediaState != null && mediaState.IsPlaying)))
            {
                StartMusic();
            }
        }

        public void SetMusicPinned(bool pinned)
        {
            musicPinned = pinned;
            if (pinned)
            {
                if (current == null && phase == VisualPhase.Hidden)
                {
                    StartMusic();
                }
                return;
            }

            if (currentIsMedia && current != null &&
                musicPaused && phase == VisualPhase.Music)
            {
                CancelQueuedAction();
                QueueAction(BeginMusicEnd, 260);
            }
        }

        public void UpdateTimer(TimerSnapshot snapshot)
        {
            if (IsDisposed)
            {
                return;
            }

            timerSnapshot = snapshot;
            if (snapshot != null && snapshot.IsActive)
            {
                if (!currentIsTimer)
                {
                    StartTimerVisual();
                }
                else
                {
                    RequestRender();
                }
                return;
            }

            if (currentIsTimer && phase != VisualPhase.Collapsing)
            {
                StopControlsTimeout();
                CollapseNow();
            }
        }

        private void StartTimerVisual()
        {
            CancelQueuedAction();
            StopControlsTimeout();
            if (current != null &&
                !currentIsMedia &&
                !currentIsTimer)
            {
                queue.Enqueue(current);
            }
            current = new NotificationItem();
            current.AppName = Localization.Get("appName");
            current.Title = Localization.Get("timer");
            current.Body = "";
            if (mediaState != null)
            {
                current.ArtworkPath = mediaState.ArtworkPath;
                current.MediaSignature = mediaState.Signature;
            }
            currentIsMedia = false;
            currentIsTimer = true;
            musicEnding = false;
            timerExpanded = false;
            phase = VisualPhase.Seed;
            Opacity = 1.0;
            animatedWidth = Scale(SeedSize);
            animatedHeight = Scale(SeedSize);
            currentWidth = (int)Math.Round(animatedWidth);
            currentHeight = (int)Math.Round(animatedHeight);
            widthVelocity = 0.0;
            heightVelocity = 0.0;
            detailBlend = 0.0;
            detailBlendVelocity = 0.0;
            detailBlendTarget = 0.0;
            springTargetWidth = currentWidth;
            springTargetHeight = currentHeight;
            if (!Visible)
            {
                Show();
            }
            ApplyGeometry();
            StartFrameClock();
            QueueAction(StartTimerExpanded, 110);
        }

        private void StartTimerExpanded()
        {
            if (!currentIsTimer || timerSnapshot == null)
            {
                return;
            }
            phase = VisualPhase.Timer;
            SetSpringTarget(
                timerExpanded && HasMusicCompanion()
                    ? TimerMultiWindowWidth
                    : TimerWindowWidth,
                timerExpanded && HasMusicCompanion()
                    ? TimerMultiWindowHeight
                    : (timerExpanded
                        ? TimerExpandedWindowHeight
                        : TimerCompactWindowHeight),
                0.0);
        }

        public void SetUiScale(double scale)
        {
            userScale = Math.Max(0.80, Math.Min(1.20, scale));
            CreateFonts(baseFontDpi * (float)userScale);
            if (current == null || phase == VisualPhase.Hidden)
            {
                RequestRender();
                return;
            }

            if (phase == VisualPhase.Collapsing)
            {
                SetSpringTarget(
                    Scale(SeedSize),
                    Scale(SeedSize),
                    0.0);
            }
            else if (currentIsMedia)
            {
                SetSpringTarget(
                    MusicTargetWidth,
                    Scale(musicControlsVisible
                        ? (SideDocked
                            ? SideMusicExpandedHeight
                            : MusicExpandedHeight)
                        : (SideDocked
                            ? SideMusicHeight
                            : MusicHeight)),
                    0.0);
            }
            else
            {
                int width = phase == VisualPhase.Detail
                    ? NotificationTargetWidth(false)
                    : (phase == VisualPhase.Compact
                        ? NotificationTargetWidth(true)
                        : Scale(SeedSize));
                int height = phase == VisualPhase.Detail
                    ? NotificationTargetHeight(false)
                    : (phase == VisualPhase.Compact
                        ? NotificationTargetHeight(true)
                        : Scale(SeedSize));
                SetSpringTarget(width, height, detailBlendTarget);
            }
            RequestRender();
        }

        public void SetGlassMode(bool enabled)
        {
            glassModeEnabled = enabled;
            glassBlendTarget = enabled ? 1.0 : 0.0;
            qualityBlendTarget =
                enabled && qualityEnhancementEnabled ? 1.0 : 0.0;
            frostedBlendTarget =
                enabled && frostedGlassEnabled ? 1.0 : 0.0;
            if (current == null || phase == VisualPhase.Hidden)
            {
                glassBlend = glassBlendTarget;
                glassBlendVelocity = 0.0;
                qualityBlend = qualityBlendTarget;
                qualityBlendVelocity = 0.0;
                frostedBlend = frostedBlendTarget;
                frostedBlendVelocity = 0.0;
                RequestRender();
                return;
            }
            StartFrameClock();
            RequestRender();
        }

        public void SetQualityEnhancement(bool enabled)
        {
            qualityEnhancementEnabled = enabled;
            qualityBlendTarget =
                enabled && glassModeEnabled ? 1.0 : 0.0;
            if (current == null || phase == VisualPhase.Hidden)
            {
                qualityBlend = qualityBlendTarget;
                qualityBlendVelocity = 0.0;
                RequestRender();
                return;
            }
            StartFrameClock();
            RequestRender();
        }

        public void SetFrostedGlass(bool enabled, double intensity)
        {
            frostedGlassEnabled = enabled;
            frostedIntensity = Clamp01(intensity);
            frostedBlendTarget =
                enabled && glassModeEnabled ? 1.0 : 0.0;
            RebuildFrostedNoiseTile();
            nextFrostedBackdropCapture = DateTime.MinValue;
            if (current == null || phase == VisualPhase.Hidden)
            {
                frostedBlend = frostedBlendTarget;
                frostedBlendVelocity = 0.0;
                RequestRender();
                return;
            }
            StartFrameClock();
            RequestRender();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            nextFrostedBackdropCapture = DateTime.MinValue;
        }

        private void RefreshFrostedBackdrop()
        {
            if (!IsHandleCreated ||
                IsDisposed ||
                frostedBlend < 0.01 ||
                !glassModeEnabled ||
                !frostedGlassEnabled)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            bool geometryMoving =
                draggingWindow ||
                snappingToDock ||
                phase == VisualPhase.Collapsing ||
                Math.Abs(widthVelocity) > 0.05 ||
                Math.Abs(heightVelocity) > 0.05 ||
                Math.Abs(animatedWidth - springTargetWidth) > 0.25 ||
                Math.Abs(animatedHeight - springTargetHeight) > 0.25;
            if (geometryMoving)
            {
                frostedBackdropDirty = true;
                nextFrostedBackdropCapture = now.AddMilliseconds(40);
                return;
            }
            if (frostedBackdropDirty)
            {
                frostedBackdropDirty = false;
                nextFrostedBackdropCapture = DateTime.MinValue;
            }
            if (lastFrostedBackdropCapture != DateTime.MinValue &&
                now - lastFrostedBackdropCapture < TimeSpan.FromMilliseconds(280))
            {
                nextFrostedBackdropCapture =
                    lastFrostedBackdropCapture.AddMilliseconds(280);
                return;
            }
            if (now < nextFrostedBackdropCapture)
            {
                return;
            }
            nextFrostedBackdropCapture = now.AddSeconds(5);
            lastFrostedBackdropCapture = now;

            Rectangle captureBounds = Bounds;
            if (captureBounds.Width < Scale(24) ||
                captureBounds.Height < Scale(12))
            {
                return;
            }

            QueueFrostedBackdropCapture(captureBounds);
        }

        private void QueueFrostedBackdropCapture(Rectangle captureBounds)
        {
            if (!IsHandleCreated ||
                IsDisposed ||
                captureBounds.Width < Scale(24) ||
                captureBounds.Height < Scale(12) ||
                Interlocked.CompareExchange(
                    ref frostedBackdropCapturePending,
                    1,
                    0) != 0)
            {
                return;
            }

            IntPtr windowHandle = Handle;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    CaptureFrostedBackdrop(windowHandle, captureBounds);
                }
                finally
                {
                    Interlocked.Exchange(
                        ref frostedBackdropCapturePending,
                        0);
                }
            });
        }

        private void CaptureFrostedBackdrop(
            IntPtr windowHandle,
            Rectangle captureBounds)
        {
            bool captureExcluded = false;
            try
            {
                if (!SetWindowDisplayAffinity(
                    windowHandle,
                    0x00000011))
                {
                    return;
                }
                captureExcluded = true;

                Bitmap captured = new Bitmap(
                    captureBounds.Width,
                    captureBounds.Height,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    using (Graphics captureGraphics =
                        Graphics.FromImage(captured))
                    {
                        captureGraphics.CopyFromScreen(
                            captureBounds.Left,
                            captureBounds.Top,
                            0,
                            0,
                            captureBounds.Size,
                            CopyPixelOperation.SourceCopy);
                    }

                    int sampleWidth = Math.Max(
                        2,
                        (int)Math.Round(captureBounds.Width / 14.0));
                    int sampleHeight = Math.Max(
                        2,
                        (int)Math.Round(captureBounds.Height / 14.0));
                    using (Bitmap sampled = new Bitmap(
                        sampleWidth,
                        sampleHeight,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    using (Graphics sampleGraphics =
                        Graphics.FromImage(sampled))
                    {
                        sampleGraphics.InterpolationMode =
                            InterpolationMode.HighQualityBilinear;
                        sampleGraphics.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;
                        sampleGraphics.DrawImage(
                            captured,
                            new Rectangle(
                                0,
                                0,
                                sampleWidth,
                                sampleHeight));

                        Bitmap blurred = new Bitmap(
                            captureBounds.Width,
                            captureBounds.Height,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (Graphics blurGraphics =
                            Graphics.FromImage(blurred))
                        {
                            blurGraphics.InterpolationMode =
                                InterpolationMode.HighQualityBicubic;
                            blurGraphics.PixelOffsetMode =
                                PixelOffsetMode.HighQuality;
                            blurGraphics.DrawImage(
                                sampled,
                                new Rectangle(
                                    0,
                                    0,
                                    blurred.Width,
                                    blurred.Height));
                        }
                        lock (frostedBackdropSync)
                        {
                            Bitmap previous = frostedBackdrop;
                            frostedBackdrop = blurred;
                            frostedBackdropSourceBounds = captureBounds;
                            frostedBackdropVersion++;
                            if (previous != null)
                            {
                                previous.Dispose();
                            }
                        }
                    }
                }
                finally
                {
                    captured.Dispose();
                }
            }
            catch (Exception exception)
            {
                Logger.Write("RefreshFrostedBackdrop: " + exception);
            }
            finally
            {
                if (captureExcluded)
                {
                    SetWindowDisplayAffinity(windowHandle, 0);
                }
            }
        }

        public void SetVolume(double value)
        {
            volumeLevel = Math.Max(0.0, Math.Min(1.0, value));
            RequestRender();
        }

        public void SetFrameRateLimit(int value)
        {
            frameRateLimit = Math.Max(60, Math.Min(165, value));
            UpdateRequestedFrameRate();
        }

        public PerformanceSnapshot GetPerformanceSnapshot()
        {
            return performanceMonitor.GetSnapshot();
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!enabled)
            {
                mouseDownForDrag = false;
                draggingWindow = false;
                dragMoved = false;
                snappingToDock = false;
                dockSnapBlend = 1.0;
                dockSnapVelocity = 0.0;
                Capture = false;
                dockPreview.HidePreview();
            }
            Cursor = enabled ? Cursors.SizeAll : Cursors.Hand;
            RequestRender();
        }

        public void SetClickThrough(bool enabled)
        {
            clickThroughEnabled = enabled;
            if (enabled)
            {
                movementEnabled = false;
                mouseDownForDrag = false;
                draggingWindow = false;
                dragMoved = false;
                snappingToDock = false;
                dockSnapBlend = 1.0;
                dockSnapVelocity = 0.0;
                Capture = false;
                dockPreview.HidePreview();
            }
            ApplyClickThroughStyle();
            Cursor = clickThroughEnabled
                ? Cursors.Default
                : (movementEnabled ? Cursors.SizeAll : Cursors.Hand);
            RequestRender();
        }

        public void SetDockEdge(string edge)
        {
            DockEdge parsed;
            if (!TryParseDockEdge(edge, out parsed))
            {
                parsed = DockEdge.Top;
            }
            dockEdge = parsed;
            if (!draggingWindow)
            {
                snappingToDock = false;
                dockSnapBlend = 1.0;
                dockSnapVelocity = 0.0;
                ApplyGeometry();
            }
            RequestRender();
        }

        public void SetDockPosition(int position)
        {
            dockPosition = Math.Max(0, Math.Min(2, position));
            if (!draggingWindow)
            {
                snappingToDock = false;
                dockSnapBlend = 1.0;
                dockSnapVelocity = 0.0;
                ApplyGeometry();
            }
            RequestRender();
        }

        public void SetDockScreen(string deviceName)
        {
            dockScreenDevice = deviceName ?? "";
            ApplyGeometry();
            RequestRender();
        }

        public void ReapplyGeometry()
        {
            ApplyGeometry();
            RequestRender();
        }

        public void EnsureAnimationClock()
        {
            if (current != null &&
                phase != VisualPhase.Hidden &&
                !IsDisposed)
            {
                StartFrameClock();
                RequestRender();
            }
        }

        public bool MovementEnabled
        {
            get { return movementEnabled; }
        }

        public bool ClickThroughEnabled
        {
            get { return clickThroughEnabled; }
        }

        public string DockEdgeName
        {
            get
            {
                if (dockEdge == DockEdge.Bottom)
                {
                    return "bottom";
                }
                if (dockEdge == DockEdge.Left)
                {
                    return "left";
                }
                if (dockEdge == DockEdge.Right)
                {
                    return "right";
                }
                return "top";
            }
        }

        public int DockPosition
        {
            get { return dockPosition; }
        }

        private bool SideDocked
        {
            get
            {
                return dockEdge == DockEdge.Left ||
                    dockEdge == DockEdge.Right;
            }
        }

        public string DockScreenName
        {
            get { return dockScreenDevice ?? ""; }
        }

        private int MusicTargetWidth
        {
            get
            {
                return Scale(SideDocked ? SideMusicWidth : MusicWidth);
            }
        }

        private int TimerWindowWidth
        {
            get { return Scale(SideDocked ? 260 : TimerWidth); }
        }

        private int TimerCompactWindowHeight
        {
            get { return Scale(SideDocked ? 178 : TimerHeight); }
        }

        private int TimerExpandedWindowHeight
        {
            get { return Scale(SideDocked ? 320 : TimerExpandedHeight); }
        }

        private int TimerMultiWindowWidth
        {
            get { return Scale(SideDocked ? 260 : TimerMultiWidth); }
        }

        private int TimerMultiWindowHeight
        {
            get { return Scale(SideDocked ? 376 : TimerExpandedHeight); }
        }

        private int NotificationTargetWidth(bool compact)
        {
            if (SideDocked)
            {
                return Scale(
                    compact ? SideCompactWidth : SideDetailWidth);
            }
            return Scale(compact ? CompactWidth : DetailWidth);
        }

        private int NotificationTargetHeight(bool compact)
        {
            if (SideDocked)
            {
                return Scale(
                    compact ? SideCompactHeight : SideDetailHeight);
            }
            return Scale(compact ? CompactHeight : DetailHeight);
        }

        public void ShowScalePreview(double scale)
        {
            if (paused || IsDisposed)
            {
                return;
            }
            string sizeLabel = scale < 0.94
                ? Localization.Get("sizeSmall")
                : (scale > 1.06
                    ? Localization.Get("sizeLarge")
                    : Localization.Get("sizeMedium"));
            string body = ((int)Math.Round(scale * 100)).ToString() +
                "% · " + sizeLabel;

            if (current != null && current.IsSizePreview)
            {
                current.Title = Localization.Get("sizeTitle");
                current.Body = body;
                RequestRender();
                return;
            }
            if (current != null)
            {
                return;
            }

            NotificationItem item = new NotificationItem();
            item.AppName = Localization.Get("appName");
            item.Title = Localization.Get("sizeTitle");
            item.Body = body;
            item.IsSizePreview = true;
            Receive(item);
        }

        public void UpdateMedia(MediaPlaybackState state)
        {
            if (!musicModeEnabled || IsDisposed)
            {
                return;
            }

            bool newPlayback =
                state != null &&
                state.IsPlaying &&
                (mediaState == null ||
                 !mediaState.IsPlaying ||
                 !string.Equals(
                    mediaState.Signature,
                    state.Signature,
                    StringComparison.Ordinal));
            if (state != null && state.IsPlaying)
            {
                mediaState = state;
            }
            else if (mediaState != null)
            {
                mediaState.IsPlaying = false;
            }
            if (newPlayback)
            {
                musicDismissed = false;
                if (currentIsMedia)
                {
                    waveformSeconds = 0.0;
                }
            }
            if (currentIsTimer && timerSnapshot != null)
            {
                musicPaused =
                    state == null ||
                    !state.IsPlaying;
                if (state != null)
                {
                    current.ArtworkPath = state.ArtworkPath;
                    current.MediaSignature = state.Signature;
                }
                SetSpringTarget(
                    Scale(timerExpanded && HasMusicCompanion()
                        ? TimerMultiWindowWidth
                        : TimerWindowWidth),
                    timerExpanded && HasMusicCompanion()
                        ? TimerMultiWindowHeight
                        : (timerExpanded
                            ? TimerExpandedWindowHeight
                            : TimerCompactWindowHeight),
                    0.0);
                StartFrameClock();
                RequestRender();
                return;
            }
            if (state == null || !state.IsPlaying)
            {
                if (currentIsMedia && current != null &&
                    phase != VisualPhase.Collapsing)
                {
                    musicPaused = true;
                    musicPauseEndPending = !musicPinned;
                    waveformMotionTarget = 0.0;
                    CancelQueuedAction();
                    if (!musicPinned && !musicControlsVisible)
                    {
                        musicPauseEndPending = false;
                        QueueAction(BeginMusicEnd, 2500);
                    }
                    RequestRender();
                }
                return;
            }

            if (currentIsMedia && current != null)
            {
                bool wasPaused = musicPaused;
                bool yieldingToNotification =
                    phase == VisualPhase.Collapsing &&
                    musicEnding &&
                    queue.Count > 0;
                musicPaused = false;
                musicPauseEndPending = false;
                waveformMotionTarget = 1.0;
                if (wasPaused ||
                    (phase == VisualPhase.Collapsing &&
                     !yieldingToNotification))
                {
                    CancelQueuedAction();
                }
                if (phase == VisualPhase.Collapsing &&
                    !yieldingToNotification)
                {
                    musicEnding = false;
                    phase = VisualPhase.Music;
                    SetSpringTarget(
                        MusicTargetWidth,
                        Scale(musicControlsVisible
                            ? (SideDocked
                                ? SideMusicExpandedHeight
                                : MusicExpandedHeight)
                            : (SideDocked
                                ? SideMusicHeight
                                : MusicHeight)),
                        0.0);
                }
                current.Title = string.IsNullOrWhiteSpace(state.Title)
                    ? Localization.Get("nowPlaying")
                    : state.Title;
                current.Body = string.IsNullOrWhiteSpace(state.Artist)
                    ? state.Album
                    : state.Artist;
                current.AppName = string.IsNullOrWhiteSpace(state.AppName)
                    ? Localization.Get("media")
                    : state.AppName;
                current.ArtworkPath = state.ArtworkPath;
                current.MediaSignature = state.Signature;
                RequestRender();
                return;
            }

            if (current == null && phase == VisualPhase.Hidden)
            {
                StartMusic();
            }
        }

        private void StartMusic()
        {
            if (!musicModeEnabled || paused || current != null)
            {
                return;
            }
            if (mediaState == null)
            {
                if (!musicPinned)
                {
                    return;
                }
                mediaState = new MediaPlaybackState();
                mediaState.AppName = Localization.Get("appName");
                mediaState.Title = Localization.Get("musicWindowTitle");
                mediaState.Artist = Localization.Get("waitingMedia");
                mediaState.Signature = "smartwindow-persistent-idle";
            }
            if (!mediaState.IsPlaying && !musicPinned)
            {
                return;
            }

            current = CreateMediaItem(mediaState);
            currentIsMedia = true;
            musicEnding = false;
            musicPaused = !mediaState.IsPlaying;
            musicPauseEndPending = false;
            musicControlsVisible = false;
            waveformMotion = 0.0;
            waveformMotionVelocity = 0.0;
            waveformMotionTarget = musicPaused ? 0.0 : 1.0;
            waveformSeconds = 0.0;
            phase = VisualPhase.Seed;
            StopControlsTimeout();
            Opacity = 1.0;
            animatedWidth = Scale(SeedSize);
            animatedHeight = Scale(SeedSize);
            currentWidth = (int)Math.Round(animatedWidth);
            currentHeight = (int)Math.Round(animatedHeight);
            widthVelocity = 0.0;
            heightVelocity = 0.0;
            detailBlend = 0.0;
            detailBlendVelocity = 0.0;
            detailBlendTarget = 0.0;
            springTargetWidth = currentWidth;
            springTargetHeight = currentHeight;
            PrefetchFrostedBackdropForTarget();
            if (!Visible)
            {
                Show();
            }
            ApplyGeometry();
            StartFrameClock();
            QueueAction(StartMusicExpanded, 120);
        }

        private void StartMusicExpanded()
        {
            if (!currentIsMedia || current == null)
            {
                return;
            }
            phase = VisualPhase.Music;
            waveformMotionTarget = musicPaused ? 0.0 : 1.0;
            SetSpringTarget(
                MusicTargetWidth,
                Scale(musicControlsVisible
                    ? (SideDocked
                        ? SideMusicExpandedHeight
                        : MusicExpandedHeight)
                    : (SideDocked
                        ? SideMusicHeight
                        : MusicHeight)),
                0.0);
        }

        private static NotificationItem CreateMediaItem(
            MediaPlaybackState state)
        {
            NotificationItem item = new NotificationItem();
            item.IsMedia = true;
            item.AppName = string.IsNullOrWhiteSpace(state.AppName)
                ? Localization.Get("media")
                : state.AppName;
            item.Title = string.IsNullOrWhiteSpace(state.Title)
                ? Localization.Get("nowPlaying")
                : state.Title;
            item.Body = string.IsNullOrWhiteSpace(state.Artist)
                ? state.Album
                : state.Artist;
            item.ArtworkPath = state.ArtworkPath;
            item.MediaSignature = state.Signature;
            item.ArrivedAt = DateTime.Now;
            return item;
        }

        private void StartNext()
        {
            if (current != null)
            {
                return;
            }
            if (paused || queue.Count == 0)
            {
                HideIsland();
                return;
            }

            current = queue.Dequeue();
            currentIsMedia = false;
            CancelNotificationCollapse();
            phase = VisualPhase.Seed;
            Opacity = 1.0;
            animatedWidth = Scale(SeedSize);
            animatedHeight = Scale(SeedSize);
            currentWidth = (int)Math.Round(animatedWidth);
            currentHeight = (int)Math.Round(animatedHeight);
            widthVelocity = 0.0;
            heightVelocity = 0.0;
            detailBlend = 0.0;
            detailBlendVelocity = 0.0;
            detailBlendTarget = 0.0;
            springTargetWidth = currentWidth;
            springTargetHeight = currentHeight;
            PrefetchFrostedBackdropForTarget();
            if (!Visible)
            {
                Show();
            }
            ApplyGeometry();
            StartFrameClock();
            QueueAction(StartCompact, 120);
        }

        private void StartCompact()
        {
            if (current == null)
            {
                return;
            }

            phase = VisualPhase.Compact;
            SetSpringTarget(
                NotificationTargetWidth(true),
                NotificationTargetHeight(true),
                0.0);
            QueueAction(StartDetail, 520);
        }

        private void StartDetail()
        {
            if (current == null)
            {
                return;
            }

            phase = VisualPhase.Detail;
            SetSpringTarget(
                NotificationTargetWidth(false),
                NotificationTargetHeight(false),
                1.0);
            ScheduleNotificationCollapse(5200);
        }

        private void BeginCollapse()
        {
            if (current == null)
            {
                return;
            }
            if (IsPointerOverIsland())
            {
                ScheduleNotificationCollapse(1200);
                return;
            }

            CollapseNow();
        }

        private void ScheduleNotificationCollapse(int delayMilliseconds)
        {
            notificationCollapseAt = DateTime.UtcNow.AddMilliseconds(
                Math.Max(1, delayMilliseconds));
        }

        private void CancelNotificationCollapse()
        {
            notificationCollapseAt = DateTime.MaxValue;
        }

        private bool IsPointerOverIsland()
        {
            if (!Visible ||
                clickThroughEnabled ||
                Bounds.Width <= 0 ||
                Bounds.Height <= 0)
            {
                return false;
            }
            Point clientPoint = PointToClient(Control.MousePosition);
            return ClientRectangle.Contains(clientPoint);
        }

        private void BeginMusicEnd()
        {
            if (currentIsMedia && musicPaused &&
                phase == VisualPhase.Music)
            {
                musicEnding = true;
                CollapseNow();
            }
        }

        private void CollapseNow()
        {
            CancelQueuedAction();
            CancelNotificationCollapse();
            StopControlsTimeout();
            collapseDissolveProgress = 0.0;
            phase = VisualPhase.Collapsing;
            if (qualityBlend > 0.1)
            {
                SetSpringTarget(
                    currentWidth,
                    currentHeight,
                    0.0);
                QueueAction(StartQualityCollapseShrink, 210);
                RequestRender();
                return;
            }
            SetSpringTarget(
                Scale(SeedSize),
                Scale(SeedSize),
                0.0);
            QueueAction(CollapseToDot, 500);
        }

        private void StartQualityCollapseShrink()
        {
            if (phase != VisualPhase.Collapsing)
            {
                return;
            }
            SetSpringTarget(
                Scale(SeedSize),
                Scale(SeedSize),
                0.0);
            QueueAction(CollapseToDot, 520);
        }

        private void CollapseToDot()
        {
            if (phase != VisualPhase.Collapsing)
            {
                return;
            }
            SetSpringTarget(
                Scale(10),
                Scale(10),
                0.0);
            QueueAction(FinishCollapse, qualityBlend > 0.1 ? 250 : 210);
        }

        private void FinishCollapse()
        {
            StopFrameClock();
            HideIsland();
        }

        private void HideIsland()
        {
            current = null;
            currentIsMedia = false;
            currentIsTimer = false;
            timerExpanded = false;
            CancelNotificationCollapse();
            musicEnding = false;
            musicPaused = false;
            musicPauseEndPending = false;
            musicControlsVisible = false;
            waveformMotion = 0.0;
            waveformMotionVelocity = 0.0;
            waveformMotionTarget = 1.0;
            phase = VisualPhase.Hidden;
            Opacity = 1.0;
            Hide();
            if (!paused && queue.Count > 0)
            {
                QueueAction(StartNext, 130);
            }
            else if (!paused &&
                musicModeEnabled &&
                !musicDismissed &&
                (musicPinned ||
                 (mediaState != null && mediaState.IsPlaying)))
            {
                QueueAction(StartMusic, 160);
            }
        }

        private void DismissCurrent()
        {
            if (current == null)
            {
                return;
            }
            if (currentIsTimer)
            {
                phase = VisualPhase.Timer;
                timerExpanded = false;
                SetSpringTarget(
                    TimerWindowWidth,
                    TimerCompactWindowHeight,
                    0.0);
            }
            else if (currentIsMedia)
            {
                musicDismissed = true;
                musicEnding = true;
                waveformMotionTarget = 0.0;
            }
            CancelQueuedAction();
            CollapseNow();
        }

        private void QueueAction(Action action, int delay)
        {
            lock (sequenceSync)
            {
                queuedAction = action;
                sequenceGeneration++;
                sequenceTimer.Change(
                    Math.Max(1, delay),
                    Timeout.Infinite);
            }
        }

        private void CancelQueuedAction()
        {
            lock (sequenceSync)
            {
                queuedAction = null;
                sequenceGeneration++;
                sequenceTimer.Change(
                    Timeout.Infinite,
                    Timeout.Infinite);
            }
        }

        private void OnSequenceTimer(object state)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }
            int generation;
            lock (sequenceSync)
            {
                generation = sequenceGeneration;
            }
            try
            {
                BeginInvoke(new Action(delegate
                {
                    RunQueuedAction(generation);
                }));
            }
            catch
            {
            }
        }

        private void RunQueuedAction(int generation)
        {
            Action action;
            lock (sequenceSync)
            {
                if (generation != sequenceGeneration)
                {
                    return;
                }
                action = queuedAction;
                queuedAction = null;
            }
            if (action != null)
            {
                action();
            }
        }

        private void SetSpringTarget(
            int targetWidth,
            int targetHeight,
            double targetDetailBlend)
        {
            springTargetWidth = targetWidth;
            springTargetHeight = targetHeight;
            detailBlendTarget = targetDetailBlend;
        }

        private void StartFrameClock()
        {
            frameWindowHandle = Handle;
            if (frameClockRunning &&
                frameThread != null &&
                frameThread.IsAlive)
            {
                return;
            }

            frameClockRunning = false;
            frameStopEvent.Reset();
            lastFrameTimestamp = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref frameMessagePending, 0);
            Volatile.Write(
                ref requestedFrameRate,
                Math.Min(ActiveFrameRate, frameRateLimit));
            frameClockRunning = true;
            frameThread = new Thread(FrameLoop);
            frameThread.IsBackground = true;
            frameThread.Priority = ThreadPriority.Normal;
            frameThread.Name = "SmartWindow.165Hz";
            frameThread.Start();
        }

        private void StopFrameClock()
        {
            frameClockRunning = false;
            try
            {
                if (!frameStopEvent.SafeWaitHandle.IsClosed)
                {
                    frameStopEvent.Set();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            Thread thread = frameThread;
            frameThread = null;
            if (thread != null && thread != Thread.CurrentThread)
            {
                thread.Join(100);
            }
        }

        private void FrameLoop()
        {
            timeBeginPeriod(1);
            try
            {
                long nextFrame = Stopwatch.GetTimestamp();
                while (frameClockRunning)
                {
                    DwmFlush();
                    int frameRate = Math.Max(
                        1,
                        Volatile.Read(ref requestedFrameRate));
                    long frameTicks = Math.Max(
                        1L,
                        (long)(Stopwatch.Frequency /
                            (double)frameRate));
                    long now = Stopwatch.GetTimestamp();
                    if (now < nextFrame)
                    {
                        int waitMilliseconds = (int)(
                            (nextFrame - now) * 1000L /
                            Stopwatch.Frequency);
                        if (waitMilliseconds > 0)
                        {
                            frameStopEvent.WaitOne(waitMilliseconds);
                        }
                        while (frameClockRunning &&
                            Stopwatch.GetTimestamp() < nextFrame)
                        {
                            Thread.SpinWait(24);
                        }
                    }

                    if (!frameClockRunning)
                    {
                        break;
                    }

                    if (Interlocked.Exchange(ref frameMessagePending, 1) == 0)
                    {
                        if (!PostMessage(
                            frameWindowHandle,
                            FrameMessage,
                            IntPtr.Zero,
                            IntPtr.Zero))
                        {
                            Interlocked.Exchange(ref frameMessagePending, 0);
                        }
                    }

                    nextFrame += frameTicks;
                    long current = Stopwatch.GetTimestamp();
                    if (nextFrame < current)
                    {
                        nextFrame = current + frameTicks;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("FrameLoop: " + exception);
            }
            finally
            {
                timeEndPeriod(1);
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == FrameMessage)
            {
                try
                {
                    AdvanceFrame();
                }
                finally
                {
                    // Allow the frame thread to enqueue only after the
                    // completed paint. This leaves room for timers and input
                    // messages even when a frame takes longer than 1/165 s.
                    Interlocked.Exchange(ref frameMessagePending, 0);
                }
                return;
            }
            if (message.Msg == 0x0084) // WM_NCHITTEST
            {
                int packed = message.LParam.ToInt32();
                int screenX = (short)(packed & 0xFFFF);
                int screenY = (short)((packed >> 16) & 0xFFFF);
                Point client = PointToClient(new Point(screenX, screenY));
                Rectangle bounds = new Rectangle(
                    0,
                    0,
                    Math.Max(1, currentWidth),
                    Math.Max(1, currentHeight));
                if (IsMultiWindowExpanded)
                {
                    int gap = Scale(16);
                    Rectangle leftPanel;
                    Rectangle rightPanel;
                    if (SideDocked)
                    {
                        int panelHeight = Math.Max(
                            1,
                            (bounds.Height - gap) / 2);
                        leftPanel = new Rectangle(
                            0,
                            0,
                            bounds.Width,
                            panelHeight);
                        rightPanel = new Rectangle(
                            0,
                            panelHeight + gap,
                            bounds.Width,
                            Math.Max(
                                1,
                                bounds.Height - panelHeight - gap));
                    }
                    else
                    {
                        int panelWidth = Math.Max(
                            1,
                            (bounds.Width - gap) / 2);
                        leftPanel = new Rectangle(
                            0,
                            0,
                            panelWidth,
                            bounds.Height);
                        rightPanel = new Rectangle(
                            panelWidth + gap,
                            0,
                            Math.Max(1, bounds.Width - panelWidth - gap),
                            bounds.Height);
                    }
                    using (GraphicsPath leftPath = RoundedRectangle(
                        leftPanel,
                        Scale(24)))
                    using (GraphicsPath rightPath = RoundedRectangle(
                        rightPanel,
                        Scale(24)))
                    {
                        message.Result =
                            leftPath.IsVisible(client) ||
                            rightPath.IsVisible(client)
                                ? new IntPtr(1)
                                : new IntPtr(-1);
                        return;
                    }
                }
                using (GraphicsPath path = RoundedRectangle(
                    bounds,
                    CalculateCornerRadius()))
                {
                    message.Result = path.IsVisible(client)
                        ? new IntPtr(1)   // HTCLIENT
                        : new IntPtr(-1); // HTTRANSPARENT
                    return;
                }
            }
            base.WndProc(ref message);
        }

        private void AdvanceFrame()
        {
            if (current == null || phase == VisualPhase.Hidden)
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();
            double elapsed = (now - lastFrameTimestamp) /
                (double)Stopwatch.Frequency;
            lastFrameTimestamp = now;
            elapsed = Math.Max(0.001, Math.Min(0.020, elapsed));

            int steps = Math.Max(1, (int)Math.Ceiling(elapsed / 0.003));
            double step = elapsed / steps;
            for (int index = 0; index < steps; index++)
            {
                StepSpring(
                    ref animatedWidth,
                    ref widthVelocity,
                    springTargetWidth,
                    step,
                    260.0,
                    32.0);
                StepSpring(
                    ref animatedHeight,
                    ref heightVelocity,
                    springTargetHeight,
                    step,
                    260.0,
                    32.0);
                StepSpring(
                    ref detailBlend,
                    ref detailBlendVelocity,
                    detailBlendTarget,
                    step,
                    310.0,
                    31.0);
                StepSpring(
                    ref waveformMotion,
                    ref waveformMotionVelocity,
                    waveformMotionTarget,
                    step,
                    390.0,
                    34.0);
                StepSpring(
                    ref glassBlend,
                    ref glassBlendVelocity,
                    glassBlendTarget,
                    step,
                    220.0,
                    24.0);
                StepSpring(
                    ref qualityBlend,
                    ref qualityBlendVelocity,
                    qualityBlendTarget,
                    step,
                    240.0,
                    27.0);
                StepSpring(
                    ref frostedBlend,
                    ref frostedBlendVelocity,
                    frostedBlendTarget,
                    step,
                    520.0,
                    36.0);
                StepSpring(
                    ref dockSnapBlend,
                    ref dockSnapVelocity,
                    1.0,
                    step,
                    330.0,
                    32.0);
            }
            detailBlend = Clamp01(detailBlend);
            waveformMotion = Clamp01(waveformMotion);
            glassBlend = Clamp01(glassBlend);
            qualityBlend = Clamp01(qualityBlend);
            frostedBlend = Clamp01(frostedBlend);
            dockSnapBlend = Clamp01(dockSnapBlend);
            if (snappingToDock && dockSnapBlend >= 0.999)
            {
                dockSnapBlend = 1.0;
                dockSnapVelocity = 0.0;
                snappingToDock = false;
            }
            if (qualityBlend > 0.01 && glassBlend > 0.01)
            {
                qualityParticleSeconds += elapsed;
            }
            if (phase == VisualPhase.Collapsing &&
                qualityBlend > 0.01 &&
                collapseDissolveProgress < 1.0)
            {
                collapseDissolveProgress = Clamp01(
                    collapseDissolveProgress +
                    elapsed / 0.78);
            }
            if ((currentIsMedia &&
                 current != null &&
                 !musicPaused &&
                 !musicEnding) ||
                (currentIsTimer &&
                 HasMusicCompanion() &&
                 !musicPaused))
            {
                waveformSeconds += elapsed;
            }
            if (currentIsTimer)
            {
                timerAnimationSeconds += elapsed;
            }
            if (current != null &&
                !currentIsMedia &&
                phase == VisualPhase.Detail &&
                !draggingWindow &&
                notificationCollapseAt != DateTime.MaxValue &&
                DateTime.UtcNow >= notificationCollapseAt)
            {
                BeginCollapse();
            }

            int nextWidth = Math.Max(1, (int)Math.Round(animatedWidth));
            int nextHeight = Math.Max(1, (int)Math.Round(animatedHeight));
            bool geometryChanged =
                nextWidth != currentWidth ||
                nextHeight != currentHeight;
            currentWidth = nextWidth;
            currentHeight = nextHeight;
            if (geometryChanged || draggingWindow || snappingToDock)
            {
                ApplyGeometry();
                RefreshFrostedBackdrop();
            }
            else
            {
                RefreshFrostedBackdrop();
                RequestRender();
            }
            UpdateRequestedFrameRate();
        }

        private void UpdateRequestedFrameRate()
        {
            bool transitionActive =
                draggingWindow ||
                snappingToDock ||
                phase == VisualPhase.Collapsing ||
                Math.Abs(widthVelocity) > 0.05 ||
                Math.Abs(heightVelocity) > 0.05 ||
                Math.Abs(detailBlendVelocity) > 0.01 ||
                Math.Abs(waveformMotionVelocity) > 0.01 ||
                Math.Abs(glassBlendVelocity) > 0.01 ||
                Math.Abs(qualityBlendVelocity) > 0.01 ||
                Math.Abs(frostedBlendVelocity) > 0.01 ||
                Math.Abs(animatedWidth - springTargetWidth) > 0.35 ||
                Math.Abs(animatedHeight - springTargetHeight) > 0.35 ||
                Math.Abs(qualityBlend - qualityBlendTarget) > 0.01 ||
                Math.Abs(glassBlend - glassBlendTarget) > 0.01 ||
                Math.Abs(frostedBlend - frostedBlendTarget) > 0.01;
            bool timerActive =
                currentIsTimer &&
                timerSnapshot != null &&
                timerSnapshot.IsActive;
            bool waveformActive =
                currentIsMedia &&
                current != null &&
                phase == VisualPhase.Music &&
                !musicPaused &&
                !musicEnding;
            int frameRate = Math.Min(
                frameRateLimit,
                transitionActive
                    ? ActiveFrameRate
                    : (waveformActive || timerActive
                        ? WaveformFrameRate
                        : IdleFrameRate));
            Volatile.Write(
                ref requestedFrameRate,
                frameRate);
        }

        private void OnGlobalMouseDown(int screenX, int screenY)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<int, int>(OnGlobalMouseDown),
                    new object[] { screenX, screenY });
                return;
            }
            if (!musicControlsVisible ||
                !currentIsMedia ||
                phase != VisualPhase.Music)
            {
                if (!timerExpanded || !currentIsTimer)
                {
                    return;
                }
            }

            Point localPoint = PointToClient(new Point(screenX, screenY));
            if (!ClientRectangle.Contains(localPoint))
            {
                if (timerExpanded && currentIsTimer)
                {
                    CollapseTimerControls();
                }
                if (musicControlsVisible && currentIsMedia)
                {
                    CollapseMusicControls();
                }
            }
        }

        private void OnDesktopWindowChanged(
            IntPtr hook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint threadId,
            uint eventTime)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }
            if (objectId != 0 || childId != 0)
            {
                return;
            }
            frostedBackdropDirty = true;
        }

        private static void StepSpring(
            ref double value,
            ref double velocity,
            double target,
            double elapsed,
            double stiffness,
            double damping)
        {
            double acceleration =
                (target - value) * stiffness -
                velocity * damping;
            velocity += acceleration * elapsed;
            value += velocity * elapsed;
            if (Math.Abs(target - value) < 0.08 &&
                Math.Abs(velocity) < 0.8)
            {
                value = target;
                velocity = 0.0;
            }
        }

        private void ApplyGeometry()
        {
            Point anchorPoint = draggingWindow || snappingToDock
                ? dragLastScreen
                : new Point(
                    Bounds.Left + currentWidth / 2,
                    Bounds.Top + currentHeight / 2);
            Screen screen = draggingWindow || snappingToDock
                ? Screen.FromPoint(anchorPoint)
                : ResolveDockScreen(anchorPoint);
            Rectangle bounds = screen.WorkingArea;
            int margin = Scale(7);
            int targetLeft;
            int targetTop;
            if (dockEdge == DockEdge.Bottom)
            {
                targetLeft = PositionedHorizontal(
                    bounds,
                    currentWidth,
                    margin,
                    dockPosition);
                targetTop = bounds.Bottom - currentHeight - margin;
            }
            else if (dockEdge == DockEdge.Left)
            {
                targetLeft = bounds.Left + margin;
                targetTop = PositionedVertical(
                    bounds,
                    currentHeight,
                    margin,
                    dockPosition);
            }
            else if (dockEdge == DockEdge.Right)
            {
                targetLeft = bounds.Right - currentWidth - margin;
                targetTop = PositionedVertical(
                    bounds,
                    currentHeight,
                    margin,
                    dockPosition);
            }
            else
            {
                targetLeft = PositionedHorizontal(
                    bounds,
                    currentWidth,
                    margin,
                    dockPosition);
                targetTop = bounds.Top + margin;
            }

            int left = targetLeft;
            int top = targetTop;
            if (draggingWindow)
            {
                left = dragLastScreen.X -
                    dragGrabOffset.X -
                    currentWidth / 2;
                top = dragLastScreen.Y -
                    dragGrabOffset.Y -
                    currentHeight / 2;
            }
            else if (snappingToDock)
            {
                double blend = SmoothStep(dockSnapBlend);
                double targetCenterX = targetLeft + currentWidth / 2.0;
                double targetCenterY = targetTop + currentHeight / 2.0;
                double startCenterX =
                    dragLastScreen.X -
                    dragGrabOffset.X;
                double startCenterY =
                    dragLastScreen.Y -
                    dragGrabOffset.Y;
                double centerX =
                    startCenterX +
                    (targetCenterX - startCenterX) * blend;
                double centerY =
                    startCenterY +
                    (targetCenterY - startCenterY) * blend;
                left = (int)Math.Round(centerX - currentWidth / 2.0);
                top = (int)Math.Round(centerY - currentHeight / 2.0);
            }
            if (Bounds.Left != left ||
                Bounds.Top != top ||
                Bounds.Width != currentWidth ||
                Bounds.Height != currentHeight)
            {
                SetBounds(left, top, currentWidth, currentHeight);
            }
            RequestRender();
        }

        private Screen ResolveDockScreen(Point fallbackPoint)
        {
            if (!string.IsNullOrWhiteSpace(dockScreenDevice))
            {
                Screen[] screens = Screen.AllScreens;
                for (int index = 0; index < screens.Length; index++)
                {
                    if (string.Equals(
                        screens[index].DeviceName,
                        dockScreenDevice,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return screens[index];
                    }
                }
            }
            return Screen.FromPoint(fallbackPoint);
        }

        private int PositionedHorizontal(
            Rectangle bounds,
            int width,
            int margin,
            int position)
        {
            int available = Math.Max(0, bounds.Width - width);
            if (position <= 0)
            {
                return bounds.Left + margin;
            }
            if (position >= 2)
            {
                return bounds.Right - width - margin;
            }
            return bounds.Left + available / 2;
        }

        private int PositionedVertical(
            Rectangle bounds,
            int height,
            int margin,
            int position)
        {
            int available = Math.Max(0, bounds.Height - height);
            if (position <= 0)
            {
                return bounds.Top + margin;
            }
            if (position >= 2)
            {
                return bounds.Bottom - height - margin;
            }
            return bounds.Top + available / 2;
        }

        private int CalculateCornerRadius()
        {
            if (currentIsTimer && phase != VisualPhase.Seed)
            {
                int compactHeight = Math.Max(
                    1,
                    Scale(TimerHeight));
                int expandedHeight = Math.Max(
                    compactHeight + 1,
                    Scale(TimerExpandedHeight));
                double expansion = Clamp01(
                    (currentHeight - compactHeight) /
                    (double)(expandedHeight - compactHeight));
                double compactRadius = compactHeight / 2.0;
                double expandedRadius = Scale(27);
                return Math.Max(
                    1,
                    (int)Math.Round(
                        compactRadius +
                        (expandedRadius - compactRadius) *
                        SmoothStep(expansion)));
            }
            if (SideDocked && phase != VisualPhase.Seed)
            {
                return Math.Max(1, Scale(24));
            }
            int radius = Math.Max(1, currentHeight / 2);
            if (currentIsMedia && phase == VisualPhase.Music)
            {
                double expansion = Clamp01(
                    (currentHeight -
                        Scale(SideDocked
                            ? SideMusicHeight
                            : MusicHeight)) /
                    (double)Math.Max(
                        1,
                        Scale(SideDocked
                            ? SideMusicExpandedHeight
                            : MusicExpandedHeight) -
                        Scale(SideDocked
                            ? SideMusicHeight
                            : MusicHeight)));
                radius = (int)Math.Round(
                    Scale(26) -
                    Scale(12) * SmoothStep(expansion));
            }
            return Math.Max(1, radius);
        }

        private void RequestRender()
        {
            if (IsDisposed || !IsHandleCreated || !Visible)
            {
                return;
            }
            RenderLayeredFrame();
        }

        private void RenderLayeredFrame()
        {
            if (renderingLayered ||
                IsDisposed ||
                !IsHandleCreated ||
                !Visible)
            {
                return;
            }

            int width = Math.Max(1, currentWidth);
            int height = Math.Max(1, currentHeight);
            long frameStart = Stopwatch.GetTimestamp();
            renderingLayered = true;
            try
            {
                if (layeredMemoryDc == IntPtr.Zero ||
                    layeredSurfaceWidth != width ||
                    layeredSurfaceHeight != height)
                {
                    ReleaseLayeredSurface();
                    CreateLayeredSurface(width, height);
                }
                if (layeredGraphics == null)
                {
                    return;
                }

                layeredGraphics.Clear(Color.Transparent);
                PaintSurface(layeredGraphics);

                NativePoint destination = new NativePoint();
                destination.X = Bounds.Left;
                destination.Y = Bounds.Top;
                NativeSize size = new NativeSize();
                size.Width = width;
                size.Height = height;
                NativePoint source = new NativePoint();
                BlendFunction blend = new BlendFunction();
                blend.BlendOp = 0;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;
                UpdateLayeredWindow(
                    Handle,
                    IntPtr.Zero,
                    ref destination,
                    ref size,
                    layeredMemoryDc,
                    ref source,
                    0,
                    ref blend,
                    2);
                performanceMonitor.RecordFrame(
                    frameStart,
                    Stopwatch.GetTimestamp(),
                    Volatile.Read(ref requestedFrameRate));
            }
            catch (Exception exception)
            {
                Logger.Write("RenderLayeredFrame: " + exception);
            }
            finally
            {
                renderingLayered = false;
            }
        }

        private void CreateLayeredSurface(int width, int height)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return;
            }
            try
            {
                layeredMemoryDc = CreateCompatibleDC(screenDc);
                if (layeredMemoryDc == IntPtr.Zero)
                {
                    return;
                }

                BitmapInfo info = new BitmapInfo();
                info.Header.Size = (uint)Marshal.SizeOf(
                    typeof(BitmapInfoHeader));
                info.Header.Width = width;
                info.Header.Height = -height;
                info.Header.Planes = 1;
                info.Header.BitCount = 32;
                info.Header.Compression = 0;
                IntPtr pixels;
                layeredDib = CreateDIBSection(
                    screenDc,
                    ref info,
                    0,
                    out pixels,
                    IntPtr.Zero,
                    0);
                if (layeredDib == IntPtr.Zero)
                {
                    DeleteDC(layeredMemoryDc);
                    layeredMemoryDc = IntPtr.Zero;
                    return;
                }

                layeredOriginalBitmap = SelectObject(
                    layeredMemoryDc,
                    layeredDib);
                layeredGraphics = Graphics.FromHdc(
                    layeredMemoryDc);
                layeredGraphics.SmoothingMode =
                    SmoothingMode.AntiAlias;
                layeredGraphics.InterpolationMode =
                    InterpolationMode.HighQualityBilinear;
                layeredGraphics.PixelOffsetMode =
                    PixelOffsetMode.HighSpeed;
                layeredGraphics.CompositingQuality =
                    CompositingQuality.HighSpeed;
                layeredGraphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint
                        .AntiAliasGridFit;
                layeredSurfaceWidth = width;
                layeredSurfaceHeight = height;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void ReleaseLayeredSurface()
        {
            if (layeredGraphics != null)
            {
                layeredGraphics.Dispose();
                layeredGraphics = null;
            }
            if (layeredMemoryDc != IntPtr.Zero)
            {
                if (layeredOriginalBitmap != IntPtr.Zero)
                {
                    SelectObject(
                        layeredMemoryDc,
                        layeredOriginalBitmap);
                    layeredOriginalBitmap = IntPtr.Zero;
                }
                if (layeredDib != IntPtr.Zero)
                {
                    DeleteObject(layeredDib);
                    layeredDib = IntPtr.Zero;
                }
                DeleteDC(layeredMemoryDc);
                layeredMemoryDc = IntPtr.Zero;
            }
            else if (layeredDib != IntPtr.Zero)
            {
                DeleteObject(layeredDib);
                layeredDib = IntPtr.Zero;
            }
            layeredSurfaceWidth = 0;
            layeredSurfaceHeight = 0;
        }

        private int Scale(int logicalPixels)
        {
            return (int)Math.Round(
                logicalPixels *
                baseFontDpi /
                96f *
                userScale);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (current == null)
            {
                animatedWidth = Scale(SeedSize);
                animatedHeight = Scale(SeedSize);
                currentWidth = (int)Math.Round(animatedWidth);
                currentHeight = (int)Math.Round(animatedHeight);
            }
            ApplyGeometry();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            baseFontDpi = e.DeviceDpiNew;
            CreateFonts(baseFontDpi * (float)userScale);
            BeginInvoke(new Action(delegate
            {
                if (current != null && phase != VisualPhase.Hidden)
                {
                    SetUiScale(userScale);
                }
                ApplyGeometry();
            }));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            RenderLayeredFrame();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // The layered surface is rendered with per-pixel transparency.
        }

        private void PaintSurface(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            if (currentIsTimer && draggingWindow)
            {
                graphics.Clear(Color.Transparent);
                DrawSeed(graphics);
                return;
            }

            if (IsMultiWindowExpanded)
            {
                PaintMultiWindow(graphics);
                return;
            }

            Rectangle outer = new Rectangle(
                0,
                0,
                Math.Max(1, ClientSize.Width - 1),
                Math.Max(1, ClientSize.Height - 1));
            bool timerCompanion =
                currentIsTimer &&
                !timerExpanded &&
                HasMusicCompanion();
            Rectangle shell = outer;
            if (timerCompanion)
            {
                int inset = Scale(24);
                shell = new Rectangle(
                    inset,
                    0,
                    Math.Max(1, outer.Width - inset * 2),
                    outer.Height);
            }
            int radius = CalculateCornerRadius();
            using (GraphicsPath path = RoundedRectangle(shell, radius))
            {
                double frostPresence = Clamp01(
                    frostedBlend * frostedIntensity);
                bool shellCached =
                    !timerCompanion &&
                    frostPresence > 0.02 &&
                    (CanUseFrostedShellCache() ||
                     frostedShellCache != null);
                if (shellCached &&
                    TryDrawFrostedShellCache(
                        graphics,
                        path,
                        frostPresence))
                {
                    // Static frosted glass is reused every frame.
                }
                else
                {
                    PaintShellBase(
                        graphics,
                        path,
                        shell,
                        radius,
                        frostPresence);
                }
                DrawShellBorder(
                    graphics,
                    path,
                    frostPresence);
                if (frostPresence < 0.02)
                {
                    DrawGlassSheen(graphics, path, outer);
                }
                DrawQualityGlass(graphics, path, shell, radius);
            }

            if (current == null || phase == VisualPhase.Hidden)
            {
                return;
            }
            if (phase == VisualPhase.Seed)
            {
                DrawSeed(graphics);
            }
            else if (phase == VisualPhase.Compact)
            {
                DrawExpanded(graphics);
            }
            else if (phase == VisualPhase.Music)
            {
                if (SideDocked)
                {
                    DrawSideMusic(graphics);
                }
                else
                {
                    DrawMusic(graphics);
                }
            }
            else if (phase == VisualPhase.Timer)
            {
                DrawTimer(graphics);
            }
            else if (phase == VisualPhase.Collapsing)
            {
                DrawCollapsing(graphics);
            }
            else
            {
                if (SideDocked)
                {
                    DrawSideExpanded(graphics);
                }
                else
                {
                    DrawExpanded(graphics);
                }
            }
        }

        private bool IsMultiWindowExpanded
        {
            get
            {
                return currentIsTimer &&
                    timerExpanded &&
                    HasMusicCompanion();
            }
        }

        private void PaintMultiWindow(Graphics graphics)
        {
            if (timerSnapshot == null)
            {
                return;
            }
            Rectangle outer = new Rectangle(
                0,
                0,
                Math.Max(1, ClientSize.Width - 1),
                Math.Max(1, ClientSize.Height - 1));
            int gap = Scale(16);
            Rectangle timerPanel;
            Rectangle musicPanel;
            if (SideDocked)
            {
                int panelHeight = Math.Max(
                    1,
                    (outer.Height - gap) / 2);
                timerPanel = new Rectangle(
                    0,
                    0,
                    outer.Width,
                    panelHeight);
                musicPanel = new Rectangle(
                    0,
                    panelHeight + gap,
                    outer.Width,
                    Math.Max(1, outer.Height - panelHeight - gap));
            }
            else
            {
                int panelWidth = Math.Max(
                    1,
                    (outer.Width - gap) / 2);
                timerPanel = new Rectangle(
                    0,
                    0,
                    panelWidth,
                    outer.Height);
                musicPanel = new Rectangle(
                    panelWidth + gap,
                    0,
                    Math.Max(1, outer.Width - panelWidth - gap),
                    outer.Height);
            }
            int radius = Scale(24);
            double frostPresence = Clamp01(
                frostedBlend * frostedIntensity);
            using (GraphicsPath timerPath = RoundedRectangle(
                timerPanel,
                radius))
            using (GraphicsPath musicPath = RoundedRectangle(
                musicPanel,
                radius))
            {
                PaintShellBase(
                    graphics,
                    timerPath,
                    timerPanel,
                    radius,
                    frostPresence);
                PaintShellBase(
                    graphics,
                    musicPath,
                    musicPanel,
                    radius,
                    frostPresence);
                DrawShellBorder(
                    graphics,
                    timerPath,
                    frostPresence);
                DrawShellBorder(
                    graphics,
                    musicPath,
                    frostPresence);
                if (frostPresence < 0.02)
                {
                    DrawGlassSheen(graphics, timerPath, timerPanel);
                    DrawGlassSheen(graphics, musicPath, musicPanel);
                    DrawQualityGlass(
                        graphics,
                        timerPath,
                        timerPanel,
                        radius);
                    DrawQualityGlass(
                        graphics,
                        musicPath,
                        musicPanel,
                        radius);
                }
            }
            DrawMultiTimerPanel(graphics, timerPanel);
            DrawMultiMusicPanel(graphics, musicPanel);
        }

        private bool CanUseFrostedShellCache()
        {
            return
                Math.Abs(widthVelocity) < 0.05 &&
                Math.Abs(heightVelocity) < 0.05 &&
                Math.Abs(animatedWidth - springTargetWidth) < 0.25 &&
                Math.Abs(animatedHeight - springTargetHeight) < 0.25 &&
                Math.Abs(frostedBlend - frostedBlendTarget) < 0.01;
        }

        private bool TryDrawFrostedShellCache(
            Graphics graphics,
            GraphicsPath clipPath,
            double frostPresence)
        {
            int width = Math.Max(1, ClientSize.Width);
            int height = Math.Max(1, ClientSize.Height);
            int radius = CalculateCornerRadius();
            bool versionMatches =
                frostedShellCache != null &&
                frostedShellVersion == frostedBackdropVersion &&
                Math.Abs(frostedShellPresence - frostPresence) < 0.002;
            bool sizeMatches =
                frostedShellWidth == width &&
                frostedShellHeight == height &&
                frostedShellRadius == radius;
            bool settled = CanUseFrostedShellCache();
            bool mayRebuild =
                !versionMatches ||
                (sizeMatches == false && settled);
            if (mayRebuild)
            {
                using (GraphicsPath fullSurface = new GraphicsPath())
                {
                    fullSurface.AddRectangle(
                        new Rectangle(0, 0, width, height));
                    Bitmap next = new Bitmap(
                        width,
                        height,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (Graphics nextGraphics = Graphics.FromImage(next))
                    {
                        nextGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                        nextGraphics.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        nextGraphics.PixelOffsetMode =
                            PixelOffsetMode.HighQuality;
                        nextGraphics.CompositingQuality =
                            CompositingQuality.HighQuality;
                        nextGraphics.Clear(Color.Transparent);
                        PaintShellBase(
                            nextGraphics,
                            fullSurface,
                            new Rectangle(
                                0,
                                0,
                                Math.Max(1, width - 1),
                                Math.Max(1, height - 1)),
                            radius,
                            frostPresence);
                    }
                    if (frostedShellCache != null)
                    {
                        frostedShellCache.Dispose();
                    }
                    frostedShellCache = next;
                    frostedShellWidth = width;
                    frostedShellHeight = height;
                    frostedShellRadius = radius;
                    frostedShellVersion = frostedBackdropVersion;
                    frostedShellPresence = frostPresence;
                }
            }
            if (frostedShellCache == null)
            {
                return false;
            }
            GraphicsState state = graphics.Save();
            graphics.SetClip(clipPath);
            if (frostedShellWidth == width &&
                frostedShellHeight == height &&
                frostedShellRadius == radius)
            {
                graphics.DrawImageUnscaled(frostedShellCache, 0, 0);
            }
            else
            {
                graphics.InterpolationMode =
                    InterpolationMode.Bilinear;
                graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                graphics.DrawImage(
                    frostedShellCache,
                    new Rectangle(0, 0, width, height));
            }
            graphics.Restore(state);
            return true;
        }

        private void PaintShellBase(
            Graphics graphics,
            GraphicsPath path,
            Rectangle outer,
            int radius,
            double frostPresence)
        {
            Color solidTop = Color.FromArgb(255, 26, 26, 29);
            Color solidBottom = Color.FromArgb(255, 9, 9, 11);
            double glassVisibility =
                1.0 - 0.46 * frostPresence;
            Color glassTop = Color.FromArgb(
                (int)Math.Round(90 * glassVisibility),
                45,
                52,
                64);
            Color glassBottom = Color.FromArgb(
                (int)Math.Round(66 * glassVisibility),
                14,
                18,
                25);
            DrawFrostedBackdrop(graphics, path, outer);
            using (LinearGradientBrush background =
                new LinearGradientBrush(
                    new Rectangle(
                        outer.Left,
                        outer.Top,
                        Math.Max(1, outer.Width),
                        Math.Max(2, outer.Height)),
                    BlendColor(solidTop, glassTop, glassBlend),
                    BlendColor(solidBottom, glassBottom, glassBlend),
                    LinearGradientMode.Vertical))
            {
                graphics.FillPath(background, path);
            }

            DrawFrostedGlass(graphics, path, outer);
        }

        private void DrawShellBorder(
            Graphics graphics,
            GraphicsPath path,
            double frostPresence)
        {
            using (Pen border = new Pen(
                Color.FromArgb(
                    (int)Math.Round(
                        (94 + 50 * glassBlend) *
                        (1.0 - 0.58 * frostPresence)),
                    185,
                    190,
                    184),
                1f))
            {
                graphics.DrawPath(border, path);
            }
        }

        private void DrawGlassSheen(
            Graphics graphics,
            GraphicsPath path,
            Rectangle bounds)
        {
            if (glassBlend <= 0.01)
            {
                return;
            }
            GraphicsState state = graphics.Save();
            graphics.SetClip(path);
            Rectangle sheenBounds = new Rectangle(
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width),
                Math.Max(2, bounds.Height / 2));
            using (LinearGradientBrush sheen = new LinearGradientBrush(
                sheenBounds,
                Color.FromArgb(
                    (int)Math.Round(36 * glassBlend),
                    255,
                    255,
                    255),
                Color.FromArgb(0, 255, 255, 255),
                LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(
                    sheen,
                    sheenBounds);
            }
            graphics.Restore(state);
        }

        private void DrawFrostedGlass(
            Graphics graphics,
            GraphicsPath path,
            Rectangle bounds)
        {
            double strength = Clamp01(
                frostedBlend *
                glassBlend *
                frostedIntensity);
            if (strength <= 0.01 || bounds.Width < Scale(24))
            {
                return;
            }

            GraphicsState state = graphics.Save();
            graphics.SetClip(path);
            Rectangle fillBounds = new Rectangle(
                bounds.Left - 1,
                bounds.Top - 1,
                Math.Max(1, bounds.Width + 2),
                Math.Max(1, bounds.Height + 2));

            int topAlpha = (int)Math.Round(54 * strength);
            int bottomAlpha = (int)Math.Round(34 * strength);
            using (LinearGradientBrush diffuse = new LinearGradientBrush(
                fillBounds,
                Color.FromArgb(topAlpha, 8, 10, 15),
                Color.FromArgb(bottomAlpha, 25, 31, 43),
                102f))
            {
                graphics.FillRectangle(diffuse, fillBounds);
            }

            Rectangle lightBand = new Rectangle(
                fillBounds.Left,
                fillBounds.Top,
                fillBounds.Width,
                Math.Max(2, fillBounds.Height / 2));
            using (LinearGradientBrush softLight = new LinearGradientBrush(
                lightBand,
                Color.FromArgb(
                    (int)Math.Round(7 * strength),
                    255,
                    255,
                    255),
                Color.Transparent,
                LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(softLight, lightBand);
            }

            if (frostedNoiseBrush != null)
            {
                graphics.FillRectangle(frostedNoiseBrush, fillBounds);
            }
            graphics.Restore(state);
        }

        private void DrawFrostedBackdrop(
            Graphics graphics,
            GraphicsPath path,
            Rectangle bounds)
        {
            if (frostedBlend <= 0.01 ||
                frostedIntensity <= 0.01)
            {
                return;
            }

            lock (frostedBackdropSync)
            {
                if (frostedBackdrop == null)
                {
                    return;
                }
                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                if (frostedBackdrop.Width >= bounds.Width &&
                    frostedBackdrop.Height >= bounds.Height)
                {
                    graphics.DrawImageUnscaled(frostedBackdrop, 0, 0);
                }
                else
                {
                    graphics.InterpolationMode =
                        InterpolationMode.HighQualityBilinear;
                    graphics.DrawImage(
                        frostedBackdrop,
                        new Rectangle(
                            bounds.Left,
                            bounds.Top,
                            bounds.Width,
                            bounds.Height));
                }
                graphics.Restore(state);
            }
        }

        private void RebuildFrostedNoiseTile()
        {
            if (frostedNoiseBrush != null)
            {
                frostedNoiseBrush.Dispose();
            }
            if (frostedNoiseTile != null)
            {
                frostedNoiseTile.Dispose();
            }

            const int size = 96;
            frostedNoiseTile = new Bitmap(
                size,
                size,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(frostedNoiseTile))
            {
                graphics.Clear(Color.Transparent);
            }

            Random random = new Random(0x1A113);
            int baseAlpha = Math.Max(
                1,
                (int)Math.Round(
                    1.0 + 2.0 * Clamp01(frostedIntensity)));
            int pointCount = 540;
            for (int index = 0; index < pointCount; index++)
            {
                bool light = random.NextDouble() >= 0.42;
                int alpha = Math.Max(
                    1,
                    baseAlpha - random.Next(0, 4));
                frostedNoiseTile.SetPixel(
                    random.Next(size),
                    random.Next(size),
                    light
                        ? Color.FromArgb(alpha, 255, 255, 255)
                        : Color.FromArgb(alpha, 32, 42, 58));
            }
            frostedNoiseBrush = new TextureBrush(
                frostedNoiseTile,
                WrapMode.TileFlipXY);
        }

        private void DrawQualityGlass(
            Graphics graphics,
            GraphicsPath path,
            Rectangle bounds,
            int radius)
        {
            double strength = Clamp01(qualityBlend * glassBlend);
            if (strength <= 0.01 || bounds.Width < Scale(30))
            {
                return;
            }

            GraphicsState state = graphics.Save();
            graphics.SetClip(path);

            int inset = Math.Max(1, Scale(2));
            Rectangle inner = new Rectangle(
                bounds.Left + inset,
                bounds.Top + inset,
                Math.Max(1, bounds.Width - inset * 2),
                Math.Max(1, bounds.Height - inset * 2));
            int innerRadius = Math.Max(1, radius - inset);
            bool flatFrost =
                frostedBlend * frostedIntensity > 0.02;
            if (!flatFrost)
            {
                using (GraphicsPath innerPath = RoundedRectangle(
                    inner,
                    innerRadius))
                using (Pen refraction = new Pen(
                    Color.FromArgb(
                        (int)Math.Round(92 * strength),
                        242,
                        250,
                        255),
                    Math.Max(1f, Scale(1))))
                {
                    graphics.DrawPath(refraction, innerPath);
                }

                Rectangle lowerInner = new Rectangle(
                    inner.Left,
                    inner.Top + Math.Max(1, inner.Height / 2),
                    inner.Width,
                    Math.Max(1, inner.Height - inner.Height / 2));
                using (LinearGradientBrush depth = new LinearGradientBrush(
                    lowerInner,
                    Color.Transparent,
                    Color.FromArgb(
                        (int)Math.Round(58 * strength),
                        0,
                        3,
                        8),
                    LinearGradientMode.Vertical))
                {
                    graphics.FillRectangle(depth, lowerInner);
                }

                Rectangle upperLight = new Rectangle(
                    inner.Left,
                    inner.Top,
                    inner.Width,
                    Math.Max(3, inner.Height / 3));
                using (LinearGradientBrush volume = new LinearGradientBrush(
                    upperLight,
                    Color.FromArgb(
                        (int)Math.Round(48 * strength),
                        255,
                        255,
                        255),
                    Color.Transparent,
                    LinearGradientMode.Vertical))
                {
                    graphics.FillRectangle(volume, upperLight);
                }
            }

            double flow = (qualityParticleSeconds * 0.19) % 1.0;
            int glowWidth = Math.Max(Scale(54), bounds.Width / 5);
            int glowLeft = bounds.Left - glowWidth +
                (int)Math.Round((bounds.Width + glowWidth) * flow);
            Rectangle streak = new Rectangle(
                glowLeft,
                inner.Bottom - Math.Max(2, Scale(3)),
                glowWidth,
                Math.Max(2, Scale(3)));
            using (LinearGradientBrush streakBrush = new LinearGradientBrush(
                streak,
                Color.Transparent,
                Color.Transparent,
                LinearGradientMode.Horizontal))
            {
                ColorBlend colorBlend = new ColorBlend(3);
                colorBlend.Colors = new Color[]
                {
                    Color.FromArgb(0, 217, 238, 255),
                    Color.FromArgb(
                        (int)Math.Round(172 * strength),
                        225,
                        244,
                        255),
                    Color.FromArgb(0, 217, 238, 255)
                };
                colorBlend.Positions = new float[] { 0f, 0.5f, 1f };
                streakBrush.InterpolationColors = colorBlend;
                graphics.FillRectangle(streakBrush, streak);
            }

            for (int index = 0; index < 9; index++)
            {
                double phase = (index * 0.61803398875) % 1.0;
                double speed = 0.018 + (index % 3) * 0.007;
                double xNorm = (phase + qualityParticleSeconds * speed) % 1.0;
                double baseY = 0.14 + ((index * 0.237) % 0.70);
                double drift = Math.Sin(
                    qualityParticleSeconds * (0.7 + index * 0.03) +
                    index * 1.31);
                double pulse = 0.45 +
                    0.55 * (0.5 + 0.5 * Math.Sin(
                        qualityParticleSeconds * 1.8 +
                        index * 0.91));
                int x = inner.Left +
                    (int)Math.Round(inner.Width * xNorm);
                int y = inner.Top +
                    (int)Math.Round(
                        inner.Height * (baseY + drift * 0.045));
                int particleRadius = Math.Max(
                    1,
                    Scale(index % 4 == 0 ? 2 : 1));
                int alpha = (int)Math.Round(
                    strength * pulse * (index % 3 == 0 ? 108 : 72));

                if (index % 3 == 0)
                {
                    Rectangle glowBounds = new Rectangle(
                        x - particleRadius * 3,
                        y - particleRadius * 3,
                        particleRadius * 6,
                        particleRadius * 6);
                    using (GraphicsPath glowPath = new GraphicsPath())
                    {
                        glowPath.AddEllipse(glowBounds);
                        using (PathGradientBrush glow =
                            new PathGradientBrush(glowPath))
                        {
                            glow.CenterColor = Color.FromArgb(
                                Math.Max(0, alpha / 3),
                                220,
                                242,
                                255);
                            glow.SurroundColors = new Color[]
                            {
                                Color.FromArgb(0, 220, 242, 255)
                            };
                            graphics.FillPath(glow, glowPath);
                        }
                    }
                }

                using (SolidBrush particle = new SolidBrush(
                    Color.FromArgb(
                        Math.Max(0, alpha),
                        246,
                        252,
                        255)))
                {
                    graphics.FillEllipse(
                        particle,
                        x - particleRadius,
                        y - particleRadius,
                        particleRadius * 2,
                        particleRadius * 2);
                }
            }

            graphics.Restore(state);
        }

        private void DrawCollapseParticleErase(Graphics graphics)
        {
            double strength = Clamp01(qualityBlend * glassBlend);
            if (strength <= 0.01 ||
                phase != VisualPhase.Collapsing ||
                ClientSize.Width < Scale(18))
            {
                return;
            }

            double progress = Clamp01(collapseDissolveProgress);
            progress = SmoothStep(progress);
            if (progress <= 0.01)
            {
                return;
            }

            Rectangle outer = new Rectangle(
                0,
                0,
                Math.Max(1, ClientSize.Width - 1),
                Math.Max(1, ClientSize.Height - 1));
            using (GraphicsPath path = RoundedRectangle(
                outer,
                CalculateCornerRadius()))
            {
                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                double eased = SmoothStep(progress);
                double front = outer.Width * eased * 0.51;
                int step = Math.Max(2, Scale(3));

                List<PointF> leftPoints = new List<PointF>();
                List<PointF> rightPoints = new List<PointF>();
                leftPoints.Add(new PointF(outer.Left, outer.Top));
                rightPoints.Add(new PointF(outer.Right, outer.Top));
                int edgeIndex = 0;
                for (int y = outer.Top; y <= outer.Bottom; y += step)
                {
                    double noise =
                        Hash01(edgeIndex, 3.71) * 2.0 - 1.0;
                    double wave = Math.Sin(
                        edgeIndex * 1.73 +
                        qualityParticleSeconds * 8.0) * 0.35;
                    double ragged = front +
                        Scale(5) * (noise + wave);
                    float leftX = (float)Math.Max(
                        outer.Left,
                        Math.Min(outer.Right, outer.Left + ragged));
                    float rightX = (float)Math.Min(
                        outer.Right,
                        Math.Max(outer.Left, outer.Right - ragged));
                    leftPoints.Add(new PointF(leftX, y));
                    rightPoints.Add(new PointF(rightX, y));
                    edgeIndex++;
                }
                leftPoints.Add(new PointF(outer.Left, outer.Bottom));
                rightPoints.Add(new PointF(outer.Right, outer.Bottom));

                using (SolidBrush erase =
                    new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                {
                    graphics.CompositingMode =
                        CompositingMode.SourceCopy;
                    graphics.FillPolygon(erase, leftPoints.ToArray());
                    graphics.FillPolygon(erase, rightPoints.ToArray());
                    graphics.CompositingMode =
                        CompositingMode.SourceOver;
                }

                int particleCount = Math.Max(
                    120,
                    Math.Min(
                        360,
                        (int)(outer.Width * (double)outer.Height / 220.0)));
                double frontNorm = Clamp01(front / outer.Width);
                using (SolidBrush grain = new SolidBrush(Color.White))
                {
                    for (int index = 0; index < particleCount; index++)
                    {
                        bool fromLeft = index % 2 == 0;
                        double bandWidth =
                            0.025 + 0.085 * (1.0 - eased);
                        double offset =
                            (Hash01(index, 31.7) - 0.5) * bandWidth +
                            (Hash01(index, 71.3) - 0.5) * 0.018;
                        double xNorm = fromLeft
                            ? frontNorm + offset
                            : 1.0 - frontNorm + offset;
                        xNorm = Clamp01(xNorm);
                        double yNorm = Hash01(index, 47.9);
                        int x = outer.Left +
                            (int)Math.Round(outer.Width * xNorm);
                        int y = outer.Top +
                            (int)Math.Round(outer.Height * yNorm);
                        bool isInsideRemaining = fromLeft
                            ? xNorm > frontNorm
                            : xNorm < 1.0 - frontNorm;
                        double localPulse =
                            0.35 + 0.65 * Hash01(index, 97.1);
                        int alpha = (int)Math.Round(
                            strength *
                            localPulse *
                            (isInsideRemaining ? 188 : 118));
                        if (alpha <= 2)
                        {
                            continue;
                        }

                        bool warm = index % 11 == 0;
                        grain.Color = Color.FromArgb(
                            alpha,
                            warm ? 255 : 224,
                            warm ? 223 : 244,
                            warm ? 178 : 255);
                        int size = Math.Max(
                            1,
                            Scale(index % 7 == 0 ? 2 : 1));
                        graphics.FillRectangle(grain, x, y, size, size);
                        if (index % 5 == 0)
                        {
                            int trail = Math.Max(1, Scale(3));
                            int drift = fromLeft ? trail : -trail;
                            grain.Color = Color.FromArgb(
                                Math.Max(0, alpha / 3),
                                warm ? 255 : 212,
                                warm ? 220 : 240,
                                warm ? 170 : 255);
                            graphics.FillRectangle(
                                grain,
                                x + drift,
                                y,
                                Math.Max(1, Scale(1)),
                                Math.Max(1, Scale(1)));
                        }
                    }
                }
                graphics.Restore(state);
            }
        }

        private static double Hash01(int index, double salt)
        {
            double value =
                Math.Sin(index * 12.9898 + salt) * 43758.5453;
            return value - Math.Floor(value);
        }

        private static Color BlendColor(Color solid, Color glass, double amount)
        {
            double value = Clamp01(amount);
            return Color.FromArgb(
                (int)Math.Round(solid.A + (glass.A - solid.A) * value),
                (int)Math.Round(solid.R + (glass.R - solid.R) * value),
                (int)Math.Round(solid.G + (glass.G - solid.G) * value),
                (int)Math.Round(solid.B + (glass.B - solid.B) * value));
        }

        private void DrawSweep(Graphics graphics, GraphicsPath path, Rectangle bounds)
        {
            if ((phase != VisualPhase.Detail && phase != VisualPhase.Music) ||
                (phase == VisualPhase.Detail && detailBlend < 0.05))
            {
                return;
            }

            double wave =
                (DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 2200.0) /
                2200.0;
            int sweepWidth = Scale(105);
            int sweepX = -sweepWidth +
                (int)Math.Round((bounds.Width + sweepWidth * 2) * wave);
            GraphicsState state = graphics.Save();
            graphics.SetClip(path);
            using (LinearGradientBrush sweep =
                new LinearGradientBrush(
                    new Rectangle(sweepX, bounds.Top, sweepWidth, bounds.Height),
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    LinearGradientMode.Horizontal))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[]
                {
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb(21, 255, 246, 220),
                    Color.FromArgb(0, 255, 255, 255)
                };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                sweep.InterpolationColors = blend;
                graphics.FillRectangle(sweep, sweepX, bounds.Top, sweepWidth, bounds.Height);
            }
            graphics.Restore(state);
        }

        private void DrawSeed(Graphics graphics)
        {
            int diameter = Math.Min(
                Scale(27),
                Math.Max(6, Math.Min(ClientSize.Width, ClientSize.Height) - Scale(7)));
            int left = (ClientSize.Width - diameter) / 2;
            int top = (ClientSize.Height - diameter) / 2;
            RectangleF circle = new RectangleF(left, top, diameter, diameter);

            using (SolidBrush white = new SolidBrush(Color.FromArgb(245, 244, 239)))
            {
                graphics.FillEllipse(white, circle);
            }
            using (Pen ring = new Pen(Color.FromArgb(95, 255, 255, 255), 1f))
            {
                graphics.DrawEllipse(
                    ring,
                    circle.X - 3,
                    circle.Y - 3,
                    circle.Width + 6,
                    circle.Height + 6);
            }
            if (currentIsTimer && timerSnapshot != null)
            {
                Color accent = timerSnapshot.AccentColor;
                RectangleF timerIcon = new RectangleF(
                    circle.Left + circle.Width * 0.24f,
                    circle.Top + circle.Height * 0.20f,
                    circle.Width * 0.52f,
                    circle.Height * 0.60f);
                if (timerSnapshot.Mode == SmartTimerMode.Countdown)
                {
                    DrawHourglassIcon(
                        graphics,
                        Rectangle.Round(timerIcon),
                        accent,
                        timerSnapshot.IsRunning);
                }
                else
                {
                    DrawStopwatchIcon(
                        graphics,
                        Rectangle.Round(timerIcon),
                        accent,
                        timerSnapshot.DisplayTime);
                }
                return;
            }
            DrawNotificationGlyph(graphics, circle);
        }

        private void DrawExpanded(Graphics graphics)
        {
            double blend = phase == VisualPhase.Detail
                ? Clamp01(detailBlend)
                : 0.0;
            int padding = Scale((int)Math.Round(8 + 4 * blend));
            int iconSize = Scale((int)Math.Round(32 + 4 * blend));
            RectangleF circle = new RectangleF(
                padding,
                (ClientSize.Height - iconSize) / 2f,
                iconSize,
                iconSize);
            using (SolidBrush white = new SolidBrush(Color.FromArgb(244, 243, 238)))
            {
                graphics.FillEllipse(white, circle);
            }
            DrawNotificationGlyph(graphics, circle);

            int textLeft = padding + iconSize +
                Scale((int)Math.Round(10 + blend));
            int rightInset = Scale((int)Math.Round(10 + 6 * blend));
            RectangleF eyebrow = new RectangleF(
                textLeft,
                (float)(Scale(6) + Scale(3) * blend),
                ClientSize.Width - textLeft - Scale(10),
                Scale(14));
            DrawFadingText(
                graphics,
                Localization.Get("messageMode"),
                microFont,
                eyebrow,
                Color.FromArgb(145, 219, 215, 202),
                1.0 - blend);
            DrawFadingText(
                graphics,
                Localization.Get("incoming"),
                microFont,
                eyebrow,
                Color.FromArgb(145, 219, 215, 202),
                blend);

            RectangleF title = new RectangleF(
                textLeft,
                (float)(Scale(19) + Scale(4) * blend),
                ClientSize.Width - textLeft - rightInset,
                Scale(27));
            DrawFadingText(
                graphics,
                current.Title,
                titleFont,
                title,
                Color.FromArgb(246, 245, 240),
                1.0,
                false,
                StringAlignment.Near,
                StringAlignment.Center);

            if (blend > 0.015 && !string.IsNullOrWhiteSpace(current.Body))
            {
                RectangleF body = new RectangleF(
                    textLeft,
                    Scale(50) + (float)(Scale(1) * blend),
                    Math.Max(1, ClientSize.Width - textLeft - rightInset),
                    Scale(33));
                DrawFadingText(
                    graphics,
                    current.Body,
                    bodyFont,
                    body,
                    Color.FromArgb(210, 222, 220, 213),
                    blend,
                    true,
                    StringAlignment.Near,
                    StringAlignment.Near);
            }

            if (blend > 0.01)
            {
                RectangleF app = new RectangleF(
                    ClientSize.Width - Scale(146),
                    Scale(9),
                    Scale(128),
                    Scale(14));
                DrawFadingText(
                    graphics,
                    current.AppName.ToUpperInvariant(),
                    microFont,
                    app,
                    Color.FromArgb(231, 226, 212),
                    0.47 * blend,
                    false,
                    StringAlignment.Far,
                    StringAlignment.Center);
            }
            DrawQueueBadge(graphics, blend);
        }

        private void DrawSideExpanded(Graphics graphics)
        {
            double blend = phase == VisualPhase.Detail
                ? Clamp01(detailBlend)
                : 0.35;
            int padding = Scale(16);
            int iconSize = Scale((int)Math.Round(38 + 4 * blend));
            RectangleF circle = new RectangleF(
                padding,
                Scale(17),
                iconSize,
                iconSize);
            using (SolidBrush white = new SolidBrush(
                Color.FromArgb(244, 243, 238)))
            {
                graphics.FillEllipse(white, circle);
            }
            DrawNotificationGlyph(graphics, circle);

            int textLeft = padding + iconSize + Scale(12);
            int textRight = ClientSize.Width - padding;
            DrawFadingText(
                graphics,
                Localization.Get("messageMode"),
                microFont,
                new RectangleF(
                    textLeft,
                    Scale(14),
                    Math.Max(1, textRight - textLeft),
                    Scale(15)),
                Color.FromArgb(150, 219, 215, 202),
                1.0);

            DrawFadingText(
                graphics,
                current.Title,
                titleFont,
                new RectangleF(
                    textLeft,
                    Scale(31),
                    Math.Max(1, textRight - textLeft),
                    Scale(31)),
                Color.FromArgb(246, 245, 240),
                1.0,
                false,
                StringAlignment.Near,
                StringAlignment.Center);

            if (blend > 0.02 && !string.IsNullOrWhiteSpace(current.Body))
            {
                DrawScrollingText(
                    graphics,
                    current.Body,
                    bodyFont,
                    new RectangleF(
                        padding,
                        Scale(70),
                        ClientSize.Width - padding * 2,
                        Math.Max(1, ClientSize.Height - Scale(102))),
                    Color.FromArgb(215, 222, 220, 213),
                    blend,
                    waveformSeconds);
            }

            DrawFadingText(
                graphics,
                string.IsNullOrWhiteSpace(current.AppName)
                    ? Localization.Get("appNotification")
                    : current.AppName,
                microFont,
                new RectangleF(
                    padding,
                    ClientSize.Height - Scale(27),
                    Math.Max(1, ClientSize.Width - padding * 2 - Scale(38)),
                    Scale(15)),
                Color.FromArgb(160, 219, 215, 202),
                blend);
            DrawQueueBadge(graphics, blend);
        }

        private void DrawQueueBadge(Graphics graphics, double opacity)
        {
            double visibleOpacity = Clamp01(opacity);
            int count = queue.Count;
            if (count <= 0 || visibleOpacity < 0.03)
            {
                return;
            }

            int width = Scale(27);
            int height = Scale(16);
            Rectangle badge = new Rectangle(
                ClientSize.Width - Scale(44),
                Scale(49),
                width,
                height);
            using (GraphicsPath path = RoundedRectangle(
                badge,
                Math.Max(1, height / 2)))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(
                    (int)Math.Round(42 * visibleOpacity),
                    255,
                    255,
                    255)))
            using (Pen outline = new Pen(
                Color.FromArgb(
                    (int)Math.Round(65 * visibleOpacity),
                    255,
                    255,
                    255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(outline, path);
            }
            DrawFadingText(
                graphics,
                "+" + count.ToString(),
                microFont,
                badge,
                Color.FromArgb(210, 238, 235, 224),
                visibleOpacity,
                false,
                StringAlignment.Center,
                StringAlignment.Center);
        }

        private void DrawBatteryRail(Graphics graphics, double opacity)
        {
            double visibleOpacity = Clamp01(opacity);
            if (visibleOpacity < 0.03)
            {
                return;
            }
            int left = Scale(58);
            int right = ClientSize.Width - Scale(58);
            int width = Math.Max(1, right - left);
            int y = ClientSize.Height - Scale(4);
            using (Pen rail = new Pen(
                Color.FromArgb(
                    (int)Math.Round(42 * visibleOpacity),
                    255,
                    255,
                    255),
                1f))
            {
                graphics.DrawLine(rail, left, y, right, y);
            }

            double pulse =
                (DateTime.UtcNow.TimeOfDay.TotalMilliseconds % 1500.0) /
                1500.0;
            int highlightWidth = Math.Max(Scale(26), width / 4);
            int highlightLeft = left +
                (int)Math.Round((width - highlightWidth) * pulse);
            using (LinearGradientBrush light =
                new LinearGradientBrush(
                    new Rectangle(highlightLeft, y, highlightWidth, 1),
                    Color.FromArgb(0, 220, 185, 112),
                    Color.FromArgb(0, 220, 185, 112),
                    LinearGradientMode.Horizontal))
            {
                ColorBlend blend = new ColorBlend(3);
                blend.Colors = new Color[]
                {
                    Color.FromArgb(0, 220, 185, 112),
                    Color.FromArgb(
                        (int)Math.Round(165 * visibleOpacity),
                        235,
                        200,
                        125),
                    Color.FromArgb(0, 220, 185, 112)
                };
                blend.Positions = new float[] { 0f, 0.5f, 1f };
                light.InterpolationColors = blend;
                using (Pen glow = new Pen(light, 1f))
                {
                    graphics.DrawLine(
                        glow,
                        highlightLeft,
                        y,
                        highlightLeft + highlightWidth,
                        y);
                }
            }
        }

        private void DrawTimer(Graphics graphics)
        {
            TimerSnapshot snapshot = timerSnapshot;
            if (snapshot == null)
            {
                return;
            }
            if (SideDocked)
            {
                DrawSideTimer(graphics, snapshot);
                return;
            }

            Color accent = snapshot.AccentColor;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            bool musicCompanion =
                !timerExpanded &&
                HasMusicCompanion();
            int horizontalInset = musicCompanion ? Scale(24) : 0;
            int contentWidth = Math.Max(
                1,
                width - horizontalInset * 2);
            if (musicCompanion)
            {
                using (Pen companion = new Pen(
                    Color.FromArgb(176, 54, 59, 70),
                    Math.Max(2f, Scale(3))))
                {
                    graphics.DrawArc(
                        companion,
                        Scale(4),
                        Scale(19),
                        Scale(16),
                        Scale(38),
                        90,
                        180);
                    graphics.DrawArc(
                        companion,
                        width - Scale(20),
                        Scale(19),
                        Scale(16),
                        Scale(38),
                        -90,
                        180);
                }
            }

            Rectangle iconBounds = new Rectangle(
                horizontalInset + Scale(20),
                Scale(15),
                Scale(44),
                Scale(44));
            if (snapshot.Mode == SmartTimerMode.Countdown)
            {
                DrawHourglassIcon(
                    graphics,
                    iconBounds,
                    accent,
                    snapshot.IsRunning);
            }
            else
            {
                DrawStopwatchIcon(
                    graphics,
                    iconBounds,
                    accent,
                    snapshot.DisplayTime);
            }

            string timeText = snapshot.FormattedTime;
            DrawText(
                graphics,
                timeText,
                timerTimeFont,
                new Rectangle(
                    horizontalInset + Scale(88),
                    Scale(9),
                    Math.Max(1, contentWidth - Scale(112)),
                    Scale(48)),
                accent,
                ContentAlignment.MiddleRight);
            DrawText(
                graphics,
                snapshot.IsCompleted
                    ? Localization.Get("timerFinished")
                    : (snapshot.IsPaused
                        ? Localization.Get("timerPaused")
                        : Localization.Get("timerRunning")),
                microFont,
                new Rectangle(
                    horizontalInset + Scale(90),
                    Scale(48),
                    Math.Max(1, contentWidth - Scale(114)),
                    Scale(16)),
                Color.FromArgb(
                    152,
                    accent.R,
                    accent.G,
                    accent.B),
                ContentAlignment.MiddleRight);

            if (!timerExpanded || height < Scale(130))
            {
                return;
            }

            using (Pen divider = new Pen(
                Color.FromArgb(34, 255, 255, 255),
                1f))
            {
                graphics.DrawLine(
                    divider,
                    horizontalInset + Scale(18),
                    Scale(78),
                    horizontalInset + contentWidth - Scale(18),
                    Scale(78));
            }

            int buttonCount = snapshot.Mode == SmartTimerMode.Stopwatch
                ? 3
                : 2;
            int gap = Scale(8);
            int left = horizontalInset + Scale(18);
            int available = contentWidth - Scale(36) - gap * (buttonCount - 1);
            int buttonWidth = Math.Max(1, available / buttonCount);
            int buttonTop = Scale(92);
            int buttonHeight = Scale(48);
            Rectangle primary = new Rectangle(
                left,
                buttonTop,
                buttonWidth,
                buttonHeight);
            DrawTimerButton(
                graphics,
                primary,
                snapshot.IsCompleted
                    ? Localization.Get("timerClose")
                    : (snapshot.IsPaused
                        ? Localization.Get("timerResume")
                        : Localization.Get("timerPause")),
                accent,
                true);
            int secondLeft = primary.Right + gap;
            if (snapshot.Mode == SmartTimerMode.Stopwatch)
            {
                Rectangle lap = new Rectangle(
                    secondLeft,
                    buttonTop,
                    buttonWidth,
                    buttonHeight);
                DrawTimerButton(
                    graphics,
                    lap,
                    Localization.Get("timerLap"),
                    accent,
                    false);
                secondLeft = lap.Right + gap;
            }
            DrawTimerButton(
                graphics,
                new Rectangle(
                    secondLeft,
                    buttonTop,
                    buttonWidth,
                    buttonHeight),
                Localization.Get("timerCancel"),
                accent,
                false);

            if (snapshot.Mode == SmartTimerMode.Stopwatch &&
                snapshot.Laps != null &&
                snapshot.Laps.Length > 0)
            {
                DrawText(
                    graphics,
                    Localization.Get("timerLastLap") +
                        "  " +
                        FormatClock(snapshot.Laps[0]) +
                        "   × " +
                        snapshot.LapCount,
                    microFont,
                    new Rectangle(
                        horizontalInset + Scale(20),
                        Scale(150),
                        contentWidth - Scale(40),
                        Scale(18)),
                    Color.FromArgb(150, accent.R, accent.G, accent.B),
                    ContentAlignment.MiddleCenter);
            }
        }

        private void DrawSideTimer(
            Graphics graphics,
            TimerSnapshot snapshot)
        {
            Color accent = snapshot.AccentColor;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            bool musicCompanion =
                !timerExpanded &&
                HasMusicCompanion();
            int inset = musicCompanion ? Scale(22) : 0;
            if (musicCompanion)
            {
                using (Pen companion = new Pen(
                    Color.FromArgb(176, 54, 59, 70),
                    Math.Max(2f, Scale(3))))
                {
                    graphics.DrawArc(
                        companion,
                        Scale(4),
                        height / 2 - Scale(20),
                        Scale(16),
                        Scale(40),
                        90,
                        180);
                    graphics.DrawArc(
                        companion,
                        width - Scale(20),
                        height / 2 - Scale(20),
                        Scale(16),
                        Scale(40),
                        -90,
                        180);
                }
            }
            Rectangle icon = new Rectangle(
                (width - Scale(46)) / 2,
                Scale(18),
                Scale(46),
                Scale(46));
            if (snapshot.Mode == SmartTimerMode.Countdown)
            {
                DrawHourglassIcon(
                    graphics,
                    icon,
                    accent,
                    snapshot.IsRunning);
            }
            else
            {
                DrawStopwatchIcon(
                    graphics,
                    icon,
                    accent,
                    snapshot.DisplayTime);
            }
            DrawText(
                graphics,
                snapshot.FormattedTime,
                timerTimeFont,
                new Rectangle(
                    inset + Scale(8),
                    Scale(70),
                    Math.Max(1, width - inset * 2 - Scale(16)),
                    Scale(52)),
                accent,
                ContentAlignment.MiddleCenter);
            DrawText(
                graphics,
                snapshot.IsCompleted
                    ? Localization.Get("timerFinished")
                    : (snapshot.IsPaused
                        ? Localization.Get("timerPaused")
                        : Localization.Get("timerRunning")),
                microFont,
                new Rectangle(
                    inset + Scale(8),
                    Scale(122),
                    Math.Max(1, width - inset * 2 - Scale(16)),
                    Scale(18)),
                Color.FromArgb(145, accent.R, accent.G, accent.B),
                ContentAlignment.MiddleCenter);

            if (!timerExpanded || height < Scale(210))
            {
                return;
            }
            using (Pen divider = new Pen(
                Color.FromArgb(34, 255, 255, 255),
                1f))
            {
                graphics.DrawLine(
                    divider,
                    Scale(18),
                    Scale(154),
                    width - Scale(18),
                    Scale(154));
            }

            int buttonCount = snapshot.Mode == SmartTimerMode.Stopwatch
                ? 3
                : 2;
            int gap = Scale(8);
            int left = Scale(18);
            int available =
                width - Scale(36) - gap * (buttonCount - 1);
            int buttonWidth = Math.Max(1, available / buttonCount);
            int buttonTop = height - Scale(58);
            for (int index = 0; index < buttonCount; index++)
            {
                string label = index == 0
                    ? (snapshot.IsCompleted
                        ? Localization.Get("timerClose")
                        : (snapshot.IsPaused
                            ? Localization.Get("timerResume")
                            : Localization.Get("timerPause")))
                    : (snapshot.Mode == SmartTimerMode.Stopwatch &&
                       index == 1
                        ? Localization.Get("timerLap")
                        : Localization.Get("timerCancel"));
                DrawTimerButton(
                    graphics,
                    new Rectangle(
                        left + index * (buttonWidth + gap),
                        buttonTop,
                        buttonWidth,
                        Scale(42)),
                    label,
                    accent,
                    index == 0);
            }
        }

        private bool HasMusicCompanion()
        {
            return musicModeEnabled &&
                mediaState != null &&
                mediaState.IsPlaying;
        }

        private void DrawMultiTimerPanel(
            Graphics graphics,
            Rectangle panel)
        {
            if (timerSnapshot == null)
            {
                return;
            }
            Color accent = timerSnapshot.AccentColor;
            DrawText(
                graphics,
                timerSnapshot.Mode == SmartTimerMode.Countdown
                    ? Localization.Get("timerCountdown")
                    : Localization.Get("timerStopwatch"),
                microFont,
                new Rectangle(
                    panel.Left + 18,
                    panel.Top + 11,
                    panel.Width - 36,
                    18),
                Color.FromArgb(150, accent.R, accent.G, accent.B),
                ContentAlignment.MiddleLeft);
            Rectangle icon = new Rectangle(
                panel.Left + 18,
                panel.Top + 38,
                Scale(42),
                Scale(42));
            if (timerSnapshot.Mode == SmartTimerMode.Countdown)
            {
                DrawHourglassIcon(
                    graphics,
                    icon,
                    accent,
                    timerSnapshot.IsRunning);
            }
            else
            {
                DrawStopwatchIcon(
                    graphics,
                    icon,
                    accent,
                    timerSnapshot.DisplayTime);
            }
            DrawText(
                graphics,
                timerSnapshot.FormattedTime,
                timerTimeFont,
                new Rectangle(
                    panel.Left + 76,
                    panel.Top + 25,
                    panel.Width - 94,
                    Scale(50)),
                accent,
                ContentAlignment.MiddleRight);
            DrawText(
                graphics,
                timerSnapshot.IsCompleted
                    ? Localization.Get("timerFinished")
                    : (timerSnapshot.IsPaused
                        ? Localization.Get("timerPaused")
                        : Localization.Get("timerRunning")),
                microFont,
                new Rectangle(
                    panel.Left + 18,
                    panel.Top + 87,
                    panel.Width - 36,
                    Scale(18)),
                Color.FromArgb(132, accent.R, accent.G, accent.B),
                ContentAlignment.MiddleRight);

            int count = timerSnapshot.Mode == SmartTimerMode.Stopwatch
                ? 3
                : 2;
            int gap = Scale(8);
            int padding = Scale(18);
            int width = Math.Max(
                Scale(38),
                (panel.Width - padding * 2 - gap * (count - 1)) / count);
            for (int index = 0; index < count; index++)
            {
                Rectangle button = new Rectangle(
                    panel.Left + padding + index * (width + gap),
                    panel.Top + Scale(120),
                    width,
                    Scale(44));
                string label = index == 0
                    ? (timerSnapshot.IsCompleted
                        ? Localization.Get("timerClose")
                        : (timerSnapshot.IsPaused
                            ? Localization.Get("timerResume")
                            : Localization.Get("timerPause")))
                    : (timerSnapshot.Mode == SmartTimerMode.Stopwatch &&
                       index == 1
                        ? Localization.Get("timerLap")
                        : Localization.Get("timerCancel"));
                DrawTimerButton(
                    graphics,
                    button,
                    label,
                    accent,
                    index == 0);
            }
        }

        private void DrawMultiMusicPanel(
            Graphics graphics,
            Rectangle panel)
        {
            MediaPlaybackState state = mediaState;
            string title = state == null ||
                string.IsNullOrWhiteSpace(state.Title)
                    ? Localization.Get("nowPlaying")
                    : state.Title;
            string artist = state == null ||
                string.IsNullOrWhiteSpace(state.Artist)
                    ? (state == null ? "" : state.Album)
                    : state.Artist;
            string appName = state == null ||
                string.IsNullOrWhiteSpace(state.AppName)
                    ? Localization.Get("media")
                    : state.AppName;
            string album = state == null
                ? ""
                : state.Album;
            string secondary = artist;
            if (!string.IsNullOrWhiteSpace(album) &&
                !string.Equals(
                    album,
                    artist,
                    StringComparison.OrdinalIgnoreCase))
            {
                secondary = artist +
                    (string.IsNullOrWhiteSpace(artist) ? "" : " · ") +
                    album;
            }
            Rectangle artwork = new Rectangle(
                panel.Left + 16,
                panel.Top + 15,
                Scale(54),
                Scale(54));
            DrawMultiArtwork(graphics, artwork);

            DrawText(
                graphics,
                appName.ToUpperInvariant(),
                microFont,
                new Rectangle(
                    panel.Left + 82,
                    panel.Top + 14,
                    panel.Width - 100,
                    16),
                Color.FromArgb(145, 218, 218, 211),
                ContentAlignment.MiddleLeft);
            DrawScrollingText(
                graphics,
                title,
                titleFont,
                new RectangleF(
                    panel.Left + 82,
                    panel.Top + 30,
                    panel.Width - 100,
                    25),
                Color.FromArgb(242, 241, 236),
                1.0,
                musicPaused ? 0.0 : waveformSeconds * 0.35);
            DrawScrollingText(
                graphics,
                secondary,
                microFont,
                new RectangleF(
                    panel.Left + 82,
                    panel.Top + 56,
                    panel.Width - 100,
                    18),
                Color.FromArgb(150, 210, 213, 210),
                1.0,
                musicPaused ? 0.0 : waveformSeconds * 0.23);

            Rectangle progress = MultiMusicProgressBounds;
            using (Pen progressTrack = new Pen(
                Color.FromArgb(35, 255, 255, 255),
                3f))
            using (Pen progressFill = new Pen(
                Color.FromArgb(194, musicAccent.R, musicAccent.G, musicAccent.B),
                3f))
            {
                progressTrack.StartCap = LineCap.Round;
                progressTrack.EndCap = LineCap.Round;
                progressFill.StartCap = LineCap.Round;
                progressFill.EndCap = LineCap.Round;
                float y = progress.Top + progress.Height / 2f;
                graphics.DrawLine(
                    progressTrack,
                    progress.Left,
                    y,
                    progress.Right,
                    y);
                double ratio = state == null ||
                    state.DurationSeconds <= 0.1
                        ? 0.0
                        : Clamp01(
                            state.PositionSeconds /
                            state.DurationSeconds);
                graphics.DrawLine(
                    progressFill,
                    progress.Left,
                    y,
                    progress.Left +
                        (float)(progress.Width * ratio),
                    y);
            }

            int barLeft = panel.Right - Scale(112);
            int barTop = panel.Top + Scale(101);
            double motion = musicPaused ? 0.12 : 1.0;
            for (int index = 0; index < 16; index++)
            {
                double phase = waveformSeconds * 7.5 + index * 0.72;
                int barHeight = Math.Max(
                    Scale(3),
                    (int)Math.Round(
                        Scale(3) +
                        Scale(15) *
                        Math.Abs(Math.Sin(phase)) *
                        motion));
                using (Pen bar = new Pen(
                    Color.FromArgb(
                        210,
                        musicAccent.R,
                        musicAccent.G,
                        musicAccent.B),
                    Math.Max(1.5f, Scale(2))))
                {
                    bar.StartCap = LineCap.Round;
                    bar.EndCap = LineCap.Round;
                    int x = barLeft + index * Scale(6);
                    graphics.DrawLine(
                        bar,
                        x,
                        barTop - barHeight / 2,
                        x,
                        barTop + barHeight / 2);
                }
            }

            int buttonGap = Scale(8);
            int padding = Scale(16);
            int width = Math.Max(
                Scale(38),
                (panel.Width - padding * 2 - buttonGap * 2) / 3);
            string[] labels = new string[]
            {
                Localization.Get("previous"),
                musicPaused
                    ? Localization.Get("resume")
                    : Localization.Get("pause"),
                Localization.Get("next")
            };
            for (int index = 0; index < 3; index++)
            {
                DrawTimerButton(
                    graphics,
                    new Rectangle(
                        panel.Left + padding +
                            index * (width + buttonGap),
                        panel.Top + Scale(120),
                        width,
                        Scale(44)),
                    labels[index],
                    musicAccent,
                    index == 1);
            }
        }

        private void DrawMultiArtwork(
            Graphics graphics,
            Rectangle bounds)
        {
            Image artwork = LoadAlbumArtwork();
            using (GraphicsPath path = RoundedRectangle(
                bounds,
                Math.Max(3, Scale(7))))
            {
                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                if (artwork != null)
                {
                    double scale = Math.Max(
                        bounds.Width / (double)artwork.Width,
                        bounds.Height / (double)artwork.Height);
                    int width = (int)Math.Ceiling(
                        artwork.Width * scale);
                    int height = (int)Math.Ceiling(
                        artwork.Height * scale);
                    graphics.DrawImage(
                        artwork,
                        new Rectangle(
                            bounds.Left + (bounds.Width - width) / 2,
                            bounds.Top + (bounds.Height - height) / 2,
                            width,
                            height));
                }
                else
                {
                    using (LinearGradientBrush fill =
                        new LinearGradientBrush(
                            bounds,
                            Color.FromArgb(70, musicAccent.R, musicAccent.G, musicAccent.B),
                            Color.FromArgb(25, 21, 25, 32),
                            55f))
                    {
                        graphics.FillRectangle(fill, bounds);
                    }
                    DrawText(
                        graphics,
                        "♪",
                        titleFont,
                        bounds,
                        Color.FromArgb(218, 240, 220, 172),
                        ContentAlignment.MiddleCenter);
                }
                graphics.Restore(state);
            }
        }

        private void DrawTimerButton(
            Graphics graphics,
            Rectangle bounds,
            string label,
            Color accent,
            bool primary)
        {
            using (GraphicsPath path = RoundedRectangle(
                bounds,
                Scale(12)))
            using (SolidBrush fill = new SolidBrush(
                primary
                    ? Color.FromArgb(
                        78,
                        accent.R,
                        accent.G,
                        accent.B)
                    : Color.FromArgb(24, 255, 255, 255)))
            using (Pen border = new Pen(
                primary
                    ? Color.FromArgb(
                        150,
                        accent.R,
                        accent.G,
                        accent.B)
                    : Color.FromArgb(38, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                label,
                microFont,
                bounds,
                primary
                    ? Color.FromArgb(
                        240,
                        Math.Min(255, accent.R + 32),
                        Math.Min(255, accent.G + 32),
                        Math.Min(255, accent.B + 32))
                    : Color.FromArgb(190, 227, 229, 227),
                ContentAlignment.MiddleCenter);
        }

        private void DrawHourglassIcon(
            Graphics graphics,
            Rectangle bounds,
            Color accent,
            bool running)
        {
            GraphicsState state = graphics.Save();
            double angle = running
                ? Math.Sin(timerAnimationSeconds * 2.3) * 18.0
                : 0.0;
            graphics.TranslateTransform(
                bounds.Left + bounds.Width / 2f,
                bounds.Top + bounds.Height / 2f);
            graphics.RotateTransform((float)angle);
            float halfWidth = bounds.Width * 0.27f;
            float halfHeight = bounds.Height * 0.38f;
            PointF[] upper = new PointF[]
            {
                new PointF(-halfWidth, -halfHeight),
                new PointF(halfWidth, -halfHeight),
                new PointF(0f, 0f)
            };
            PointF[] lower = new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(halfWidth, halfHeight),
                new PointF(-halfWidth, halfHeight)
            };
            using (SolidBrush fill = new SolidBrush(accent))
            using (Pen rim = new Pen(
                Color.FromArgb(
                    220,
                    Math.Min(255, accent.R + 48),
                    Math.Min(255, accent.G + 48),
                    Math.Min(255, accent.B + 48)),
                Math.Max(1.2f, Scale(2))))
            {
                graphics.FillPolygon(fill, upper);
                graphics.FillPolygon(fill, lower);
                graphics.DrawLine(
                    rim,
                    -halfWidth - Scale(2),
                    -halfHeight,
                    halfWidth + Scale(2),
                    -halfHeight);
                graphics.DrawLine(
                    rim,
                    -halfWidth - Scale(2),
                    halfHeight,
                    halfWidth + Scale(2),
                    halfHeight);
            }
            graphics.Restore(state);
        }

        private void DrawStopwatchIcon(
            Graphics graphics,
            Rectangle bounds,
            Color accent,
            TimeSpan elapsed)
        {
            Rectangle dial = new Rectangle(
                bounds.Left + Scale(3),
                bounds.Top + Scale(5),
                bounds.Width - Scale(6),
                bounds.Height - Scale(8));
            using (SolidBrush fill = new SolidBrush(accent))
            {
                graphics.FillEllipse(fill, dial);
            }
            using (Pen crown = new Pen(
                Color.FromArgb(
                    225,
                    Math.Min(255, accent.R + 40),
                    Math.Min(255, accent.G + 40),
                    Math.Min(255, accent.B + 40)),
                Math.Max(1.5f, Scale(2))))
            {
                graphics.DrawLine(
                    crown,
                    dial.Left + dial.Width / 2,
                    bounds.Top + Scale(1),
                    dial.Left + dial.Width / 2,
                    dial.Top + Scale(4));
                graphics.DrawLine(
                    crown,
                    dial.Right - Scale(4),
                    dial.Top + Scale(7),
                    dial.Right - Scale(1),
                    dial.Top + Scale(11));
            }
            double seconds = elapsed.TotalSeconds;
            double angle =
                ((seconds % 60.0) / 60.0) *
                Math.PI * 2.0 -
                Math.PI / 2.0;
            double luminance =
                (0.299 * accent.R +
                 0.587 * accent.G +
                 0.114 * accent.B) /
                255.0;
            Color handColor = luminance > 0.57
                ? Color.FromArgb(235, 15, 24, 35)
                : Color.FromArgb(245, 245, 250, 252);
            Color outlineColor = luminance > 0.57
                ? Color.FromArgb(120, 255, 255, 255)
                : Color.FromArgb(100, 8, 15, 24);
            float centerX = dial.Left + dial.Width / 2f;
            float centerY = dial.Top + dial.Height / 2f;
            float handX = centerX +
                (float)(Math.Cos(angle) * dial.Width * 0.28);
            float handY = centerY +
                (float)(Math.Sin(angle) * dial.Height * 0.28);
            using (Pen outline = new Pen(
                outlineColor,
                Math.Max(3f, Scale(3))))
            {
                outline.StartCap = LineCap.Round;
                outline.EndCap = LineCap.Round;
                graphics.DrawLine(
                    outline,
                    centerX,
                    centerY,
                    handX,
                    handY);
            }
            using (Pen hand = new Pen(
                handColor,
                Math.Max(1.5f, Scale(2))))
            {
                hand.StartCap = LineCap.Round;
                hand.EndCap = LineCap.Round;
                graphics.DrawLine(
                    hand,
                    centerX,
                    centerY,
                    handX,
                    handY);
            }
        }

        private static string FormatClock(TimeSpan value)
        {
            long totalSeconds = Math.Max(
                0,
                (long)Math.Floor(value.TotalSeconds));
            return (totalSeconds / 60).ToString("00") +
                ":" +
                (totalSeconds % 60).ToString("00");
        }

        private void DrawMusic(Graphics graphics)
        {
            if (current == null)
            {
                return;
            }

            double reveal = Clamp01(
                (ClientSize.Width - Scale(SeedSize)) /
                (double)Math.Max(1, Scale(MusicWidth) - Scale(SeedSize)));
            double easedReveal = SmoothStep(reveal);
            int artSize = (int)Math.Round(
                Scale(27) + (Scale(34) - Scale(27)) * easedReveal);
            int centeredLeft = (ClientSize.Width - artSize) / 2;
            int targetLeft = Scale(16);
            int artLeft = (int)Math.Round(
                centeredLeft +
                (targetLeft - centeredLeft) * easedReveal);
            int mediaRowHeight = Scale(MusicHeight);
            Rectangle albumBounds = new Rectangle(
                artLeft,
                (mediaRowHeight - artSize) / 2,
                artSize,
                artSize);
            DrawAlbumArtwork(graphics, albumBounds);

            double textOpacity = Clamp01(
                (ClientSize.Width - Scale(158)) /
                (double)Math.Max(1, Scale(MusicWidth) - Scale(158)));
            if (textOpacity > 0.025)
            {
                int textLeft = Scale(62);
                int textRight = ClientSize.Width - Scale(88);
                string appName = string.IsNullOrWhiteSpace(current.AppName)
                    ? Localization.Get("media")
                    : current.AppName.ToUpperInvariant();
                DrawFadingText(
                    graphics,
                    appName,
                    microFont,
                    new RectangleF(
                        textLeft,
                        Scale(5),
                        Math.Max(1, textRight - textLeft),
                        Scale(12)),
                    Color.FromArgb(160, 219, 215, 202),
                    textOpacity);
                DrawScrollingText(
                    graphics,
                    string.IsNullOrWhiteSpace(current.Title)
                        ? Localization.Get("nowPlaying")
                        : current.Title,
                    titleFont,
                    new RectangleF(
                        textLeft,
                        Scale(17),
                        Math.Max(1, textRight - textLeft),
                        Scale(24)),
                    Color.FromArgb(247, 246, 242),
                    textOpacity,
                    waveformSeconds);

                if (!string.IsNullOrWhiteSpace(current.Body))
                {
                    DrawScrollingText(
                        graphics,
                        current.Body,
                        bodyFont,
                        new RectangleF(
                            textLeft,
                            Scale(42),
                            Math.Max(1, textRight - textLeft),
                            Scale(18)),
                        Color.FromArgb(205, 219, 215, 205),
                        textOpacity * 0.9,
                        waveformSeconds);
                }
            }

            double waveformOpacity = Clamp01(
                (ClientSize.Width - Scale(235)) /
                (double)Math.Max(1, Scale(MusicWidth) - Scale(235)));
            if (waveformOpacity > 0.02)
            {
                DrawWaveform(graphics, waveformOpacity);
            }

            double controlsProgress = Clamp01(
                (ClientSize.Height - Scale(MusicHeight)) /
                (double)Math.Max(
                    1,
                    Scale(MusicExpandedHeight) - Scale(MusicHeight)));
            if (musicControlsVisible && controlsProgress > 0.02)
            {
                DrawMusicControls(graphics, controlsProgress);
            }
        }

        private void DrawSideMusic(Graphics graphics)
        {
            if (current == null)
            {
                return;
            }

            double reveal = Clamp01(
                (ClientSize.Width - Scale(SeedSize)) /
                (double)Math.Max(
                    1,
                    Scale(SideMusicWidth) - Scale(SeedSize)));
            double easedReveal = SmoothStep(reveal);
            int artSize = (int)Math.Round(
                Scale(38) + (Scale(58) - Scale(38)) * easedReveal);
            int targetLeft = Scale(18);
            int centeredLeft = (ClientSize.Width - artSize) / 2;
            int artLeft = (int)Math.Round(
                centeredLeft +
                (targetLeft - centeredLeft) * easedReveal);
            Rectangle albumBounds = new Rectangle(
                artLeft,
                Scale(20),
                artSize,
                artSize);
            DrawAlbumArtwork(graphics, albumBounds);

            double textOpacity = Clamp01(
                (ClientSize.Width - Scale(142)) /
                (double)Math.Max(
                    1,
                    Scale(SideMusicWidth) - Scale(142)));
            if (textOpacity > 0.025)
            {
                int textLeft = Scale(92);
                int textRight = ClientSize.Width - Scale(18);
                DrawFadingText(
                    graphics,
                    string.IsNullOrWhiteSpace(current.AppName)
                        ? Localization.Get("media")
                        : current.AppName.ToUpperInvariant(),
                    microFont,
                    new RectangleF(
                        textLeft,
                        Scale(18),
                        Math.Max(1, textRight - textLeft),
                        Scale(13)),
                    Color.FromArgb(160, 219, 215, 202),
                    textOpacity);
                DrawScrollingText(
                    graphics,
                    string.IsNullOrWhiteSpace(current.Title)
                        ? Localization.Get("nowPlaying")
                        : current.Title,
                    titleFont,
                    new RectangleF(
                        textLeft,
                        Scale(36),
                        Math.Max(1, textRight - textLeft),
                        Scale(26)),
                    Color.FromArgb(247, 246, 242),
                    textOpacity,
                    waveformSeconds);
                if (!string.IsNullOrWhiteSpace(current.Body))
                {
                    DrawScrollingText(
                        graphics,
                        current.Body,
                        bodyFont,
                        new RectangleF(
                            textLeft,
                            Scale(65),
                            Math.Max(1, textRight - textLeft),
                            Scale(20)),
                        Color.FromArgb(205, 219, 215, 205),
                        textOpacity * 0.9,
                        waveformSeconds);
                }
            }

            double waveformOpacity = Clamp01(
                (ClientSize.Width - Scale(178)) /
                (double)Math.Max(
                    1,
                    Scale(SideMusicWidth) - Scale(178)));
            if (waveformOpacity > 0.02)
            {
                DrawSideWaveform(
                    graphics,
                    new Rectangle(
                        Scale(20),
                        Scale(112),
                        ClientSize.Width - Scale(40),
                        Scale(34)),
                    waveformOpacity);
            }

            double controlsProgress = Clamp01(
                (ClientSize.Height - Scale(SideMusicHeight)) /
                (double)Math.Max(
                    1,
                    Scale(SideMusicExpandedHeight) -
                    Scale(SideMusicHeight)));
            if (musicControlsVisible && controlsProgress > 0.02)
            {
                DrawMusicControls(graphics, controlsProgress);
            }
        }

        private void DrawSideWaveform(
            Graphics graphics,
            Rectangle bounds,
            double opacity)
        {
            const int barCount = 17;
            double motionLevel =
                0.12 + 0.88 * Clamp01(waveformMotion);
            double gap = bounds.Width / (double)barCount;
            float barWidth = Math.Max(1f, Scale(2));
            float centerY = bounds.Top + bounds.Height / 2f;
            for (int index = 0; index < barCount; index++)
            {
                double phase =
                    waveformSeconds * 7.5 + index * 0.62;
                double envelope =
                    Math.Sin(waveformSeconds * 2.1 + index * 0.25) *
                    0.24 + 0.76;
                double amplitude =
                    Math.Abs(
                        Math.Sin(phase) * 0.64 +
                        Math.Sin(phase * 1.67 + 0.8) * 0.36) *
                    envelope *
                    motionLevel;
                float height = (float)Math.Max(
                    Scale(3),
                    Math.Round(Scale(4) + Scale(24) * amplitude));
                float x = (float)(
                    bounds.Left +
                    gap * index +
                    gap / 2.0);
                using (Pen bar = new Pen(
                    Color.FromArgb(
                        (int)Math.Round(220 * opacity),
                        musicAccent.R,
                        musicAccent.G,
                        musicAccent.B),
                    barWidth))
                {
                    bar.StartCap = LineCap.Round;
                    bar.EndCap = LineCap.Round;
                    graphics.DrawLine(
                        bar,
                        x,
                        centerY - height / 2f,
                        x,
                        centerY + height / 2f);
                }
            }
        }

        private void DrawMusicControls(Graphics graphics, double opacity)
        {
            string[] labels = new string[]
            {
                Localization.Get("previous"),
                Localization.Get(musicPaused ? "resume" : "pause"),
                Localization.Get("next")
            };

            for (int index = 0; index < 3; index++)
            {
                Rectangle bounds = MusicButtonBounds(index);
                bool primary = index == 1;
                bool hovered = hoveredMusicButton == index;
                using (GraphicsPath path = RoundedRectangle(
                    bounds,
                    Math.Max(2, Scale(8))))
                using (SolidBrush fill = new SolidBrush(
                    primary
                        ? Color.FromArgb(
                            (int)Math.Round((hovered ? 80 : 55) * opacity),
                            musicAccent.R,
                            musicAccent.G,
                            musicAccent.B)
                        : Color.FromArgb(
                            (int)Math.Round((hovered ? 34 : 18) * opacity),
                            255,
                            255,
                            255)))
                using (Pen outline = new Pen(
                    Color.FromArgb(
                        (int)Math.Round((hovered ? 68 : 38) * opacity),
                        255,
                        255,
                        255),
                    1f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(outline, path);
                }

                DrawMusicControlGlyph(
                    graphics,
                    bounds,
                    index,
                    opacity);
                Rectangle labelBounds = new Rectangle(
                    bounds.Left,
                    bounds.Top + Scale(25),
                    bounds.Width,
                    Scale(17));
                DrawFadingText(
                    graphics,
                    labels[index],
                    tinyFont,
                    labelBounds,
                    Color.FromArgb(224, 238, 235, 225),
                    opacity,
                    false,
                    StringAlignment.Center,
                    StringAlignment.Center);
            }
            DrawPlaybackProgress(graphics, opacity);
            DrawVolumeControl(graphics, opacity);
        }

        private void DrawPlaybackProgress(Graphics graphics, double opacity)
        {
            Rectangle track = PlaybackProgressBounds();
            double duration = mediaState == null
                ? 0.0
                : mediaState.DurationSeconds;
            double position = mediaState == null
                ? 0.0
                : mediaState.PositionSeconds;
            double progress = duration <= 0.1
                ? 0.0
                : Clamp01(position / duration);
            using (Pen background = new Pen(
                Color.FromArgb(
                    (int)Math.Round(42 * opacity),
                    255,
                    255,
                    255),
                Math.Max(2f, Scale(3))))
            using (Pen foreground = new Pen(
                Color.FromArgb(
                    (int)Math.Round(205 * opacity),
                    musicAccent.R,
                    musicAccent.G,
                    musicAccent.B),
                Math.Max(2f, Scale(3))))
            {
                background.StartCap = LineCap.Round;
                background.EndCap = LineCap.Round;
                foreground.StartCap = LineCap.Round;
                foreground.EndCap = LineCap.Round;
                float y = track.Top + track.Height / 2f;
                graphics.DrawLine(
                    background,
                    track.Left,
                    y,
                    track.Right,
                    y);
                graphics.DrawLine(
                    foreground,
                    track.Left,
                    y,
                    track.Left + (float)(track.Width * progress),
                    y);
            }
            string currentText = FormatPlaybackTime(position);
            string durationText = duration > 0.1
                ? FormatPlaybackTime(duration)
                : "--:--";
            DrawFadingText(
                graphics,
                currentText,
                microFont,
                new Rectangle(
                    track.Left,
                    track.Bottom + Scale(2),
                    Scale(58),
                    Scale(14)),
                Color.FromArgb(150, 220, 222, 216),
                opacity);
            DrawFadingText(
                graphics,
                durationText,
                microFont,
                new Rectangle(
                    track.Right - Scale(58),
                    track.Bottom + Scale(2),
                    Scale(58),
                    Scale(14)),
                Color.FromArgb(150, 220, 222, 216),
                opacity,
                false,
                StringAlignment.Far,
                StringAlignment.Center);
        }

        private void DrawVolumeControl(Graphics graphics, double opacity)
        {
            Rectangle track = VolumeTrackBounds();
            using (Pen background = new Pen(
                Color.FromArgb(
                    (int)Math.Round(38 * opacity),
                    255,
                    255,
                    255),
                Math.Max(2f, Scale(3))))
            using (Pen foreground = new Pen(
                Color.FromArgb(
                    (int)Math.Round(180 * opacity),
                    215,
                    230,
                    224),
                Math.Max(2f, Scale(3))))
            {
                background.StartCap = LineCap.Round;
                background.EndCap = LineCap.Round;
                foreground.StartCap = LineCap.Round;
                foreground.EndCap = LineCap.Round;
                float y = track.Top + track.Height / 2f;
                graphics.DrawLine(
                    background,
                    track.Left,
                    y,
                    track.Right,
                    y);
                graphics.DrawLine(
                    foreground,
                    track.Left,
                    y,
                    track.Left + (float)(track.Width * volumeLevel),
                    y);
            }
            Rectangle soundButton = SoundSettingsBounds();
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(
                    (int)Math.Round(28 * opacity),
                    255,
                    255,
                    255)))
            using (SolidBrush ink = new SolidBrush(
                Color.FromArgb(
                    (int)Math.Round(205 * opacity),
                    235,
                    240,
                    236)))
            {
                graphics.FillEllipse(fill, soundButton);
                float left = soundButton.Left + Scale(9);
                float centerY = soundButton.Top + soundButton.Height / 2f;
                PointF[] speaker = new PointF[]
                {
                    new PointF(left, centerY - Scale(3)),
                    new PointF(left + Scale(4), centerY - Scale(3)),
                    new PointF(left + Scale(9), centerY - Scale(7)),
                    new PointF(left + Scale(9), centerY + Scale(7)),
                    new PointF(left + Scale(4), centerY + Scale(3)),
                    new PointF(left, centerY + Scale(3))
                };
                graphics.FillPolygon(ink, speaker);
                using (Pen wave = new Pen(ink.Color, 1f))
                {
                    graphics.DrawArc(
                        wave,
                        left + Scale(7),
                        centerY - Scale(6),
                        Scale(9),
                        Scale(12),
                        -55,
                        110);
                }
            }
        }

        private string FormatPlaybackTime(double seconds)
        {
            int total = Math.Max(0, (int)Math.Floor(seconds));
            return (total / 60).ToString("00") +
                ":" +
                (total % 60).ToString("00");
        }

        private void DrawMusicControlGlyph(
            Graphics graphics,
            Rectangle bounds,
            int index,
            double opacity)
        {
            Color color = Color.FromArgb(
                (int)Math.Round(225 * opacity),
                247,
                244,
                235);
            float centerX = bounds.Left + bounds.Width / 2f;
            float centerY = bounds.Top + Scale(13);
            float half = Scale(5);
            using (SolidBrush brush = new SolidBrush(color))
            using (Pen pen = new Pen(
                color,
                Math.Max(1f, Scale(16) / 10f)))
            {
                if (index == 1)
                {
                    if (musicPaused)
                    {
                        PointF[] play = new PointF[]
                        {
                            new PointF(centerX - half * 0.65f, centerY - half),
                            new PointF(centerX + half, centerY),
                            new PointF(centerX - half * 0.65f, centerY + half)
                        };
                        graphics.FillPolygon(brush, play);
                    }
                    else
                    {
                        graphics.FillRectangle(
                            brush,
                            centerX - Scale(4),
                            centerY - half,
                            Scale(25) / 10f,
                            half * 2);
                        graphics.FillRectangle(
                            brush,
                            centerX + Scale(15) / 10f,
                            centerY - half,
                            Scale(25) / 10f,
                            half * 2);
                    }
                }
                else
                {
                    bool previous = index == 0;
                    float direction = previous ? -1f : 1f;
                    float baseX = centerX - direction * half;
                    float tipX = centerX + direction * half;
                    PointF[] triangle = new PointF[]
                    {
                        new PointF(baseX, centerY - half),
                        new PointF(baseX, centerY + half),
                        new PointF(tipX, centerY)
                    };
                    graphics.FillPolygon(brush, triangle);
                    float barX = centerX + direction * (half + Scale(2));
                    graphics.DrawLine(
                        pen,
                        barX,
                        centerY - half,
                        barX,
                        centerY + half);
                }
            }
        }

        private void DrawAlbumArtwork(Graphics graphics, Rectangle bounds)
        {
            Image artwork = LoadAlbumArtwork();
            Rectangle safeBounds = new Rectangle(
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width),
                Math.Max(1, bounds.Height));
            using (GraphicsPath path = RoundedRectangle(
                safeBounds,
                Math.Max(2, Scale(6))))
            {
                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                if (artwork != null)
                {
                    double scale = Math.Max(
                        safeBounds.Width / (double)artwork.Width,
                        safeBounds.Height / (double)artwork.Height);
                    int width = (int)Math.Ceiling(artwork.Width * scale);
                    int height = (int)Math.Ceiling(artwork.Height * scale);
                    Rectangle target = new Rectangle(
                        safeBounds.Left + (safeBounds.Width - width) / 2,
                        safeBounds.Top + (safeBounds.Height - height) / 2,
                        width,
                        height);
                    graphics.DrawImage(artwork, target);
                }
                else
                {
                    using (LinearGradientBrush placeholder =
                        new LinearGradientBrush(
                            safeBounds,
                            Color.FromArgb(82, 70, 52),
                            Color.FromArgb(24, 23, 22),
                            55f))
                    {
                        graphics.FillRectangle(placeholder, safeBounds);
                    }
                    using (StringFormat format = new StringFormat())
                    using (SolidBrush note = new SolidBrush(
                        Color.FromArgb(218, 240, 219, 164)))
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        graphics.DrawString(
                            "♪",
                            titleFont,
                            note,
                            safeBounds,
                            format);
                    }
                }
                graphics.Restore(state);
                using (Pen border = new Pen(
                    Color.FromArgb(68, 255, 255, 255),
                    1f))
                {
                    graphics.DrawPath(border, path);
                }
            }
        }

        private Image LoadAlbumArtwork()
        {
            string path = current == null ? "" : current.ArtworkPath;
            string signature = current == null
                ? ""
                : (current.MediaSignature ?? "");
            if (string.IsNullOrWhiteSpace(path))
            {
                CancelAlbumArtworkRequest();
                ClearAlbumArtwork();
                return null;
            }

            if (albumArtwork != null &&
                string.Equals(
                    albumArtworkPath,
                    path,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    albumArtworkSignature,
                    signature,
                    StringComparison.Ordinal))
            {
                return albumArtwork;
            }

            ClearAlbumArtwork();
            if (DateTime.UtcNow < artworkRetryAfter)
            {
                return null;
            }

            if (artworkLoadInProgress &&
                string.Equals(
                    pendingArtworkPath,
                    path,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    pendingArtworkSignature,
                    signature,
                    StringComparison.Ordinal))
            {
                return null;
            }

            pendingArtworkPath = path;
            pendingArtworkSignature = signature;
            artworkLoadInProgress = true;
            int generation = ++artworkRequestGeneration;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Bitmap loaded = null;
                Color accent = Color.FromArgb(240, 218, 151);
                try
                {
                    using (System.IO.FileStream stream =
                        new System.IO.FileStream(
                            path,
                            System.IO.FileMode.Open,
                            System.IO.FileAccess.Read,
                            System.IO.FileShare.ReadWrite |
                                System.IO.FileShare.Delete))
                    using (Image source = Image.FromStream(stream))
                    {
                        loaded = new Bitmap(source);
                        accent = ExtractMusicAccent(loaded);
                    }
                }
                catch
                {
                }

                try
                {
                    if (IsDisposed || Disposing)
                    {
                        if (loaded != null)
                        {
                            loaded.Dispose();
                        }
                        return;
                    }
                    BeginInvoke(new Action(delegate
                    {
                        ApplyLoadedAlbumArtwork(
                            generation,
                            path,
                            signature,
                            loaded,
                            accent);
                    }));
                }
                catch
                {
                    if (loaded != null)
                    {
                        loaded.Dispose();
                    }
                }
            });
            return null;
        }

        private void ApplyLoadedAlbumArtwork(
            int generation,
            string path,
            string signature,
            Bitmap loaded,
            Color accent)
        {
            if (generation != artworkRequestGeneration ||
                IsDisposed)
            {
                if (loaded != null)
                {
                    loaded.Dispose();
                }
                return;
            }

            artworkLoadInProgress = false;
            pendingArtworkPath = null;
            pendingArtworkSignature = null;
            if (loaded == null ||
                current == null ||
                !string.Equals(
                    current.ArtworkPath,
                    path,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    current.MediaSignature ?? "",
                    signature,
                    StringComparison.Ordinal))
            {
                if (loaded != null)
                {
                    loaded.Dispose();
                }
                artworkRetryAfter = DateTime.UtcNow.AddMilliseconds(220);
                return;
            }

            if (albumArtwork != null)
            {
                albumArtwork.Dispose();
            }
            albumArtwork = loaded;
            albumArtworkPath = path;
            albumArtworkSignature = signature;
            musicAccent = accent;
            artworkRetryAfter = DateTime.MinValue;
            RequestRender();
        }

        private void ClearAlbumArtwork()
        {
            if (albumArtwork != null)
            {
                albumArtwork.Dispose();
                albumArtwork = null;
            }
            albumArtworkPath = "";
            albumArtworkSignature = "";
            musicAccent = Color.FromArgb(240, 218, 151);
        }

        private void CancelAlbumArtworkRequest()
        {
            artworkRequestGeneration++;
            artworkLoadInProgress = false;
            pendingArtworkPath = null;
            pendingArtworkSignature = null;
            artworkRetryAfter = DateTime.MinValue;
        }

        private static Color ExtractMusicAccent(Bitmap bitmap)
        {
            const int bucketCount = 18;
            double[] weights = new double[bucketCount];
            double[] red = new double[bucketCount];
            double[] green = new double[bucketCount];
            double[] blue = new double[bucketCount];
            int stepX = Math.Max(1, bitmap.Width / 48);
            int stepY = Math.Max(1, bitmap.Height / 48);
            Rectangle lockArea = new Rectangle(
                0,
                0,
                bitmap.Width,
                bitmap.Height);
            System.Drawing.Imaging.BitmapData data =
                bitmap.LockBits(
                    lockArea,
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            byte[] pixels = new byte[
                Math.Abs(data.Stride) * bitmap.Height];
            try
            {
                Marshal.Copy(
                    data.Scan0,
                    pixels,
                    0,
                    pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            for (int y = 0; y < bitmap.Height; y += stepY)
            {
                int row = y * data.Stride;
                for (int x = 0; x < bitmap.Width; x += stepX)
                {
                    int offset = row + x * 4;
                    int blueValue = pixels[offset];
                    int greenValue = pixels[offset + 1];
                    int redValue = pixels[offset + 2];
                    int a = pixels[offset + 3];
                    if (a < 128)
                    {
                        continue;
                    }

                    int maximum = Math.Max(
                        redValue,
                        Math.Max(greenValue, blueValue));
                    int minimum = Math.Min(
                        redValue,
                        Math.Min(greenValue, blueValue));
                    if (maximum < 45)
                    {
                        continue;
                    }
                    double saturation =
                        (maximum - minimum) / (double)maximum;
                    double value = maximum / 255.0;
                    if (saturation < 0.10 || value < 0.16)
                    {
                        continue;
                    }

                    int bucket = (int)(
                        RgbHue(
                            redValue,
                            greenValue,
                            blueValue,
                            maximum,
                            minimum) /
                        360.0 * bucketCount) % bucketCount;
                    double weight =
                        value * 0.45 +
                        saturation * 1.25;
                    weights[bucket] += weight;
                    red[bucket] += redValue * weight;
                    green[bucket] += greenValue * weight;
                    blue[bucket] += blueValue * weight;
                }
            }

            int best = -1;
            for (int index = 0; index < bucketCount; index++)
            {
                if (weights[index] > 0.01 &&
                    (best < 0 || weights[index] > weights[best]))
                {
                    best = index;
                }
            }
            if (best < 0)
            {
                return Color.FromArgb(240, 218, 151);
            }

            double total = weights[best];
            int r = (int)Math.Round(red[best] / total);
            int g = (int)Math.Round(green[best] / total);
            int b = (int)Math.Round(blue[best] / total);
            int brightest = Math.Max(r, Math.Max(g, b));
            if (brightest > 0 && brightest < 205)
            {
                double scale = 205.0 / brightest;
                r = Math.Min(255, (int)Math.Round(r * scale));
                g = Math.Min(255, (int)Math.Round(g * scale));
                b = Math.Min(255, (int)Math.Round(b * scale));
            }
            return Color.FromArgb(r, g, b);
        }

        private static double RgbHue(
            int red,
            int green,
            int blue,
            int maximum,
            int minimum)
        {
            int delta = maximum - minimum;
            if (delta <= 0)
            {
                return 0.0;
            }
            double hue;
            if (maximum == red)
            {
                hue = (green - blue) / (double)delta;
            }
            else if (maximum == green)
            {
                hue = 2.0 + (blue - red) / (double)delta;
            }
            else
            {
                hue = 4.0 + (red - green) / (double)delta;
            }
            hue *= 60.0;
            return hue < 0.0 ? hue + 360.0 : hue;
        }

        private void DrawWaveform(Graphics graphics, double opacity)
        {
            int barCount = 9;
            int availableWidth = Scale(66);
            int left = ClientSize.Width - availableWidth - Scale(11);
            int centerY = Scale(MusicHeight) / 2;
            double seconds = waveformSeconds;
            double motionLevel =
                0.12 + 0.88 * Clamp01(waveformMotion);
            double gap = Scale(23) / 10.0;
            float barWidth = Math.Max(1f, Scale(2));

            for (int index = 0; index < barCount; index++)
            {
                double phase = seconds * 7.5 + index * 0.72;
                double envelope =
                    Math.Sin(seconds * 2.1 + index * 0.34) * 0.28 + 0.72;
                double amplitude =
                    Math.Abs(
                        Math.Sin(phase) * 0.62 +
                        Math.Sin(phase * 1.73 + 1.1) * 0.38) *
                    envelope *
                    motionLevel;
                int height = Math.Max(
                    Scale(3),
                    (int)Math.Round(
                        Scale(4) + Scale(16) * amplitude));
                float x = (float)(left + index * (barWidth + gap));
                using (Pen bar = new Pen(
                    Color.FromArgb(
                        (int)Math.Round(225 * opacity),
                        musicAccent.R,
                        musicAccent.G,
                        musicAccent.B),
                    barWidth))
                {
                    bar.StartCap = LineCap.Round;
                    bar.EndCap = LineCap.Round;
                    graphics.DrawLine(
                        bar,
                        x,
                        centerY - height / 2f,
                        x,
                        centerY + height / 2f);
                }
            }
        }

        private void DrawMusicCollapsing(Graphics graphics)
        {
            int seedWidth = Scale(SeedSize);
            int musicWidth = Scale(MusicWidth);
            double ratio = Clamp01(
                (currentWidth - seedWidth) /
                (double)Math.Max(1, musicWidth - seedWidth));
            double movement = SmoothStep(1.0 - ratio);
            int desiredSize = (int)Math.Round(
                Scale(27) + (Scale(34) - Scale(27)) * ratio);
            int availableSize = Math.Max(
                2,
                Math.Min(ClientSize.Width, ClientSize.Height) - Scale(4));
            int artSize = Math.Min(desiredSize, availableSize);
            int startLeft = Scale(16);
            int centeredLeft = (ClientSize.Width - artSize) / 2;
            int artLeft = (int)Math.Round(
                startLeft + (centeredLeft - startLeft) * movement);
            Rectangle artwork = new Rectangle(
                artLeft,
                (ClientSize.Height - artSize) / 2,
                artSize,
                artSize);
            DrawAlbumArtwork(graphics, artwork);

            double textOpacity = Clamp01(
                (ClientSize.Width - Scale(132)) /
                (double)Math.Max(1, musicWidth - Scale(132)));
            if (textOpacity > 0.025)
            {
                int textLeft = Scale(62);
                int textRight = ClientSize.Width - Scale(88);
                DrawFadingText(
                    graphics,
                    string.IsNullOrWhiteSpace(current.AppName)
                        ? Localization.Get("media")
                        : current.AppName.ToUpperInvariant(),
                    microFont,
                    new RectangleF(
                        textLeft,
                        Scale(5),
                        Math.Max(1, textRight - textLeft),
                        Scale(12)),
                    Color.FromArgb(160, 219, 215, 202),
                    textOpacity);
                DrawScrollingText(
                    graphics,
                    string.IsNullOrWhiteSpace(current.Title)
                        ? Localization.Get("nowPlaying")
                        : current.Title,
                    titleFont,
                    new RectangleF(
                        textLeft,
                        Scale(17),
                        Math.Max(1, textRight - textLeft),
                        Scale(24)),
                    Color.FromArgb(247, 246, 242),
                    textOpacity,
                    waveformSeconds);
                if (!string.IsNullOrWhiteSpace(current.Body))
                {
                    DrawScrollingText(
                        graphics,
                        current.Body,
                        bodyFont,
                        new RectangleF(
                            textLeft,
                            Scale(42),
                            Math.Max(1, textRight - textLeft),
                            Scale(18)),
                        Color.FromArgb(205, 219, 215, 205),
                        textOpacity * 0.9,
                        waveformSeconds);
                }
            }

            double waveOpacity = Clamp01(
                (ClientSize.Width - Scale(124)) /
                (double)Math.Max(1, musicWidth - Scale(124)));
            if (waveOpacity > 0.02)
            {
                DrawWaveform(graphics, waveOpacity);
            }
            DrawCollapseParticleErase(graphics);
        }

        private void DrawCollapsing(Graphics graphics)
        {
            if (currentIsMedia && musicEnding)
            {
                DrawMusicCollapsing(graphics);
                return;
            }

            int detailWidth = Scale(DetailWidth);
            int seedWidth = Scale(SeedSize);
            double ratio = Clamp01(
                (currentWidth - seedWidth) /
                (double)Math.Max(1, detailWidth - seedWidth));
            double movement = SmoothStep(1.0 - ratio);

            int contentDiameter = Scale(34);
            int seedDiameter = Scale(27);
            int desiredDiameter = (int)Math.Round(
                seedDiameter +
                (contentDiameter - seedDiameter) * ratio);
            int availableDiameter = Math.Max(
                2,
                Math.Min(ClientSize.Width, ClientSize.Height) - Scale(6));
            int iconDiameter = Math.Min(desiredDiameter, availableDiameter);
            int contentLeft = Scale(11);
            int centeredLeft = (ClientSize.Width - iconDiameter) / 2;
            int iconLeft = (int)Math.Round(
                contentLeft + (centeredLeft - contentLeft) * movement);
            RectangleF circle = new RectangleF(
                iconLeft,
                (ClientSize.Height - iconDiameter) / 2f,
                iconDiameter,
                iconDiameter);
            using (SolidBrush white = new SolidBrush(Color.FromArgb(244, 243, 238)))
            {
                graphics.FillEllipse(white, circle);
            }
            DrawNotificationGlyph(graphics, circle);

            double fadeStart = Scale(175);
            double maxWidth = Math.Max(fadeStart + 1, Scale(DetailWidth));
            double textAlpha = Clamp01(
                (currentWidth - fadeStart) / (maxWidth - fadeStart));
            if (textAlpha > 0.025 && ClientSize.Width >= Scale(82))
            {
                int textLeft = Scale(56);
                RectangleF titleBounds = new RectangleF(
                    textLeft,
                    Scale(20),
                    Math.Max(1, ClientSize.Width - textLeft - Scale(12)),
                    Scale(22));
                DrawFadingText(
                    graphics,
                    current.Title,
                    titleFont,
                    titleBounds,
                    Color.FromArgb(247, 246, 242),
                    textAlpha);

                if (!string.IsNullOrWhiteSpace(current.Body))
                {
                    RectangleF bodyBounds = new RectangleF(
                        textLeft,
                        Scale(43),
                        Math.Max(1, ClientSize.Width - textLeft - Scale(12)),
                        Scale(22));
                    DrawFadingText(
                        graphics,
                        current.Body,
                        bodyFont,
                        bodyBounds,
                        Color.FromArgb(210, 222, 220, 213),
                        textAlpha * 0.9);
                }
            }
            DrawCollapseParticleErase(graphics);
        }

        private static void DrawFadingText(
            Graphics graphics,
            string text,
            Font font,
            RectangleF bounds,
            Color color,
            double opacity,
            bool wrap = false,
            StringAlignment horizontalAlignment = StringAlignment.Near,
            StringAlignment verticalAlignment = StringAlignment.Near)
        {
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(
                    (int)Math.Round(255 * Clamp01(opacity)),
                    color.R,
                    color.G,
                    color.B)))
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = wrap
                    ? StringFormatFlags.LineLimit
                    : StringFormatFlags.NoWrap;
                format.Alignment = horizontalAlignment;
                format.LineAlignment = verticalAlignment;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private static void DrawText(
            Graphics graphics,
            string text,
            Font font,
            Rectangle bounds,
            Color color,
            ContentAlignment alignment)
        {
            DrawFadingText(
                graphics,
                text,
                font,
                bounds,
                color,
                1.0,
                false,
                alignment == ContentAlignment.MiddleCenter ||
                    alignment == ContentAlignment.TopCenter
                    ? StringAlignment.Center
                    : (alignment == ContentAlignment.MiddleRight ||
                       alignment == ContentAlignment.TopRight
                        ? StringAlignment.Far
                        : StringAlignment.Near),
                alignment == ContentAlignment.MiddleLeft ||
                    alignment == ContentAlignment.MiddleCenter ||
                    alignment == ContentAlignment.MiddleRight
                    ? StringAlignment.Center
                    : StringAlignment.Near);
        }

        private void DrawScrollingText(
            Graphics graphics,
            string text,
            Font font,
            RectangleF bounds,
            Color color,
            double opacity,
            double scrollSeconds)
        {
            if (string.IsNullOrWhiteSpace(text) ||
                bounds.Width < Scale(12))
            {
                return;
            }

            SizeF measured = graphics.MeasureString(
                text,
                font,
                PointF.Empty,
                StringFormat.GenericTypographic);
            if (measured.Width <= bounds.Width)
            {
                DrawFadingText(
                    graphics,
                    text,
                    font,
                    bounds,
                    color,
                    opacity,
                    false,
                    StringAlignment.Near,
                    StringAlignment.Center);
                return;
            }

            float gap = Scale(42);
            float cycle = measured.Width + gap;
            float offset = (float)(
                scrollSeconds * Scale(24)) % cycle;
            float y = bounds.Top +
                Math.Max(0f, (bounds.Height - measured.Height) / 2f);
            GraphicsState state = graphics.Save();
            graphics.SetClip(bounds);
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(
                    (int)Math.Round(255 * Clamp01(opacity)),
                    color.R,
                    color.G,
                    color.B)))
            {
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    bounds.Left - offset,
                    y,
                    StringFormat.GenericTypographic);
                graphics.DrawString(
                    text,
                    font,
                    brush,
                    bounds.Left - offset + cycle,
                    y,
                    StringFormat.GenericTypographic);
            }
            graphics.Restore(state);
        }

        private void DrawNotificationGlyph(Graphics graphics, RectangleF circle)
        {
            string label = AppMonogram(current == null ? "" : current.AppName);
            using (StringFormat format = new StringFormat())
            using (SolidBrush ink = new SolidBrush(Color.FromArgb(18, 18, 20)))
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString(label, titleFont, ink, circle, format);
            }
        }

        private static string AppMonogram(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return "N";
            }
            string trimmed = appName.Trim();
            if (trimmed.IndexOf("Codex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "C";
            }
            if (trimmed.IndexOf("QQ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Q";
            }
            return trimmed.Substring(0, 1).ToUpperInvariant();
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }
            if (value > 1.0)
            {
                return 1.0;
            }
            return value;
        }

        private static double SmoothStep(double value)
        {
            double clamped = Clamp01(value);
            return clamped * clamped * (3.0 - 2.0 * clamped);
        }

        private void OnIslandClick(object sender, MouseEventArgs e)
        {
            if (DateTime.UtcNow < suppressClickUntil)
            {
                return;
            }
            if (currentIsTimer &&
                current != null &&
                phase == VisualPhase.Timer)
            {
                if (!timerExpanded)
                {
                    timerExpanded = true;
                    SetSpringTarget(
                        HasMusicCompanion()
                            ? TimerMultiWindowWidth
                            : TimerWindowWidth,
                        HasMusicCompanion()
                            ? TimerMultiWindowHeight
                            : TimerExpandedWindowHeight,
                        0.0);
                    StartFrameClock();
                    ResetControlsTimeout();
                    RequestRender();
                    return;
                }

                int timerButton = HitTestTimerButton(e.Location);
                if (timerButton == 0)
                {
                    EventHandler<SmartTimerMode> handler = timerSnapshot != null &&
                        timerSnapshot.IsCompleted
                            ? TimerCancelRequested
                            : TimerPauseToggleRequested;
                    if (handler != null)
                    {
                        handler(this, timerSnapshot.Mode);
                    }
                    ResetControlsTimeout();
                    return;
                }
                if (timerButton == 1 &&
                    timerSnapshot != null &&
                    timerSnapshot.Mode == SmartTimerMode.Stopwatch)
                {
                    EventHandler<SmartTimerMode> handler =
                        TimerLapRequested;
                    if (handler != null)
                    {
                        handler(this, SmartTimerMode.Stopwatch);
                    }
                    ResetControlsTimeout();
                    return;
                }
                if (timerButton >= 1)
                {
                    EventHandler<SmartTimerMode> handler =
                        TimerCancelRequested;
                    if (handler != null)
                    {
                        handler(this, timerSnapshot.Mode);
                    }
                    ResetControlsTimeout();
                    return;
                }

                int musicButton = HitTestMultiMusicButton(e.Location);
                if (musicButton >= 0)
                {
                    MediaCommandAction action =
                        musicButton == 0
                            ? MediaCommandAction.Previous
                            : (musicButton == 1
                                ? MediaCommandAction.TogglePlayPause
                                : MediaCommandAction.Next);
                    EventHandler<MediaCommandAction> handler =
                        MediaCommandRequested;
                    if (handler != null)
                    {
                        handler(this, action);
                    }
                    ResetControlsTimeout();
                    return;
                }
                if (IsMultiWindowExpanded &&
                    MultiMusicPanelBounds.Contains(e.Location))
                {
                    Rectangle progress = MultiMusicProgressBounds;
                    if (progress.Contains(e.Location) &&
                        mediaState != null &&
                        mediaState.DurationSeconds > 0.1)
                    {
                        double fraction = Clamp01(
                            (e.X - progress.Left) /
                            (double)Math.Max(1, progress.Width));
                        EventHandler<double> seekHandler = SeekRequested;
                        if (seekHandler != null)
                        {
                            seekHandler(
                                this,
                                mediaState.DurationSeconds * fraction);
                        }
                    }
                    else if (e.Y <
                        MultiMusicPanelBounds.Top + Scale(118))
                    {
                        EventHandler sourceHandler =
                            MediaSourceOpenRequested;
                        if (sourceHandler != null)
                        {
                            sourceHandler(this, EventArgs.Empty);
                        }
                    }
                    ResetControlsTimeout();
                    return;
                }

                StopControlsTimeout();
                timerExpanded = false;
                SetSpringTarget(
                    TimerWindowWidth,
                    TimerCompactWindowHeight,
                    0.0);
                RequestRender();
                return;
            }
            if (currentIsMedia && current != null &&
                phase == VisualPhase.Music)
            {
                if (!musicControlsVisible)
                {
                    ExpandMusicControls();
                    return;
                }

                if (SoundSettingsBounds().Contains(e.Location))
                {
                    EventHandler soundHandler = SoundSettingsRequested;
                    if (soundHandler != null)
                    {
                        soundHandler(this, EventArgs.Empty);
                    }
                    ResetControlsTimeout();
                    return;
                }

                if (PlaybackProgressBounds().Contains(e.Location) &&
                    mediaState != null &&
                    mediaState.DurationSeconds > 0.1)
                {
                    Rectangle track = PlaybackProgressBounds();
                    double fraction = Clamp01(
                        (e.X - track.Left) /
                        (double)Math.Max(1, track.Width));
                    EventHandler<double> seekHandler = SeekRequested;
                    if (seekHandler != null)
                    {
                        seekHandler(
                            this,
                            mediaState.DurationSeconds * fraction);
                    }
                    ResetControlsTimeout();
                    return;
                }

                if (e.Location.Y <= Scale(24))
                {
                    EventHandler sourceHandler =
                        MediaSourceOpenRequested;
                    if (sourceHandler != null)
                    {
                        sourceHandler(this, EventArgs.Empty);
                    }
                    ResetControlsTimeout();
                    return;
                }

                int button = HitTestMusicButton(e.Location);
                if (button >= 0)
                {
                    MediaCommandAction action =
                        button == 0
                            ? MediaCommandAction.Previous
                            : (button == 1
                                ? MediaCommandAction.TogglePlayPause
                                : MediaCommandAction.Next);
                    EventHandler<MediaCommandAction> handler =
                        MediaCommandRequested;
                    if (handler != null)
                    {
                        handler(this, action);
                    }
                    ResetControlsTimeout();
                }
                else
                {
                    ResetControlsTimeout();
                }
                return;
            }

            if (current != null)
            {
                DismissCurrent();
            }
        }

        private void OnIslandDoubleClick(object sender, MouseEventArgs e)
        {
            if (DateTime.UtcNow < suppressClickUntil)
            {
                return;
            }
            if (!currentIsMedia || current == null)
            {
                return;
            }
            if (musicControlsVisible &&
                HitTestMusicButton(e.Location) >= 0)
            {
                return;
            }

            StopControlsTimeout();
            EventHandler handler = MusicWindowCloseRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void OnIslandMouseDown(object sender, MouseEventArgs e)
        {
            mouseDownForDrag = false;
            dragMoved = false;
            if (e.Button == MouseButtons.Left &&
                currentIsMedia &&
                musicControlsVisible &&
                VolumeTrackBounds().Contains(e.Location))
            {
                draggingVolume = true;
                Capture = true;
                UpdateVolumeFromPoint(e.X);
                return;
            }
            if (!movementEnabled ||
                clickThroughEnabled ||
                e.Button != MouseButtons.Left ||
                current == null)
            {
                return;
            }
            if (currentIsMedia &&
                musicControlsVisible &&
                HitTestMusicButton(e.Location) >= 0)
            {
                return;
            }
            if (currentIsTimer &&
                timerExpanded &&
                HitTestTimerButton(e.Location) >= 0)
            {
                return;
            }
            if (currentIsTimer &&
                timerExpanded &&
                HitTestMultiMusicButton(e.Location) >= 0)
            {
                return;
            }

            dragStartScreen = Control.MousePosition;
            dragLastScreen = dragStartScreen;
            dragGrabOffset = new Point(
                dragStartScreen.X -
                    Bounds.Left -
                    currentWidth / 2,
                dragStartScreen.Y -
                    Bounds.Top -
                    currentHeight / 2);
            mouseDownForDrag = true;
            Capture = true;
        }

        private void OnIslandMouseMove(object sender, MouseEventArgs e)
        {
            if (currentIsTimer && timerExpanded)
            {
                ResetControlsTimeout();
            }
            if (draggingVolume)
            {
                UpdateVolumeFromPoint(e.X);
                return;
            }
            if (mouseDownForDrag &&
                (e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                Point screenPoint = Control.MousePosition;
                int threshold = Math.Max(7, Scale(7));
                if (!dragMoved &&
                    (Math.Abs(screenPoint.X - dragStartScreen.X) >= threshold ||
                     Math.Abs(screenPoint.Y - dragStartScreen.Y) >= threshold))
                {
                    BeginWindowDrag(screenPoint);
                }
                if (dragMoved)
                {
                    dragLastScreen = screenPoint;
                    UpdateDockPreview(screenPoint);
                    ApplyGeometry();
                    RequestRender();
                    return;
                }
            }

            int nextHovered =
                currentIsMedia &&
                current != null &&
                phase == VisualPhase.Music &&
                musicControlsVisible
                    ? HitTestMusicButton(e.Location)
                    : -1;
            if (nextHovered != hoveredMusicButton)
            {
                hoveredMusicButton = nextHovered;
                Cursor = nextHovered >= 0
                    ? Cursors.Hand
                    : (movementEnabled
                        ? Cursors.SizeAll
                        : Cursors.Hand);
                RequestRender();
            }
        }

        private void OnIslandMouseUp(object sender, MouseEventArgs e)
        {
            if (draggingVolume && e.Button == MouseButtons.Left)
            {
                draggingVolume = false;
                Capture = false;
                ResetControlsTimeout();
                return;
            }
            if (!mouseDownForDrag || e.Button != MouseButtons.Left)
            {
                return;
            }
            mouseDownForDrag = false;
            Capture = false;
            if (!dragMoved)
            {
                return;
            }

            dragLastScreen = Control.MousePosition;
            FinishWindowDrag();
        }

        private void UpdateVolumeFromPoint(int x)
        {
            Rectangle track = VolumeTrackBounds();
            double value = Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    (x - track.Left) /
                        (double)Math.Max(1, track.Width)));
            volumeLevel = value;
            EventHandler<double> handler = VolumeChangeRequested;
            if (handler != null)
            {
                handler(this, value);
            }
            RequestRender();
        }

        private void BeginWindowDrag(Point screenPoint)
        {
            dragMoved = true;
            draggingWindow = true;
            snappingToDock = false;
            dockSnapBlend = 0.0;
            dockSnapVelocity = 0.0;
            dragLastScreen = screenPoint;
            CancelQueuedAction();
            CancelNotificationCollapse();
            StopControlsTimeout();
            musicControlsVisible = false;
            hoveredMusicButton = -1;
            phase = VisualPhase.Seed;
            waveformMotionTarget =
                musicPaused ? 0.0 : 1.0;
            SetSpringTarget(
                Scale(SeedSize),
                Scale(SeedSize),
                0.0);
            Cursor = Cursors.SizeAll;
            StartFrameClock();
            ApplyGeometry();
            RequestRender();
            UpdateDockPreview(screenPoint);
        }

        private void CalculateDockPlacement(
            Point point,
            out DockEdge edge,
            out int position)
        {
            Screen screen = Screen.FromPoint(point);
            Rectangle bounds = screen.WorkingArea;
            int topDistance = Math.Abs(point.Y - bounds.Top);
            int bottomDistance = Math.Abs(bounds.Bottom - point.Y);
            int leftDistance = Math.Abs(point.X - bounds.Left);
            int rightDistance = Math.Abs(bounds.Right - point.X);
            edge = DockEdge.Top;
            int shortest = topDistance;
            if (bottomDistance < shortest)
            {
                edge = DockEdge.Bottom;
                shortest = bottomDistance;
            }
            if (leftDistance < shortest)
            {
                edge = DockEdge.Left;
                shortest = leftDistance;
            }
            if (rightDistance < shortest)
            {
                edge = DockEdge.Right;
            }

            if (edge == DockEdge.Top ||
                edge == DockEdge.Bottom)
            {
                double horizontal = bounds.Width <= 0
                    ? 0.5
                    : (point.X - bounds.Left) /
                        (double)bounds.Width;
                position = horizontal < 1.0 / 3.0
                    ? 0
                    : (horizontal > 2.0 / 3.0 ? 2 : 1);
            }
            else
            {
                double vertical = bounds.Height <= 0
                    ? 0.5
                    : (point.Y - bounds.Top) /
                        (double)bounds.Height;
                position = vertical < 1.0 / 3.0
                    ? 0
                    : (vertical > 2.0 / 3.0 ? 2 : 1);
            }
        }

        private void UpdateDockPreview(Point point)
        {
            DockEdge edge;
            int position;
            CalculateDockPlacement(point, out edge, out position);
            Screen screen = Screen.FromPoint(point);
            Rectangle area = screen.WorkingArea;
            bool side = edge == DockEdge.Left || edge == DockEdge.Right;
            int width = currentIsMedia
                ? Scale(side ? SideMusicWidth : MusicWidth)
                : Scale(side ? SideDetailWidth : DetailWidth);
            int height = currentIsMedia
                ? Scale(side ? SideMusicHeight : MusicHeight)
                : Scale(side ? SideDetailHeight : DetailHeight);
            int margin = Scale(7);
            int left;
            int top;
            if (edge == DockEdge.Bottom)
            {
                left = PositionedHorizontal(
                    area,
                    width,
                    margin,
                    position);
                top = area.Bottom - height - margin;
            }
            else if (edge == DockEdge.Left)
            {
                left = area.Left + margin;
                top = PositionedVertical(
                    area,
                    height,
                    margin,
                    position);
            }
            else if (edge == DockEdge.Right)
            {
                left = area.Right - width - margin;
                top = PositionedVertical(
                    area,
                    height,
                    margin,
                    position);
            }
            else
            {
                left = PositionedHorizontal(
                    area,
                    width,
                    margin,
                    position);
                top = area.Top + margin;
            }
            dockPreview.ShowPreview(
                new Rectangle(left, top, width, height),
                Scale(24));
        }

        private Rectangle CalculateDockedBounds(
            Screen screen,
            int width,
            int height,
            DockEdge edge,
            int position)
        {
            Rectangle area = screen.WorkingArea;
            int margin = Scale(7);
            int left;
            int top;
            if (edge == DockEdge.Bottom)
            {
                left = PositionedHorizontal(
                    area,
                    width,
                    margin,
                    position);
                top = area.Bottom - height - margin;
            }
            else if (edge == DockEdge.Left)
            {
                left = area.Left + margin;
                top = PositionedVertical(
                    area,
                    height,
                    margin,
                    position);
            }
            else if (edge == DockEdge.Right)
            {
                left = area.Right - width - margin;
                top = PositionedVertical(
                    area,
                    height,
                    margin,
                    position);
            }
            else
            {
                left = PositionedHorizontal(
                    area,
                    width,
                    margin,
                    position);
                top = area.Top + margin;
            }
            return new Rectangle(left, top, width, height);
        }

        private void PrefetchFrostedBackdropForTarget()
        {
            if (!IsHandleCreated ||
                IsDisposed ||
                !glassModeEnabled ||
                !frostedGlassEnabled ||
                frostedIntensity <= 0.01)
            {
                return;
            }

            Screen screen = ResolveDockScreen(
                new Point(
                    Bounds.Left + Math.Max(1, Bounds.Width) / 2,
                    Bounds.Top + Math.Max(1, Bounds.Height) / 2));
            int targetWidth = currentIsTimer
                ? (timerExpanded && HasMusicCompanion()
                    ? TimerMultiWindowWidth
                    : TimerWindowWidth)
                : (currentIsMedia
                    ? MusicTargetWidth
                    : NotificationTargetWidth(false));
            int targetHeight = currentIsTimer
                ? (timerExpanded && HasMusicCompanion()
                    ? TimerMultiWindowHeight
                    : (timerExpanded
                        ? TimerExpandedWindowHeight
                        : TimerCompactWindowHeight))
                : (currentIsMedia
                    ? Scale(SideDocked
                        ? SideMusicHeight
                        : MusicHeight)
                    : NotificationTargetHeight(false));
            frostedBackdropDirty = false;
            QueueFrostedBackdropCapture(
                CalculateDockedBounds(
                    screen,
                    targetWidth,
                    targetHeight,
                    dockEdge,
                    dockPosition));
        }

        private void FinishWindowDrag()
        {
            dockScreenDevice =
                Screen.FromPoint(dragLastScreen).DeviceName;
            DockEdge next;
            int nextPosition;
            CalculateDockPlacement(
                dragLastScreen,
                out next,
                out nextPosition);
            dockEdge = next;
            dockPosition = nextPosition;

            draggingWindow = false;
            snappingToDock = true;
            dockSnapBlend = 0.0;
            dockSnapVelocity = 0.0;
            suppressClickUntil = DateTime.UtcNow.AddMilliseconds(320);
            if (currentIsMedia)
            {
                if (mediaState != null && mediaState.IsPlaying)
                {
                    musicPaused = false;
                    musicPauseEndPending = false;
                    musicEnding = false;
                }
                phase = VisualPhase.Music;
                musicControlsVisible = false;
                waveformMotionTarget =
                    musicPaused ? 0.0 : 1.0;
                SetSpringTarget(
                    MusicTargetWidth,
                    Scale(SideDocked
                        ? SideMusicHeight
                        : MusicHeight),
                    0.0);
                if (musicPaused && !musicPinned)
                {
                    musicPauseEndPending = true;
                    QueueAction(BeginMusicEnd, 2500);
                }
            }
            else
            {
                phase = VisualPhase.Detail;
                SetSpringTarget(
                    NotificationTargetWidth(false),
                    NotificationTargetHeight(false),
                    1.0);
                ScheduleNotificationCollapse(5200);
            }
            dockPreview.HidePreview();
            int targetWidth = currentIsMedia
                ? MusicTargetWidth
                : NotificationTargetWidth(false);
            int targetHeight = currentIsMedia
                ? Scale(SideDocked
                    ? SideMusicHeight
                    : MusicHeight)
                : NotificationTargetHeight(false);
            nextFrostedBackdropCapture = DateTime.MinValue;
            QueueFrostedBackdropCapture(
                CalculateDockedBounds(
                    Screen.FromPoint(dragLastScreen),
                    targetWidth,
                    targetHeight,
                    dockEdge,
                    dockPosition));
            StartFrameClock();
            ApplyGeometry();
            RequestRender();
            EventHandler handler = DockEdgeChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static bool TryParseDockEdge(
            string value,
            out DockEdge edge)
        {
            if (string.Equals(value, "bottom", StringComparison.OrdinalIgnoreCase))
            {
                edge = DockEdge.Bottom;
                return true;
            }
            if (string.Equals(value, "left", StringComparison.OrdinalIgnoreCase))
            {
                edge = DockEdge.Left;
                return true;
            }
            if (string.Equals(value, "right", StringComparison.OrdinalIgnoreCase))
            {
                edge = DockEdge.Right;
                return true;
            }
            if (string.Equals(value, "top", StringComparison.OrdinalIgnoreCase))
            {
                edge = DockEdge.Top;
                return true;
            }
            edge = DockEdge.Top;
            return false;
        }

        private void ApplyClickThroughStyle()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }
            const int extendedStyle = -20;
            const int transparentStyle = 0x00000020;
            int style = GetWindowLong(Handle, extendedStyle);
            int next = clickThroughEnabled
                ? style | transparentStyle
                : style & ~transparentStyle;
            if (next != style)
            {
                SetWindowLong(Handle, extendedStyle, next);
                SetWindowPos(
                    Handle,
                    new IntPtr(-1),
                    0,
                    0,
                    0,
                    0,
                    0x0001 | 0x0002 | 0x0010 | 0x0020);
            }
        }

        private void ExpandMusicControls()
        {
            musicControlsVisible = true;
            SetSpringTarget(
                MusicTargetWidth,
                Scale(SideDocked
                    ? SideMusicExpandedHeight
                    : MusicExpandedHeight),
                0.0);
            ResetControlsTimeout();
            RequestRender();
        }

        private void ResetControlsTimeout()
        {
            if (!musicControlsVisible &&
                !(timerExpanded && currentIsTimer))
            {
                return;
            }
            controlsTimer.Change(5000, Timeout.Infinite);
        }

        private void StopControlsTimeout()
        {
            controlsTimer.Change(
                Timeout.Infinite,
                Timeout.Infinite);
        }

        private void OnControlsTimer(object state)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return;
            }
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (timerExpanded && currentIsTimer)
                    {
                        CollapseTimerControls();
                    }
                    if (musicControlsVisible && currentIsMedia)
                    {
                        CollapseMusicControls();
                    }
                }));
            }
            catch
            {
            }
        }

        private void CollapseMusicControls()
        {
            StopControlsTimeout();
            musicControlsVisible = false;
            hoveredMusicButton = -1;
            if (currentIsMedia && current != null &&
                phase == VisualPhase.Music)
            {
                SetSpringTarget(
                    MusicTargetWidth,
                    Scale(SideDocked
                        ? SideMusicHeight
                        : MusicHeight),
                    0.0);
                RequestRender();
            }
            if (musicPauseEndPending && !musicPinned)
            {
                musicPauseEndPending = false;
                QueueAction(BeginMusicEnd, 2500);
            }
        }

        private void CollapseTimerControls()
        {
            StopControlsTimeout();
            if (!timerExpanded || !currentIsTimer)
            {
                return;
            }
            timerExpanded = false;
            SetSpringTarget(
                TimerWindowWidth,
                TimerCompactWindowHeight,
                0.0);
            StartFrameClock();
            RequestRender();
        }

        private int HitTestMusicButton(Point point)
        {
            for (int index = 0; index < 3; index++)
            {
                if (MusicButtonBounds(index).Contains(point))
                {
                    return index;
                }
            }
            return -1;
        }

        private int HitTestTimerButton(Point point)
        {
            if (!currentIsTimer ||
                !timerExpanded ||
                timerSnapshot == null)
            {
                return -1;
            }
            int buttonCount = timerSnapshot.Mode == SmartTimerMode.Stopwatch
                ? 3
                : 2;
            int gap = Scale(8);
            int panelWidth = IsMultiWindowExpanded && !SideDocked
                ? Math.Max(
                    1,
                    (ClientSize.Width - Scale(16)) / 2)
                : ClientSize.Width;
            int left = Scale(18);
            int available =
                panelWidth - Scale(36) - gap * (buttonCount - 1);
            int buttonWidth = Math.Max(1, available / buttonCount);
            int buttonTop = IsMultiWindowExpanded
                ? Scale(120)
                : (SideDocked
                    ? ClientSize.Height - Scale(58)
                    : Scale(92));
            int buttonHeight = SideDocked && !IsMultiWindowExpanded
                ? Scale(42)
                : Scale(44);
            for (int index = 0; index < buttonCount; index++)
            {
                Rectangle bounds = new Rectangle(
                    left + index * (buttonWidth + gap),
                    buttonTop,
                    buttonWidth,
                    buttonHeight);
                if (bounds.Contains(point))
                {
                    return index;
                }
            }
            return -1;
        }

        private int HitTestMultiMusicButton(Point point)
        {
            if (!IsMultiWindowExpanded)
            {
                return -1;
            }
            int gap = Scale(16);
            int panelWidth;
            int rightLeft;
            int buttonTop;
            if (SideDocked)
            {
                panelWidth = ClientSize.Width;
                int panelHeight = Math.Max(
                    1,
                    (ClientSize.Height - gap) / 2);
                rightLeft = 0;
                buttonTop = panelHeight + gap + Scale(120);
            }
            else
            {
                panelWidth = Math.Max(
                    1,
                    (ClientSize.Width - gap) / 2);
                rightLeft = panelWidth + gap;
                buttonTop = Scale(120);
            }
            int buttonGap = Scale(8);
            int padding = Scale(16);
            int width = Math.Max(
                Scale(38),
                (panelWidth - padding * 2 - buttonGap * 2) / 3);
            for (int index = 0; index < 3; index++)
            {
                Rectangle bounds = new Rectangle(
                    rightLeft + padding + index * (width + buttonGap),
                    buttonTop,
                    width,
                    Scale(44));
                if (bounds.Contains(point))
                {
                    return index;
                }
            }
            return -1;
        }

        private Rectangle MultiMusicPanelBounds
        {
            get
            {
                int gap = Scale(16);
                if (SideDocked)
                {
                    int panelHeight = Math.Max(
                        1,
                        (ClientSize.Height - gap) / 2);
                    return new Rectangle(
                        0,
                        panelHeight + gap,
                        ClientSize.Width,
                        Math.Max(
                            1,
                            ClientSize.Height - panelHeight - gap));
                }
                int panelWidth = Math.Max(
                    1,
                    (ClientSize.Width - gap) / 2);
                return new Rectangle(
                    panelWidth + gap,
                    0,
                    Math.Max(1, ClientSize.Width - panelWidth - gap),
                    ClientSize.Height);
            }
        }

        private Rectangle MultiMusicProgressBounds
        {
            get
            {
                Rectangle panel = MultiMusicPanelBounds;
                return new Rectangle(
                    panel.Left + Scale(82),
                    panel.Top + Scale(78),
                    Math.Max(1, panel.Width - Scale(100)),
                    Scale(5));
            }
        }

        private Rectangle MusicButtonBounds(int index)
        {
            int padding = Scale(SideDocked ? 18 : 24);
            int gap = Scale(SideDocked ? 10 : 12);
            int width = Math.Max(
                Scale(42),
                (ClientSize.Width - padding * 2 - gap * 2) / 3);
            int left = padding + index * (width + gap);
            return new Rectangle(
                left,
                Scale(SideDocked ? 176 : 89),
                width,
                Scale(SideDocked ? 62 : 50));
        }

        private Rectangle PlaybackProgressBounds()
        {
            return new Rectangle(
                Scale(24),
                Scale(SideDocked ? 252 : 149),
                Math.Max(1, ClientSize.Width - Scale(48)),
                Scale(8));
        }

        private Rectangle VolumeTrackBounds()
        {
            return new Rectangle(
                Scale(24),
                Scale(SideDocked ? 294 : 188),
                Math.Max(1, ClientSize.Width - Scale(88)),
                Scale(8));
        }

        private Rectangle SoundSettingsBounds()
        {
            return new Rectangle(
                ClientSize.Width - Scale(50),
                Scale(SideDocked ? 282 : 176),
                Scale(30),
                Scale(30));
        }

        public void RefreshLanguage()
        {
            CreateFonts(baseFontDpi * (float)userScale);
            Text = Localization.Get("appName");
            RequestRender();
        }

        private void CreateFonts(float dpi)
        {
            if (microFont != null)
            {
                microFont.Dispose();
                titleFont.Dispose();
                bodyFont.Dispose();
                tinyFont.Dispose();
                timerTimeFont.Dispose();
            }
            string[] fonts = new string[]
            {
                Localization.PreferredFontFamily,
                "Microsoft YaHei UI",
                "Segoe UI"
            };
            microFont = CreateFont(fonts, 7.6f, FontStyle.Regular, dpi);
            titleFont = CreateFont(fonts, 12.0f, FontStyle.Bold, dpi);
            bodyFont = CreateFont(fonts, 10.4f, FontStyle.Regular, dpi);
            tinyFont = CreateFont(fonts, 8.6f, FontStyle.Regular, dpi);
            timerTimeFont = CreateFont(fonts, 18.0f, FontStyle.Bold, dpi);
        }

        private static Font CreateFont(
            string[] candidates,
            float size,
            FontStyle style,
            float dpi)
        {
            float pixelSize = Math.Max(6f, size * dpi / 72f);
            try
            {
                return RuntimeAssets.CreateUiFont(pixelSize, style);
            }
            catch
            {
            }
            for (int index = 0; index < candidates.Length; index++)
            {
                try
                {
                    Font font = new Font(
                        candidates[index],
                        pixelSize,
                        style,
                        GraphicsUnit.Pixel);
                    if (string.Equals(
                        font.Name,
                        candidates[index],
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return font;
                    }
                    font.Dispose();
                }
                catch
                {
                }
            }
            return new Font(
                FontFamily.GenericMonospace,
                pixelSize,
                style,
                GraphicsUnit.Pixel);
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            public int Width;
            public int Height;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint ImageSize;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        private delegate void WinEventDelegate(
            IntPtr hook,
            uint eventType,
            IntPtr window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr module,
            WinEventDelegate callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(
            IntPtr window,
            uint affinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(
            IntPtr window,
            int index);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(
            IntPtr window,
            int index,
            int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint milliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint timeEndPeriod(uint milliseconds);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(
            IntPtr window,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(
            IntPtr window,
            IntPtr destinationDc,
            ref NativePoint destination,
            ref NativeSize size,
            IntPtr sourceDc,
            ref NativePoint source,
            int colorKey,
            ref BlendFunction blend,
            int flags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(
            IntPtr dc,
            ref BitmapInfo bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                StopFrameClock();
                sequenceTimer.Dispose();
                controlsTimer.Dispose();
                if (desktopChangeHook != IntPtr.Zero)
                {
                    UnhookWinEvent(desktopChangeHook);
                    desktopChangeHook = IntPtr.Zero;
                }
                mouseHook.Dispose();
                dockPreview.Dispose();
                microFont.Dispose();
                titleFont.Dispose();
                bodyFont.Dispose();
                tinyFont.Dispose();
                timerTimeFont.Dispose();
                if (albumArtwork != null)
                {
                    albumArtwork.Dispose();
                    albumArtwork = null;
                }
                if (frostedNoiseBrush != null)
                {
                    frostedNoiseBrush.Dispose();
                    frostedNoiseBrush = null;
                }
                if (frostedNoiseTile != null)
                {
                    frostedNoiseTile.Dispose();
                    frostedNoiseTile = null;
                }
                lock (frostedBackdropSync)
                {
                    if (frostedBackdrop != null)
                    {
                        frostedBackdrop.Dispose();
                        frostedBackdrop = null;
                    }
                }
                if (frostedShellCache != null)
                {
                    frostedShellCache.Dispose();
                    frostedShellCache = null;
                }
                ReleaseLayeredSurface();
                frameStopEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class DockPreviewForm : Form
    {
        private int cornerRadius = 24;

        public DockPreviewForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(90, 210, 236);
            Opacity = 0.24;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000;
                parameters.ExStyle |= 0x00000080;
                parameters.ExStyle |= 0x00080000;
                parameters.ExStyle |= 0x00000020;
                return parameters;
            }
        }

        public void ShowPreview(Rectangle bounds, int radius)
        {
            if (IsDisposed)
            {
                return;
            }
            cornerRadius = Math.Max(2, radius);
            if (!Visible)
            {
                Show();
            }
            if (Bounds != bounds)
            {
                Bounds = bounds;
                using (GraphicsPath path = CreateRoundedPath(
                    new Rectangle(0, 0, bounds.Width, bounds.Height),
                    cornerRadius))
                {
                    Region previous = Region;
                    Region = new Region(path);
                    if (previous != null)
                    {
                        previous.Dispose();
                    }
                }
            }
            Invalidate();
        }

        public void HidePreview()
        {
            if (!IsDisposed && Visible)
            {
                Hide();
            }
        }

        private static GraphicsPath CreateRoundedPath(
            Rectangle bounds,
            int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            GraphicsPath path = new GraphicsPath();
            Rectangle arc = new Rectangle(
                bounds.Location,
                new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(
                1,
                1,
                Math.Max(1, ClientSize.Width - 2),
                Math.Max(1, ClientSize.Height - 2));
            using (GraphicsPath path = CreateRoundedPath(
                bounds,
                cornerRadius))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(70, 220, 244)))
            using (Pen border = new Pen(
                Color.FromArgb(230, 245, 255),
                2f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
        }
    }
}
