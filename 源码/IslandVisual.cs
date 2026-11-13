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
        private const int MusicExpandedHeight = 152;

        private enum VisualPhase
        {
            Hidden,
            Seed,
            Compact,
            Detail,
            Music,
            Collapsing
        }

        private readonly Queue<NotificationItem> queue =
            new Queue<NotificationItem>();
        private readonly System.Threading.Timer sequenceTimer;
        private readonly System.Threading.Timer controlsTimer;
        private readonly object sequenceSync = new object();
        private readonly GlobalMouseHook mouseHook;
        private Font microFont;
        private Font titleFont;
        private Font bodyFont;
        private Font tinyFont;
        private readonly ManualResetEvent frameStopEvent =
            new ManualResetEvent(false);

        public event EventHandler<MediaCommandAction> MediaCommandRequested;
        public event EventHandler MusicWindowCloseRequested;

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
        private Bitmap layeredBitmap;
        private Graphics layeredGraphics;
        private bool renderingLayered;
        private bool hovering;
        private bool paused;
        private double userScale = 1.0;
        private float baseFontDpi;
        private bool glassModeEnabled;
        private double glassBlend;
        private double glassBlendVelocity;
        private double glassBlendTarget;
        private bool qualityEnhancementEnabled;
        private double qualityBlend;
        private double qualityBlendVelocity;
        private double qualityBlendTarget;
        private double qualityParticleSeconds;
        private double collapseDissolveProgress;
        private bool musicModeEnabled;
        private bool musicPinned;
        private bool currentIsMedia;
        private bool musicDismissed;
        private bool musicEnding;
        private bool musicPaused;
        private bool musicPauseEndPending;
        private bool musicControlsVisible;
        private int hoveredMusicButton = -1;
        private MediaPlaybackState mediaState;
        private Image albumArtwork;
        private string albumArtworkPath;
        private DateTime albumArtworkStamp;
        private Color musicAccent = Color.FromArgb(240, 218, 151);
        private double waveformMotion = 1.0;
        private double waveformMotionVelocity;
        private double waveformMotionTarget = 1.0;
        private double waveformSeconds;
        private Action queuedAction;
        private int sequenceGeneration;

        private const uint FrameMessage =
            0x8000 + 0x173;
        private const double TargetFrameRate = 165.0;

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
            Width = Scale(SeedSize);
            Height = Scale(SeedSize);
            Cursor = Cursors.Hand;
            Opacity = 1.0;

            float fontDpi;
            using (Graphics graphics = CreateGraphics())
            {
                fontDpi = graphics.DpiX;
            }
            baseFontDpi = fontDpi;
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

            MouseEnter += delegate
            {
                hovering = true;
                RequestRender();
            };
            MouseLeave += delegate
            {
                hovering = false;
                RequestRender();
            };
            MouseClick += OnIslandClick;
            MouseDoubleClick += OnIslandDoubleClick;
            MouseMove += OnIslandMouseMove;
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
                    Scale(MusicWidth),
                    Scale(musicControlsVisible
                        ? MusicExpandedHeight
                        : MusicHeight),
                    0.0);
            }
            else
            {
                int width = phase == VisualPhase.Detail
                    ? DetailWidth
                    : (phase == VisualPhase.Compact
                        ? CompactWidth
                        : SeedSize);
                int height = phase == VisualPhase.Detail
                    ? DetailHeight
                    : (phase == VisualPhase.Compact
                        ? CompactHeight
                        : SeedSize);
                SetSpringTarget(Scale(width), Scale(height), detailBlendTarget);
            }
            RequestRender();
        }

        public void SetGlassMode(bool enabled)
        {
            glassModeEnabled = enabled;
            glassBlendTarget = enabled ? 1.0 : 0.0;
            qualityBlendTarget =
                enabled && qualityEnhancementEnabled ? 1.0 : 0.0;
            if (current == null || phase == VisualPhase.Hidden)
            {
                glassBlend = glassBlendTarget;
                glassBlendVelocity = 0.0;
                qualityBlend = qualityBlendTarget;
                qualityBlendVelocity = 0.0;
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
                musicPaused = false;
                musicPauseEndPending = false;
                waveformMotionTarget = 1.0;
                CancelQueuedAction();
                if (phase == VisualPhase.Collapsing)
                {
                    musicEnding = false;
                    phase = VisualPhase.Music;
                    SetSpringTarget(
                        Scale(MusicWidth),
                        Scale(musicControlsVisible
                            ? MusicExpandedHeight
                            : MusicHeight),
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
                Scale(MusicWidth),
                Scale(musicControlsVisible
                    ? MusicExpandedHeight
                    : MusicHeight),
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
                Scale(CompactWidth),
                Scale(CompactHeight),
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
                Scale(DetailWidth),
                Scale(DetailHeight),
                1.0);
            QueueAction(BeginCollapse, 5200);
        }

        private void BeginCollapse()
        {
            if (current == null)
            {
                return;
            }
            if (hovering)
            {
                QueueAction(BeginCollapse, 1200);
                return;
            }

            CollapseNow();
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
            if (currentIsMedia)
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
            if (frameClockRunning)
            {
                return;
            }

            frameStopEvent.Reset();
            lastFrameTimestamp = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref frameMessagePending, 0);
            frameClockRunning = true;
            frameThread = new Thread(FrameLoop);
            frameThread.IsBackground = true;
            frameThread.Priority = ThreadPriority.AboveNormal;
            frameThread.Name = "SmartWindow.165Hz";
            frameThread.Start();
        }

        private void StopFrameClock()
        {
            frameClockRunning = false;
            frameStopEvent.Set();
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
                long frameTicks = Math.Max(
                    1L,
                    (long)(Stopwatch.Frequency / TargetFrameRate));
                long nextFrame = Stopwatch.GetTimestamp();
                while (frameClockRunning)
                {
                    DwmFlush();
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
            }
            detailBlend = Clamp01(detailBlend);
            waveformMotion = Clamp01(waveformMotion);
            glassBlend = Clamp01(glassBlend);
            qualityBlend = Clamp01(qualityBlend);
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
            if (currentIsMedia &&
                !musicPaused &&
                !musicEnding &&
                phase == VisualPhase.Music)
            {
                waveformSeconds += elapsed;
            }

            int nextWidth = Math.Max(1, (int)Math.Round(animatedWidth));
            int nextHeight = Math.Max(1, (int)Math.Round(animatedHeight));
            bool geometryChanged =
                nextWidth != currentWidth ||
                nextHeight != currentHeight;
            currentWidth = nextWidth;
            currentHeight = nextHeight;
            if (geometryChanged)
            {
                ApplyGeometry();
            }
            else
            {
                RequestRender();
            }
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
                return;
            }

            Point localPoint = PointToClient(new Point(screenX, screenY));
            if (!ClientRectangle.Contains(localPoint))
            {
                CollapseMusicControls();
            }
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
            Screen screen = Screen.PrimaryScreen;
            Rectangle bounds = screen.Bounds;
            int left = bounds.Left + (bounds.Width - currentWidth) / 2;
            int top = bounds.Top + Scale(7);
            if (Bounds.Left != left ||
                Bounds.Top != top ||
                Bounds.Width != currentWidth ||
                Bounds.Height != currentHeight)
            {
                SetBounds(left, top, currentWidth, currentHeight);
            }
            RequestRender();
        }

        private int CalculateCornerRadius()
        {
            int radius = Math.Max(1, currentHeight / 2);
            if (currentIsMedia && phase == VisualPhase.Music)
            {
                double expansion = Clamp01(
                    (currentHeight - Scale(MusicHeight)) /
                    (double)Math.Max(
                        1,
                        Scale(MusicExpandedHeight) - Scale(MusicHeight)));
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
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr previousBitmap = IntPtr.Zero;
            renderingLayered = true;
            try
            {
                if (layeredBitmap == null ||
                    layeredBitmap.Width != width ||
                    layeredBitmap.Height != height)
                {
                    if (layeredGraphics != null)
                    {
                        layeredGraphics.Dispose();
                        layeredGraphics = null;
                    }
                    if (layeredBitmap != null)
                    {
                        layeredBitmap.Dispose();
                    }
                    layeredBitmap = new Bitmap(
                        width,
                        height,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    int dpi = Math.Max(96, DeviceDpi);
                    layeredBitmap.SetResolution(dpi, dpi);
                    layeredGraphics = Graphics.FromImage(layeredBitmap);
                }

                layeredGraphics.Clear(Color.Transparent);
                PaintSurface(layeredGraphics);

                screenDc = GetDC(IntPtr.Zero);
                memoryDc = CreateCompatibleDC(screenDc);
                bitmapHandle = layeredBitmap.GetHbitmap(
                    Color.FromArgb(0, 0, 0, 0));
                previousBitmap = SelectObject(memoryDc, bitmapHandle);

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
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    2);
            }
            catch (Exception exception)
            {
                Logger.Write("RenderLayeredFrame: " + exception);
            }
            finally
            {
                if (memoryDc != IntPtr.Zero && previousBitmap != IntPtr.Zero)
                {
                    SelectObject(memoryDc, previousBitmap);
                }
                if (bitmapHandle != IntPtr.Zero)
                {
                    DeleteObject(bitmapHandle);
                }
                if (memoryDc != IntPtr.Zero)
                {
                    DeleteDC(memoryDc);
                }
                if (screenDc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
                renderingLayered = false;
            }
        }

        private int Scale(int logicalPixels)
        {
            using (Graphics graphics = CreateGraphics())
            {
                return (int)Math.Round(
                    logicalPixels *
                    graphics.DpiX /
                    96f *
                    userScale);
            }
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
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Rectangle outer = new Rectangle(
                0,
                0,
                Math.Max(1, ClientSize.Width - 1),
                Math.Max(1, ClientSize.Height - 1));
            int radius = CalculateCornerRadius();
            using (GraphicsPath path = RoundedRectangle(outer, radius))
            {
                Color solidTop = Color.FromArgb(255, 26, 26, 29);
                Color solidBottom = Color.FromArgb(255, 9, 9, 11);
                Color glassTop = Color.FromArgb(178, 45, 52, 64);
                Color glassBottom = Color.FromArgb(126, 14, 18, 25);
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

                using (Pen border = new Pen(
                    Color.FromArgb(
                        (int)Math.Round(94 + 50 * glassBlend),
                        185,
                        190,
                        184),
                    1f))
                {
                    graphics.DrawPath(border, path);
                }
                DrawGlassSheen(graphics, path, outer);
                DrawQualityGlass(graphics, path, outer, radius);
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
                DrawMusic(graphics);
            }
            else if (phase == VisualPhase.Collapsing)
            {
                DrawCollapsing(graphics);
            }
            else
            {
                DrawExpanded(graphics);
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
            using (GraphicsPath innerPath = RoundedRectangle(inner, innerRadius))
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
            if (string.IsNullOrWhiteSpace(path) ||
                !System.IO.File.Exists(path))
            {
                if (albumArtwork != null)
                {
                    albumArtwork.Dispose();
                    albumArtwork = null;
                }
                musicAccent = Color.FromArgb(240, 218, 151);
                albumArtworkPath = "";
                albumArtworkStamp = DateTime.MinValue;
                return null;
            }

            DateTime stamp;
            try
            {
                stamp = System.IO.File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return albumArtwork;
            }
            if (albumArtwork != null &&
                string.Equals(
                    albumArtworkPath,
                    path,
                    StringComparison.OrdinalIgnoreCase) &&
                stamp == albumArtworkStamp)
            {
                return albumArtwork;
            }

            try
            {
                using (System.IO.FileStream stream = new System.IO.FileStream(
                    path,
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.Read,
                    System.IO.FileShare.ReadWrite | System.IO.FileShare.Delete))
                using (Image source = Image.FromStream(stream))
                {
                    Bitmap copy = new Bitmap(source);
                    if (albumArtwork != null)
                    {
                        albumArtwork.Dispose();
                    }
                    albumArtwork = copy;
                    albumArtworkPath = path;
                    albumArtworkStamp = stamp;
                    musicAccent = ExtractMusicAccent(copy);
                }
            }
            catch
            {
                return albumArtwork;
            }
            return albumArtwork;
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

            for (int y = 0; y < bitmap.Height; y += stepY)
            {
                for (int x = 0; x < bitmap.Width; x += stepX)
                {
                    Color color = bitmap.GetPixel(x, y);
                    if (color.A < 128)
                    {
                        continue;
                    }

                    int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                    int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
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
                        color.GetHue() / 360.0 * bucketCount) % bucketCount;
                    double weight =
                        value * 0.45 +
                        saturation * 1.25;
                    weights[bucket] += weight;
                    red[bucket] += color.R * weight;
                    green[bucket] += color.G * weight;
                    blue[bucket] += color.B * weight;
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
            if (currentIsMedia && current != null &&
                phase == VisualPhase.Music)
            {
                if (!musicControlsVisible)
                {
                    ExpandMusicControls();
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

        private void OnIslandMouseMove(object sender, MouseEventArgs e)
        {
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
                    : Cursors.Default;
                RequestRender();
            }
        }

        private void ExpandMusicControls()
        {
            musicControlsVisible = true;
            SetSpringTarget(
                Scale(MusicWidth),
                Scale(MusicExpandedHeight),
                0.0);
            ResetControlsTimeout();
            RequestRender();
        }

        private void ResetControlsTimeout()
        {
            if (!musicControlsVisible)
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
                BeginInvoke(new Action(CollapseMusicControls));
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
                    Scale(MusicWidth),
                    Scale(MusicHeight),
                    0.0);
                RequestRender();
            }
            if (musicPauseEndPending && !musicPinned)
            {
                musicPauseEndPending = false;
                QueueAction(BeginMusicEnd, 2500);
            }
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

        private Rectangle MusicButtonBounds(int index)
        {
            int padding = Scale(24);
            int gap = Scale(12);
            int width = Math.Max(
                Scale(42),
                (ClientSize.Width - padding * 2 - gap * 2) / 3);
            int left = padding + index * (width + gap);
            return new Rectangle(
                left,
                Scale(89),
                width,
                Scale(50));
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

        [DllImport("dwmapi.dll")]
        private static extern int DwmFlush();

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
        private static extern IntPtr SelectObject(IntPtr dc, IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr graphicsObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr dc);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopFrameClock();
                sequenceTimer.Dispose();
                controlsTimer.Dispose();
                mouseHook.Dispose();
                microFont.Dispose();
                titleFont.Dispose();
                bodyFont.Dispose();
                tinyFont.Dispose();
                if (albumArtwork != null)
                {
                    albumArtwork.Dispose();
                    albumArtwork = null;
                }
                if (layeredGraphics != null)
                {
                    layeredGraphics.Dispose();
                    layeredGraphics = null;
                }
                if (layeredBitmap != null)
                {
                    layeredBitmap.Dispose();
                    layeredBitmap = null;
                }
                frameStopEvent.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
