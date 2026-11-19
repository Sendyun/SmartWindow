using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartWindow
{
    internal sealed class ConsoleForm : Form
    {
        private const int LogicalWidth = 1000;
        private const int LogicalHeight = 720;
        private const int HeaderHeight = 92;
        private const string DisplayVersion = "Ver 1.2.0";

        private readonly Bitmap logoLight;
        private readonly Bitmap logoDark;
        private readonly Bitmap githubLogo;
        private readonly Timer renderTimer;
        private readonly Timer hoverRenderTimer;
        private readonly Timer logoTimer;
        private readonly Timer logoTransitionTimer;
        private readonly Timer installationMarqueeTimer;
        private readonly Timer performanceTimer;
        private readonly Timer timerWheelTimer;
        private readonly List<float> performanceHistory =
            new List<float>();
        private Font titleFont;
        private Font sectionFont;
        private Font bodyFont;
        private Font smallFont;
        private Font microFont;
        private Font performanceFont;
        private float dpiScale;
        private Bitmap surface;
        private Graphics surfaceGraphics;
        private bool authorPage;
        private bool performancePage;
        private bool timerPage;
        private bool languageMenuOpen;
        private bool draggingSizeSlider;
        private bool draggingOpacitySlider;
        private bool draggingFrostedSlider;
        private bool draggingScrollbar;
        private string hoverZone = "";
        private double contentScroll;
        private bool paused;
        private bool musicEnabled;
        private bool musicPersistent;
        private bool transparentWindow;
        private bool qualityEnhancement;
        private bool frostedGlassEnabled;
        private bool autoStart;
        private bool movementEnabled;
        private bool clickThroughEnabled;
        private double sizeScale = 1.0;
        private double consoleOpacity = 0.5;
        private double frostedIntensity = 0.55;
        private int frameRateLimit = 120;
        private string runtimeStatus = Localization.Get("statusRunning");
        private bool allowClose;
        private bool rendering;
        private bool renderPending;
        private bool logoDarkMode;
        private double logoBlend;
        private double logoBlendTarget;
        private DateTime installationDate = DateTime.Today;
        private int installationDays = 1;
        private string installationDayText = "";
        private int installationTextWidth;
        private bool installationTextNeedsScroll;
        private double installationMarqueeOffset;
        private Func<PerformanceSnapshot> performanceProvider;
        private PerformanceSnapshot performanceSnapshot;
        private TimerSnapshot timerSnapshot;
        private TimerPreset[] timerPresets = new TimerPreset[0];
        private string timerSoundPath = "";
        private Color timerAccentColor = Color.FromArgb(100, 181, 246);
        private Bitmap timerColorWheel;
        private int timerColorWheelSize;
        private double timerHue;
        private double timerSaturation = 0.62;
        private double timerValue = 1.0;
        private bool draggingTimerWheel;
        private bool draggingTimerBrightness;
        private int draggingTimerColumn = -1;
        private int timerDragStartY;
        private int timerDragStartValue;
        private int timerHours;
        private int timerMinutes = 10;
        private int timerSeconds;
        private double timerLapScroll;
        private int timerLapScrollCount;
        private readonly double[] timerWheelValues =
            new double[] { 0.0, 10.0, 0.0 };
        private readonly double[] timerWheelVelocities =
            new double[3];
        private SmartTimerMode timerSetupMode = SmartTimerMode.Countdown;
        private DateTime nextTimerPageRender = DateTime.MinValue;

        public event EventHandler PauseToggleRequested;
        public event EventHandler MusicToggleRequested;
        public event EventHandler PersistentToggleRequested;
        public event EventHandler GlassModeToggleRequested;
        public event EventHandler QualityEnhanceToggleRequested;
        public event EventHandler FrostedGlassToggleRequested;
        public event EventHandler AutoStartToggleRequested;
        public event EventHandler WindowMovementToggleRequested;
        public event EventHandler ClickThroughToggleRequested;
        public event EventHandler TestNotificationRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler NotificationHistoryRequested;
        public event EventHandler AppFiltersRequested;
        public event EventHandler ExportSettingsRequested;
        public event EventHandler ImportSettingsRequested;
        public event EventHandler ResetSettingsRequested;
        public event EventHandler SponsorRequested;
        public event EventHandler ExitRequested;
        public event EventHandler<UiLanguage> LanguageChangeRequested;
        public event EventHandler<double> SizeScaleChanged;
        public event EventHandler<double> ConsoleOpacityChanged;
        public event EventHandler<double> FrostedIntensityChanged;
        public event EventHandler<int> FrameRateLimitChanged;
        public event EventHandler<TimerStartRequest> TimerStartRequested;
        public event EventHandler<SmartTimerMode> TimerPauseToggleRequested;
        public event EventHandler<SmartTimerMode> TimerCancelRequested;
        public event EventHandler<SmartTimerMode> TimerLapRequested;
        public event EventHandler<TimerPreset> TimerPresetAddRequested;
        public event EventHandler<int> TimerPresetDeleteRequested;
        public event EventHandler<Color> TimerColorChanged;
        public event EventHandler TimerSoundBrowseRequested;
        public event EventHandler TimerSoundPreviewRequested;
        public event EventHandler TimerSoundResetRequested;

        public ConsoleForm()
        {
            Text = Localization.Get("consoleTitle");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            MaximizeBox = false;
            MinimizeBox = true;
            BackColor = Color.FromArgb(9, 11, 15);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            dpiScale = 1f;
            Width = LogicalWidth;
            Height = LogicalHeight;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);

            logoLight = RuntimeAssets.LoadLogoLight();
            logoDark = RuntimeAssets.LoadLogoDark();
            githubLogo = RuntimeAssets.LoadGithubLogo();
            CreateFonts();

            renderTimer = new Timer();
            renderTimer.Interval = 16;
            renderTimer.Tick += delegate
            {
                renderTimer.Stop();
                if (renderPending)
                {
                    renderPending = false;
                    RenderSurface();
                }
            };
            hoverRenderTimer = new Timer();
            hoverRenderTimer.Interval = 45;
            hoverRenderTimer.Tick += delegate
            {
                hoverRenderTimer.Stop();
                RequestRender();
            };
            logoTimer = new Timer();
            logoTimer.Interval = 3200;
            logoTimer.Tick += delegate
            {
                logoDarkMode = !logoDarkMode;
                logoBlendTarget = logoDarkMode ? 1.0 : 0.0;
                logoTransitionTimer.Stop();
                logoTransitionTimer.Start();
                RequestRender();
            };
            logoTimer.Start();
            logoTransitionTimer = new Timer();
            logoTransitionTimer.Interval = 16;
            logoTransitionTimer.Tick += delegate
            {
                double difference = logoBlendTarget - logoBlend;
                if (Math.Abs(difference) < 0.002)
                {
                    logoBlend = logoBlendTarget;
                    logoTransitionTimer.Stop();
                }
                else
                {
                    logoBlend += difference * 0.09;
                }
                RequestRender();
            };
            installationMarqueeTimer = new Timer();
            installationMarqueeTimer.Interval = 60;
            installationMarqueeTimer.Tick += delegate
            {
                if (!Visible || !installationTextNeedsScroll)
                {
                    installationMarqueeTimer.Stop();
                    return;
                }
                installationMarqueeOffset += 1.8;
                double cycle = installationTextWidth + 30.0;
                if (installationMarqueeOffset >= cycle)
                {
                    installationMarqueeOffset = 0.0;
                }
                RequestRender();
            };
            performanceTimer = new Timer();
            performanceTimer.Interval = 500;
            performanceTimer.Tick += delegate
            {
                UpdatePerformance();
            };
            timerWheelTimer = new Timer();
            timerWheelTimer.Interval = 16;
            timerWheelTimer.Tick += delegate
            {
                AdvanceTimerWheelAnimation();
            };

            MouseMove += OnConsoleMouseMove;
            MouseLeave += OnConsoleMouseLeave;
            MouseDown += OnConsoleMouseDown;
            MouseUp += OnConsoleMouseUp;
            MouseClick += OnConsoleMouseClick;
            MouseWheel += OnConsoleMouseWheel;
        }

        public void SetPerformanceProvider(
            Func<PerformanceSnapshot> provider)
        {
            performanceProvider = provider;
        }

        public void SetTimerState(
            TimerSnapshot snapshot,
            TimerPreset[] presets,
            string soundPath)
        {
            bool activeChanged =
                (timerSnapshot == null) != (snapshot == null) ||
                (timerSnapshot != null &&
                 snapshot != null &&
                 timerSnapshot.IsActive != snapshot.IsActive);
            timerSnapshot = snapshot;
            int lapCount = snapshot == null ? 0 : snapshot.LapCount;
            if (lapCount != timerLapScrollCount)
            {
                timerLapScrollCount = lapCount;
                timerLapScroll = 0.0;
            }
            timerPresets = presets ?? new TimerPreset[0];
            timerSoundPath = soundPath ?? "";
            if (snapshot != null &&
                snapshot.AccentColor.A > 0)
            {
                timerAccentColor = snapshot.AccentColor;
                double hue;
                double saturation;
                double value;
                ColorToHsv(
                    timerAccentColor,
                    out hue,
                    out saturation,
                    out value);
                timerHue = hue;
                timerSaturation = saturation;
                timerValue = value;
            }
            bool shouldRender =
                timerPage ||
                activeChanged ||
                (snapshot != null &&
                 snapshot.IsActive &&
                 DateTime.UtcNow >= nextTimerPageRender);
            if (Visible && shouldRender)
            {
                nextTimerPageRender = DateTime.UtcNow.AddMilliseconds(
                    timerPage ? 180 : 500);
                RequestRender();
            }
        }

        private void UpdatePerformance()
        {
            if (!Visible ||
                performanceProvider == null)
            {
                performanceTimer.Stop();
                return;
            }

            try
            {
                PerformanceSnapshot next = performanceProvider();
                if (next == null)
                {
                    return;
                }
                performanceSnapshot = next;
                if (performancePage)
                {
                    performanceHistory.Add((float)next.CurrentFps);
                    if (performanceHistory.Count > 120)
                    {
                        performanceHistory.RemoveAt(0);
                    }
                }
                RequestRender();
            }
            catch (Exception exception)
            {
                Logger.Write("UpdatePerformance: " + exception);
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                performanceTimer.Interval = performancePage ? 350 : 500;
                performanceTimer.Start();
                UpdatePerformance();
            }
            else
            {
                performanceTimer.Stop();
            }
        }

        public void SetState(
            bool isPaused,
            bool isMusicEnabled,
            bool isMusicPersistent,
            bool isTransparent,
            bool isQualityEnhanced,
            bool isFrostedGlassEnabled,
            double frostedAmount,
            bool isAutoStart,
            bool isMovementEnabled,
            bool isClickThrough,
            string status,
            double scale,
            double opacity,
            int maximumFrameRate,
            DateTime firstInstallDate)
        {
            paused = isPaused;
            musicEnabled = isMusicEnabled;
            musicPersistent = isMusicPersistent;
            transparentWindow = isTransparent;
            qualityEnhancement = isQualityEnhanced;
            frostedGlassEnabled = isFrostedGlassEnabled;
            frostedIntensity = Math.Max(0.0, Math.Min(1.0, frostedAmount));
            autoStart = isAutoStart;
            movementEnabled = isMovementEnabled;
            clickThroughEnabled = isClickThrough;
            sizeScale = Math.Max(0.80, Math.Min(1.20, scale));
            consoleOpacity = Math.Max(0.0, Math.Min(1.0, opacity));
            frameRateLimit = Math.Max(
                60,
                Math.Min(165, maximumFrameRate));
            installationDate = firstInstallDate.Date;
            installationDays = Math.Max(
                1,
                (DateTime.Today - installationDate).Days + 1);
            UpdateInstallationDayText();
            if (!string.IsNullOrWhiteSpace(status))
            {
                runtimeStatus = status;
            }
            RequestRender();
        }

        public void ShowConsole()
        {
            if (IsDisposed)
            {
                return;
            }
            if (!Visible)
            {
                Show();
            }
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            Activate();
            BringToFront();
            if (installationTextNeedsScroll)
            {
                installationMarqueeTimer.Start();
            }
            performanceTimer.Interval = performancePage ? 350 : 500;
            performanceTimer.Start();
            UpdatePerformance();
            RequestRender();
        }

        public void RefreshLocalization()
        {
            CreateFonts();
            Text = Localization.Get("consoleTitle");
            UpdateInstallationDayText();
            RequestRender();
        }

        public void CloseForExit()
        {
            allowClose = true;
            Close();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0201) // WM_LBUTTONDOWN
            {
                int packed = message.LParam.ToInt32();
                int x = (short)(packed & 0xFFFF);
                int y = (short)((packed >> 16) & 0xFFFF);
                string zone = HitZone(ToLogical(new Point(x, y)));
                if (zone == "sponsor")
                {
                    BeginInvoke(new Action(delegate
                    {
                        Raise(SponsorRequested);
                    }));
                    return;
                }
            }
            base.WndProc(ref message);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyBlur();
            if (installationTextNeedsScroll)
            {
                installationMarqueeTimer.Start();
            }
            FlushRender();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            dpiScale = Math.Max(1f, GetDpiForWindow(Handle) / 96f);
            Width = (int)Math.Round(LogicalWidth * dpiScale);
            Height = (int)Math.Round(LogicalHeight * dpiScale);
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            RecreateSurface();
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            dpiScale = Math.Max(1f, e.DeviceDpiNew / 96f);
            Width = (int)Math.Round(LogicalWidth * dpiScale);
            Height = (int)Math.Round(LogicalHeight * dpiScale);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
            RecreateSurface();
            FlushRender();
        }

        private void RecreateSurface()
        {
            if (surfaceGraphics != null)
            {
                surfaceGraphics.Dispose();
                surfaceGraphics = null;
            }
            if (surface != null)
            {
                surface.Dispose();
            }
            surface = new Bitmap(
                Math.Max(1, Width),
                Math.Max(1, Height),
                PixelFormat.Format32bppArgb);
            surfaceGraphics = Graphics.FromImage(surface);
            surfaceGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            surfaceGraphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;
            surfaceGraphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                installationMarqueeTimer.Stop();
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // UpdateLayeredWindow paints the finished ARGB surface directly.
            // Repainting here would enqueue another full-surface render and can
            // create a feedback loop that pins a CPU core.
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        private void RequestRender()
        {
            if (IsDisposed || !IsHandleCreated || !Visible)
            {
                return;
            }
            renderPending = true;
            if (!renderTimer.Enabled)
            {
                renderTimer.Start();
            }
        }

        private void FlushRender()
        {
            if (IsDisposed || !IsHandleCreated || !Visible)
            {
                return;
            }
            renderTimer.Stop();
            renderPending = false;
            RenderSurface();
        }

        private void RenderSurface()
        {
            if (rendering || IsDisposed)
            {
                return;
            }
            if (surfaceGraphics == null ||
                surface == null ||
                surface.Width != Width ||
                surface.Height != Height)
            {
                RecreateSurface();
            }
            rendering = true;
            IntPtr screenDc = IntPtr.Zero;
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmapHandle = IntPtr.Zero;
            IntPtr previousBitmap = IntPtr.Zero;
            try
            {
                surfaceGraphics.Clear(Color.Transparent);
                surfaceGraphics.ResetTransform();
                surfaceGraphics.ScaleTransform(dpiScale, dpiScale);
                PaintConsole(surfaceGraphics);

                screenDc = GetDC(IntPtr.Zero);
                memoryDc = CreateCompatibleDC(screenDc);
                bitmapHandle = surface.GetHbitmap(Color.FromArgb(0, 0, 0, 0));
                previousBitmap = SelectObject(memoryDc, bitmapHandle);

                NativePoint destination = new NativePoint();
                destination.X = Left;
                destination.Y = Top;
                NativeSize size = new NativeSize();
                size.Width = Width;
                size.Height = Height;
                NativePoint source = new NativePoint();
                BlendFunction blend = new BlendFunction();
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
                Logger.Write("Console render: " + exception);
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
                rendering = false;
            }
        }

        private void PaintConsole(Graphics graphics)
        {
            Rectangle shell = new Rectangle(
                14,
                14,
                972,
                LogicalHeight - 28);
            Color topColor;
            Color bottomColor;
            GetConsoleBackgroundColors(out topColor, out bottomColor);
            using (GraphicsPath shellPath = RoundedRectangle(shell, 24))
            {
                using (LinearGradientBrush glass =
                    new LinearGradientBrush(
                        shell,
                        topColor,
                        bottomColor,
                        115f))
                {
                    graphics.FillPath(glass, shellPath);
                }
                using (Pen border = new Pen(
                    Color.FromArgb(65, 255, 255, 255),
                    1f))
                {
                    graphics.DrawPath(border, shellPath);
                }
            }

            DrawHeader(graphics);
            DrawSidebar(graphics);
            Rectangle contentViewport = new Rectangle(234, 104, 720, 598);
            GraphicsState contentState = graphics.Save();
            graphics.SetClip(contentViewport);
            graphics.TranslateTransform(0f, -(float)contentScroll);
            if (authorPage)
            {
                DrawAuthorPage(graphics);
            }
            else if (performancePage)
            {
                DrawPerformancePage(graphics);
            }
            else if (timerPage)
            {
                DrawTimerPage(graphics);
            }
            else
            {
                DrawDashboard(graphics);
            }
            graphics.Restore(contentState);
            if (!performancePage)
            {
                DrawScrollbar(graphics);
            }
            if (languageMenuOpen)
            {
                DrawLanguageMenu(graphics);
            }
        }

        private void GetConsoleBackgroundColors(
            out Color top,
            out Color bottom)
        {
            Color blackTop = Color.FromArgb(255, 0, 0, 0);
            Color blackBottom = Color.FromArgb(255, 0, 0, 0);
            Color glassTop = Color.FromArgb(226, 18, 22, 30);
            Color glassBottom = Color.FromArgb(239, 7, 9, 13);
            Color airyTop = Color.FromArgb(104, 32, 38, 48);
            Color airyBottom = Color.FromArgb(118, 3, 6, 11);
            if (consoleOpacity <= 0.5)
            {
                double amount = consoleOpacity * 2.0;
                top = BlendColor(blackTop, glassTop, amount);
                bottom = BlendColor(blackBottom, glassBottom, amount);
            }
            else
            {
                double amount = (consoleOpacity - 0.5) * 2.0;
                top = BlendColor(glassTop, airyTop, amount);
                bottom = BlendColor(glassBottom, airyBottom, amount);
            }
        }

        private static Color BlendColor(Color first, Color second, double amount)
        {
            double value = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromArgb(
                (int)Math.Round(first.A + (second.A - first.A) * value),
                (int)Math.Round(first.R + (second.R - first.R) * value),
                (int)Math.Round(first.G + (second.G - first.G) * value),
                (int)Math.Round(first.B + (second.B - first.B) * value));
        }

        private void DrawHeader(Graphics graphics)
        {
            DrawLogo(graphics, new Rectangle(34, 27, 50, 50));

            double lightOpacity = Math.Max(
                0.0,
                1.0 - logoBlend * 2.0);
            double darkOpacity = Math.Max(
                0.0,
                (logoBlend - 0.5) * 2.0);
            int lightAlpha = (int)Math.Round(
                247.0 * lightOpacity);
            int poweredAlpha = (int)Math.Round(
                145.0 * darkOpacity);
            int darkTitleAlpha = (int)Math.Round(
                247.0 * darkOpacity);
            DrawText(
                graphics,
                Localization.Get("appName"),
                titleFont,
                new Rectangle(98, 28, 300, 48),
                Color.FromArgb(
                    lightAlpha,
                    247,
                    246,
                    242),
                ContentAlignment.MiddleLeft);
            DrawText(
                graphics,
                "Powered by",
                microFont,
                new Rectangle(99, 29, 250, 16),
                Color.FromArgb(
                    poweredAlpha,
                    211,
                    216,
                    220));
            DrawText(
                graphics,
                "Sendyun",
                sectionFont,
                new Rectangle(98, 45, 250, 27),
                Color.FromArgb(
                    darkTitleAlpha,
                    247,
                    246,
                    242));

            DrawLanguageButton(
                graphics,
                new Rectangle(720, 28, 150, 30));
            DrawWindowButton(graphics, new Rectangle(886, 28, 34, 30), "minimize");
            DrawWindowButton(graphics, new Rectangle(928, 28, 34, 30), "close");

            using (Pen divider = new Pen(
                Color.FromArgb(40, 255, 255, 255),
                1f))
            {
                graphics.DrawLine(divider, 28, HeaderHeight, 972, HeaderHeight);
            }
        }

        private void DrawSidebar(Graphics graphics)
        {
            Rectangle overview = new Rectangle(30, 122, 184, 44);
            Rectangle about = new Rectangle(30, 174, 184, 44);
            Rectangle performance = new Rectangle(30, 226, 184, 44);
            Rectangle timer = new Rectangle(30, 510, 184, 44);
            DrawNavButton(
                graphics,
                overview,
                Localization.Get("controlCenter"),
                "navDashboard",
                !authorPage && !performancePage && !timerPage);
            DrawNavButton(
                graphics,
                about,
                Localization.Get("aboutAuthor"),
                "navAuthor",
                authorPage);
            DrawPerformanceNavButton(graphics, performance);
            DrawTimerNavButton(graphics, timer);
            float y = 304;
            DrawText(
                graphics,
                Localization.Get("runtimeStatus"),
                microFont,
                new Rectangle(34, (int)y, 140, 18),
                Color.FromArgb(120, 220, 220, 218));
            y += 24;
            using (SolidBrush dot = new SolidBrush(
                paused
                    ? Color.FromArgb(214, 180, 98)
                    : Color.FromArgb(106, 210, 163)))
            {
                graphics.FillEllipse(dot, 34, y + 5, 8, 8);
            }
            DrawText(
                graphics,
                paused ? Localization.Get("statusPaused") : runtimeStatus,
                smallFont,
                new Rectangle(50, (int)y, 160, 22),
                Color.FromArgb(205, 231, 231, 226));

            DrawText(
                graphics,
                Localization.Get("shortcuts"),
                microFont,
                new Rectangle(34, 382, 140, 18),
                Color.FromArgb(120, 220, 220, 218));
            DrawShortcut(
                graphics,
                34,
                408,
                "Ctrl + M",
                Localization.Get("shortcutMusic"));
            DrawShortcut(
                graphics,
                34,
                440,
                "Ctrl + M + L",
                Localization.Get("shortcutPersistent"));
            DrawShortcut(
                graphics,
                34,
                472,
                "Ctrl + Alt + N",
                Localization.Get("shortcutHistory"));

            Rectangle authorCard = new Rectangle(28, 568, 188, 52);
            using (GraphicsPath path = RoundedRectangle(authorCard, 12))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(
                    hoverZone == "author" ? 31 : 16,
                    255,
                    255,
                    255)))
            using (Pen border = new Pen(
                Color.FromArgb(35, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                Localization.Get("creator"),
                microFont,
                new Rectangle(42, 577, 64, 15),
                Color.FromArgb(110, 210, 210, 207));
            DrawText(
                graphics,
                "Mr.Dove",
                sectionFont,
                new Rectangle(42, 592, 154, 22),
                Color.FromArgb(235, 245, 243, 237));

            DrawInstallationDayCard(graphics);
            if (installationTextNeedsScroll &&
                Visible &&
                !installationMarqueeTimer.Enabled)
            {
                installationMarqueeTimer.Start();
            }
        }

        private void DrawInstallationDayCard(Graphics graphics)
        {
            Rectangle card = new Rectangle(28, 632, 188, 50);
            DrawGlassCard(
                graphics,
                hoverZone == "installationInfo"
                    ? "installationInfo"
                    : "",
                card);
            Rectangle textBounds = new Rectangle(
                card.Left + 13,
                card.Top + 8,
                card.Width - 26,
                card.Height - 16);
            if (!installationTextNeedsScroll)
            {
                DrawText(
                    graphics,
                    installationDayText,
                    microFont,
                    textBounds,
                    Color.FromArgb(212, 230, 226, 214),
                    ContentAlignment.MiddleLeft);
                return;
            }

            double cycle = installationTextWidth + 30.0;
            float offset = (float)(
                installationMarqueeOffset % Math.Max(1.0, cycle));
            GraphicsState state = graphics.Save();
            graphics.SetClip(textBounds);
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(212, 230, 226, 214)))
            using (StringFormat format = new StringFormat())
            {
                format.FormatFlags = StringFormatFlags.NoWrap;
                float y = textBounds.Top +
                    (textBounds.Height - microFont.GetHeight(graphics)) /
                    2f;
                graphics.DrawString(
                    installationDayText,
                    microFont,
                    brush,
                    textBounds.Left - offset,
                    y,
                    format);
                graphics.DrawString(
                    installationDayText,
                    microFont,
                    brush,
                    textBounds.Left - offset + (float)cycle,
                    y,
                    format);
            }
            graphics.Restore(state);
        }

        private void UpdateInstallationDayText()
        {
            installationDayText = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Localization.Get("installationDay"),
                installationDays);
            try
            {
                Size measured = TextRenderer.MeasureText(
                    installationDayText,
                    microFont,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.SingleLine);
                installationTextWidth = (int)Math.Ceiling(
                    measured.Width / Math.Max(1.0, dpiScale));
            }
            catch
            {
                installationTextWidth =
                    installationDayText.Length * 10;
            }
            installationTextNeedsScroll =
                installationTextWidth > 162;
            installationMarqueeOffset = 0.0;
            if (installationTextNeedsScroll &&
                Visible &&
                installationMarqueeTimer != null)
            {
                installationMarqueeTimer.Start();
            }
            else if (installationMarqueeTimer != null)
            {
                installationMarqueeTimer.Stop();
            }
        }

        private void ShowInstallationInfo()
        {
            System.Globalization.CultureInfo culture;
            switch (Localization.Current)
            {
                case UiLanguage.English:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("en-US");
                    break;
                case UiLanguage.Japanese:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("ja-JP");
                    break;
                case UiLanguage.Korean:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("ko-KR");
                    break;
                case UiLanguage.Russian:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("ru-RU");
                    break;
                case UiLanguage.French:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("fr-FR");
                    break;
                default:
                    culture = System.Globalization.CultureInfo
                        .GetCultureInfo("zh-CN");
                    break;
            }

            string message =
                string.Format(
                    culture,
                    Localization.Get("installationFirstDate"),
                    installationDate.ToString("D", culture)) +
                Environment.NewLine +
                string.Format(
                    culture,
                    Localization.Get("installationWeekday"),
                    installationDate.ToString("dddd", culture)) +
                Environment.NewLine +
                string.Format(
                    culture,
                    Localization.Get("installationCompanionDays"),
                    installationDays) +
                Environment.NewLine +
                string.Format(
                    culture,
                    Localization.Get("installationCurrentVersion"),
                    DisplayVersion);
            MessageBox.Show(
                this,
                message,
                Localization.Get("installationInfoTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void DrawPerformancePage(Graphics graphics)
        {
            PerformanceSnapshot data = performanceSnapshot;
            int left = 246;
            int width = 700;
            double currentFps = data == null ? 0.0 : data.CurrentFps;
            int targetFps = data == null ? 30 : data.TargetFps;
            PerformanceHealth health = data == null
                ? PerformanceHealth.Idle
                : data.Health;
            Color healthColor = PerformanceColor(health);

            DrawText(
                graphics,
                Localization.Get("fpsMonitorTitle"),
                titleFont,
                new Rectangle(left, 116, 580, 34),
                Color.FromArgb(248, 247, 243));
            DrawText(
                graphics,
                Localization.Get("fpsMonitorSubtitle"),
                bodyFont,
                new Rectangle(left, 151, 320, 24),
                Color.FromArgb(145, 211, 215, 218));
            DrawFrameRateControl(
                graphics,
                new Rectangle(576, 147, 370, 30));

            Rectangle hero = new Rectangle(left, 186, width, 160);
            DrawGlassCard(graphics, "performance", hero);
            DrawText(
                graphics,
                currentFps.ToString("0"),
                performanceFont,
                new Rectangle(left + 22, 198, 150, 66),
                healthColor,
                ContentAlignment.MiddleLeft);
            DrawText(
                graphics,
                "FPS",
                sectionFont,
                new Rectangle(left + 154, 215, 58, 32),
                Color.FromArgb(200, 230, 231, 226));
            DrawText(
                graphics,
                Localization.Get("fpsTarget") + " " + targetFps,
                smallFont,
                new Rectangle(left + 24, 266, 170, 22),
                Color.FromArgb(156, 210, 214, 215));
            DrawText(
                graphics,
                PerformanceStatusText(health),
                smallFont,
                new Rectangle(left + 24, 290, 170, 22),
                healthColor);
            DrawText(
                graphics,
                Localization.Get("fpsDropped") + " " +
                    (data == null
                        ? "--"
                        : (data.DroppedFrameRatio * 100).ToString("0.0") + "%") +
                    "   " +
                    Localization.Get("fpsJankEvents") + " " +
                    (data == null ? "--" : data.JankEventCount.ToString()),
                microFont,
                new Rectangle(left + 24, 316, 210, 18),
                Color.FromArgb(125, 205, 208, 207));
            DrawPerformanceChart(
                graphics,
                new Rectangle(left + 236, 204, 442, 122),
                targetFps,
                healthColor,
                data);

            int tileWidth = 220;
            int tileHeight = 68;
            int gap = 13;
            int rowOne = 358;
            int rowTwo = 438;
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left, rowOne, tileWidth, tileHeight),
                Localization.Get("fpsAverage"),
                data == null ? "--" : data.AverageFps.ToString("0.0"),
                Localization.Get("fpsUnit"));
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left + tileWidth + gap, rowOne, tileWidth, tileHeight),
                Localization.Get("fpsMinimum"),
                data == null ? "--" : data.MinimumFps.ToString("0"),
                Localization.Get("fpsUnit"));
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left + (tileWidth + gap) * 2, rowOne, tileWidth, tileHeight),
                Localization.Get("fpsOnePercentLow"),
                data == null ? "--" : data.OnePercentLowFps.ToString("0.0"),
                Localization.Get("fpsUnit"));
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left, rowTwo, tileWidth, tileHeight),
                Localization.Get("fpsFrameTime"),
                data == null ? "--" : data.AverageFrameTimeMs.ToString("0.0"),
                "ms");
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left + tileWidth + gap, rowTwo, tileWidth, tileHeight),
                Localization.Get("fpsJitter"),
                data == null ? "--" : data.FrameTimeDeviationMs.ToString("0.0"),
                "ms");
            DrawPerformanceMetricTile(
                graphics,
                new Rectangle(left + (tileWidth + gap) * 2, rowTwo, tileWidth, tileHeight),
                Localization.Get("fpsStability"),
                data == null ? "--" : data.StabilityScore.ToString(),
                "/ 100");

            Rectangle system = new Rectangle(left, 518, width, 82);
            DrawGlassCard(graphics, "performanceSystem", system);
            DrawText(
                graphics,
                Localization.Get("fpsSystemContext"),
                microFont,
                new Rectangle(system.Left + 18, system.Top + 9, 180, 16),
                Color.FromArgb(130, 215, 216, 211));
            string[] systemLabels = new string[]
            {
                Localization.Get("fpsCpu"),
                Localization.Get("fpsGpu"),
                Localization.Get("fpsAppCpu"),
                Localization.Get("fpsMemory"),
                Localization.Get("fpsRefreshRate"),
                Localization.Get("fpsResolution")
            };
            string[] systemValues = new string[]
            {
                data == null ? "--" : data.SystemCpuPercent.ToString("0") + "%",
                data == null || data.GpuUsagePercent < 0.0
                    ? "--"
                    : data.GpuUsagePercent.ToString("0") + "%",
                data == null ? "--" : data.ProcessCpuPercent.ToString("0.0") + "%",
                data == null ? "--" : data.WorkingSetMegabytes.ToString("0") + " MB",
                data == null ? "--" : data.RefreshRateHz + " Hz",
                data == null
                    ? "--"
                    : data.ScreenWidth + "×" + data.ScreenHeight
            };
            int systemCell = system.Width / systemValues.Length;
            for (int index = 0; index < systemValues.Length; index++)
            {
                int x = system.Left + index * systemCell;
                DrawText(
                    graphics,
                    systemLabels[index],
                    microFont,
                    new Rectangle(x + 12, system.Top + 32, systemCell - 20, 15),
                    Color.FromArgb(110, 205, 208, 207));
                DrawText(
                    graphics,
                    systemValues[index],
                    smallFont,
                    new Rectangle(x + 12, system.Top + 49, systemCell - 20, 22),
                    Color.FromArgb(218, 236, 234, 226));
                if (index > 0)
                {
                    using (Pen divider = new Pen(
                        Color.FromArgb(24, 255, 255, 255),
                        1f))
                    {
                        graphics.DrawLine(
                            divider,
                            x,
                            system.Top + 32,
                            x,
                            system.Bottom - 12);
                    }
                }
            }

            Rectangle eventsCard = new Rectangle(left, 612, width, 78);
            DrawGlassCard(graphics, "performanceEvents", eventsCard);
            DrawText(
                graphics,
                Localization.Get("fpsEventLog"),
                microFont,
                new Rectangle(
                    eventsCard.Left + 18,
                    eventsCard.Top + 9,
                    180,
                    16),
                Color.FromArgb(130, 215, 216, 211));
            DrawPerformanceEvents(
                graphics,
                eventsCard,
                data);
        }

        private void DrawFrameRateControl(
            Graphics graphics,
            Rectangle bounds)
        {
            DrawText(
                graphics,
                Localization.Get("fpsLimit"),
                microFont,
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    76,
                    bounds.Height),
                Color.FromArgb(132, 210, 213, 211),
                ContentAlignment.MiddleRight);

            int[] rates = new int[] { 60, 90, 120, 165 };
            int buttonWidth = 54;
            int gap = 5;
            int startX = bounds.Left + 84;
            for (int index = 0; index < rates.Length; index++)
            {
                int rate = rates[index];
                Rectangle button = new Rectangle(
                    startX + index * (buttonWidth + gap),
                    bounds.Top,
                    buttonWidth,
                    bounds.Height);
                bool selected = frameRateLimit == rate;
                string zone = "fpsLimit" + rate;
                using (GraphicsPath path = RoundedRectangle(button, 9))
                using (SolidBrush fill = new SolidBrush(
                    selected
                        ? Color.FromArgb(66, 219, 192, 125)
                        : Color.FromArgb(
                            hoverZone == zone ? 29 : 14,
                            255,
                            255,
                            255)))
                using (Pen border = new Pen(
                    selected
                        ? Color.FromArgb(126, 238, 210, 151)
                        : Color.FromArgb(25, 255, 255, 255),
                    1f))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }
                DrawText(
                    graphics,
                    rate.ToString(),
                    microFont,
                    button,
                    selected
                        ? Color.FromArgb(242, 244, 230, 196)
                        : Color.FromArgb(155, 223, 225, 221),
                    ContentAlignment.MiddleCenter);
            }
        }

        private void DrawPerformanceMetricTile(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            string unit)
        {
            DrawGlassCard(graphics, "", bounds);
            DrawText(
                graphics,
                label,
                microFont,
                new Rectangle(
                    bounds.Left + 15,
                    bounds.Top + 9,
                    bounds.Width - 24,
                    16),
                Color.FromArgb(118, 207, 210, 209));
            DrawText(
                graphics,
                value,
                sectionFont,
                new Rectangle(
                    bounds.Left + 15,
                    bounds.Top + 29,
                    120,
                    28),
                Color.FromArgb(238, 242, 239, 231));
            DrawText(
                graphics,
                unit,
                microFont,
                new Rectangle(
                    bounds.Right - 78,
                    bounds.Top + 34,
                    62,
                    18),
                Color.FromArgb(112, 207, 211, 210),
                ContentAlignment.MiddleRight);
        }

        private void DrawPerformanceChart(
            Graphics graphics,
            Rectangle bounds,
            int targetFps,
            Color lineColor,
            PerformanceSnapshot data)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 12))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(17, 255, 255, 255)))
            {
                graphics.FillPath(fill, path);
            }
            using (Pen grid = new Pen(
                Color.FromArgb(24, 255, 255, 255),
                1f))
            {
                for (int index = 1; index < 4; index++)
                {
                    int y = bounds.Top + bounds.Height * index / 4;
                    graphics.DrawLine(
                        grid,
                        bounds.Left + 12,
                        y,
                        bounds.Right - 12,
                        y);
                }
            }

            double maximum = Math.Max(
                60.0,
                Math.Max(targetFps * 1.2, 1.0));
            for (int index = 0; index < performanceHistory.Count; index++)
            {
                maximum = Math.Max(maximum, performanceHistory[index]);
            }
            int targetY = bounds.Bottom - 12 -
                (int)Math.Round(
                    Math.Min(1.0, targetFps / maximum) *
                    (bounds.Height - 24));
            using (Pen targetPen = new Pen(
                Color.FromArgb(72, 225, 198, 132),
                1f))
            {
                targetPen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(
                    targetPen,
                    bounds.Left + 12,
                    targetY,
                    bounds.Right - 12,
                    targetY);
            }

            if (performanceHistory.Count >= 2)
            {
                PointF[] points = new PointF[performanceHistory.Count];
                float horizontalStep =
                    (bounds.Width - 24f) /
                    Math.Max(1, performanceHistory.Count - 1);
                for (int index = 0; index < performanceHistory.Count; index++)
                {
                    double ratio = Math.Max(
                        0.0,
                        Math.Min(1.0,
                            performanceHistory[index] / maximum));
                    points[index] = new PointF(
                        bounds.Left + 12 + horizontalStep * index,
                        (float)(bounds.Bottom - 12 -
                            ratio * (bounds.Height - 24)));
                }
                GraphicsState state = graphics.Save();
                using (GraphicsPath chartClip = RoundedRectangle(
                    bounds,
                    12))
                {
                    graphics.SetClip(chartClip);
                    using (Pen chartPen = new Pen(lineColor, 2.2f))
                    {
                        chartPen.LineJoin = LineJoin.Round;
                        graphics.DrawLines(chartPen, points);
                    }
                }
                graphics.Restore(state);
            }
            else
            {
                DrawText(
                    graphics,
                    Localization.Get("fpsCollecting"),
                    microFont,
                    bounds,
                    Color.FromArgb(105, 205, 208, 207),
                    ContentAlignment.MiddleCenter);
            }
        }

        private void DrawPerformanceEvents(
            Graphics graphics,
            Rectangle bounds,
            PerformanceSnapshot data)
        {
            if (data == null || data.Events == null || data.Events.Length == 0)
            {
                DrawText(
                    graphics,
                    Localization.Get("fpsNoEvents"),
                    smallFont,
                    new Rectangle(
                        bounds.Left + 18,
                        bounds.Top + 32,
                        bounds.Width - 36,
                        26),
                    Color.FromArgb(115, 207, 210, 208));
                return;
            }

            int count = Math.Min(2, data.Events.Length);
            for (int index = 0; index < count; index++)
            {
                PerformanceEventItem item =
                    data.Events[data.Events.Length - count + index];
                string kind = item.Kind == PerformanceEventKind.SevereJank
                    ? Localization.Get("fpsSevereJank")
                    : (item.Kind == PerformanceEventKind.Jank
                        ? Localization.Get("fpsJank")
                        : Localization.Get("fpsDroppedFrame"));
                string line =
                    item.Timestamp.ToString("HH:mm:ss") +
                    "   " +
                    kind +
                    "   " +
                    item.FrameTimeMilliseconds.ToString("0.0") +
                    " ms   " +
                    item.FramesPerSecond.ToString("0") +
                    " FPS";
                DrawText(
                    graphics,
                    line,
                    microFont,
                    new Rectangle(
                        bounds.Left + 18,
                        bounds.Top + 29 + index * 18,
                        bounds.Width - 36,
                        18),
                    index == 0
                        ? Color.FromArgb(205, 224, 213, 191)
                        : Color.FromArgb(145, 209, 211, 208));
            }
        }

        private static Color PerformanceColor(PerformanceHealth health)
        {
            if (health == PerformanceHealth.Stable)
            {
                return Color.FromArgb(238, 111, 215, 166);
            }
            if (health == PerformanceHealth.Variable)
            {
                return Color.FromArgb(238, 229, 191, 104);
            }
            if (health == PerformanceHealth.Jank)
            {
                return Color.FromArgb(238, 232, 116, 112);
            }
            return Color.FromArgb(150, 202, 207, 210);
        }

        private static string PerformanceStatusText(
            PerformanceHealth health)
        {
            if (health == PerformanceHealth.Stable)
            {
                return Localization.Get("fpsStatusStable");
            }
            if (health == PerformanceHealth.Variable)
            {
                return Localization.Get("fpsStatusVariable");
            }
            if (health == PerformanceHealth.Jank)
            {
                return Localization.Get("fpsStatusJank");
            }
            return Localization.Get("fpsStatusIdle");
        }

        private void DrawTimerPage(Graphics graphics)
        {
            int left = 246;
            int width = 700;
            DrawText(
                graphics,
                Localization.Get("timer"),
                titleFont,
                new Rectangle(left, 116, 500, 34),
                Color.FromArgb(248, 247, 243));
            DrawText(
                graphics,
                Localization.Get("timerSubtitle"),
                bodyFont,
                new Rectangle(left, 151, 650, 24),
                Color.FromArgb(145, 211, 215, 218));

            Rectangle countdownTab = new Rectangle(left, 184, 120, 36);
            Rectangle stopwatchTab = new Rectangle(left + 128, 184, 120, 36);
            DrawTimerModeButton(
                graphics,
                countdownTab,
                Localization.Get("timerCountdown"),
                "timerModeCountdown",
                timerSetupMode == SmartTimerMode.Countdown);
            DrawTimerModeButton(
                graphics,
                stopwatchTab,
                Localization.Get("timerStopwatch"),
                "timerModeStopwatch",
                timerSetupMode == SmartTimerMode.Stopwatch);

            bool active = TimerModeActive(timerSetupMode);
            bool pickerVisible =
                timerSetupMode == SmartTimerMode.Countdown &&
                !active;
            if (pickerVisible)
            {
                DrawTimerCountdownPicker(
                    graphics,
                    new Rectangle(left, 232, width, 200));
            }
            else
            {
                DrawTimerStatusCard(
                    graphics,
                    new Rectangle(left, 232, width, 100));
            }
            int presetHeadingY = pickerVisible ? 454 : 350;
            bool showPresets =
                timerSetupMode == SmartTimerMode.Countdown;
            DrawText(
                graphics,
                showPresets
                    ? Localization.Get("timerPresets")
                    : Localization.Get("timerLapList"),
                sectionFont,
                new Rectangle(
                    left,
                    presetHeadingY,
                    300,
                    24),
                Color.FromArgb(240, 238, 233));

            int presetTop = presetHeadingY + 36;
            int presetWidth = 340;
            int presetHeight = 52;
            int presetGap = 12;
            if (showPresets)
            {
                int maximum = Math.Min(6, timerPresets.Length);
                for (int index = 0; index < maximum; index++)
                {
                    int column = index % 2;
                    int row = index / 2;
                    Rectangle bounds = new Rectangle(
                        left + column * (presetWidth + presetGap),
                        presetTop + row * (presetHeight + 7),
                        presetWidth,
                        presetHeight);
                    DrawTimerPreset(
                        graphics,
                        bounds,
                        index,
                        timerPresets[index]);
                }
                DrawActionButton(
                    graphics,
                    "timerAddPreset",
                    new Rectangle(
                        left,
                        presetTop + 181,
                        width,
                        42),
                    Localization.Get("timerAddPreset"),
                    false);
            }
            else
            {
                DrawTimerLaps(
                    graphics,
                    TimerLapsBounds);
            }

            DrawTimerColorCard(
                graphics,
                new Rectangle(
                    left,
                    presetTop + 243,
                    width,
                    246));
            DrawTimerSoundCard(
                graphics,
                new Rectangle(
                    left,
                    presetTop + 503,
                    width,
                    108));
        }

        private void DrawTimerCountdownPicker(
            Graphics graphics,
            Rectangle bounds)
        {
            DrawGlassCard(graphics, "timerPicker", bounds);
            int[] limits = new int[] { 24, 60, 60 };
            int[] centers = new int[] { 350, 472, 594 };
            string[] labels = new string[]
            {
                Localization.Get("timerHours"),
                Localization.Get("timerMinutes"),
                Localization.Get("timerSeconds")
            };
            GraphicsState wheelState = graphics.Save();
            graphics.SetClip(new Rectangle(
                bounds.Left,
                bounds.Top + 20,
                bounds.Width,
                136));
            int centerY = bounds.Top + 97;
            int rowPitch = 38;
            for (int column = 0; column < 3; column++)
            {
                int centerX = bounds.Left + centers[column] - 246;
                double visualValue = timerWheelValues[column];
                int baseValue = (int)Math.Floor(visualValue);
                using (GraphicsPath highlight = RoundedRectangle(
                    new Rectangle(centerX - 50, centerY - 24, 100, 48),
                    15))
                using (LinearGradientBrush highlightFill =
                    new LinearGradientBrush(
                        new Rectangle(centerX - 50, centerY - 24, 100, 48),
                        Color.FromArgb(14, 255, 255, 255),
                        Color.FromArgb(3, 255, 255, 255),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(highlightFill, highlight);
                }
                for (int row = -3; row <= 3; row++)
                {
                    int number = baseValue + row;
                    int wrapped = number % limits[column];
                    if (wrapped < 0)
                    {
                        wrapped += limits[column];
                    }
                    double distance = Math.Abs(number - visualValue);
                    double fade = 1.0 - Math.Min(1.0, distance / 2.7);
                    fade = fade * fade * (3.0 - 2.0 * fade);
                    int alpha = Math.Max(
                        24,
                        Math.Min(238, (int)Math.Round(28 + 210 * fade)));
                    float scale = 0.58f + 0.42f * (float)fade;
                    float y = centerY +
                        (float)((number - visualValue) * rowPitch);
                    GraphicsState rowState = graphics.Save();
                    graphics.TranslateTransform(centerX, y);
                    graphics.ScaleTransform(scale, scale);
                    using (SolidBrush brush = new SolidBrush(
                        Color.FromArgb(alpha, 241, 241, 237)))
                    using (StringFormat format = new StringFormat())
                    {
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        graphics.DrawString(
                            wrapped.ToString("00"),
                            performanceFont,
                            brush,
                            new RectangleF(-56f, -34f, 112f, 68f),
                            format);
                    }
                    graphics.Restore(rowState);
                }
            }
            graphics.Restore(wheelState);
            for (int column = 0; column < 3; column++)
            {
                int centerX = bounds.Left + centers[column] - 246;
                DrawText(
                    graphics,
                    labels[column],
                    microFont,
                    new Rectangle(centerX - 48, bounds.Top + 169, 96, 17),
                    Color.FromArgb(112, 211, 214, 212),
                    ContentAlignment.MiddleCenter);
            }
            DrawText(
                graphics,
                ":",
                performanceFont,
                new Rectangle(411, bounds.Top + 68, 24, 58),
                Color.FromArgb(160, 239, 239, 234),
                ContentAlignment.MiddleCenter);
            DrawText(
                graphics,
                ":",
                performanceFont,
                new Rectangle(533, bounds.Top + 68, 24, 58),
                Color.FromArgb(160, 239, 239, 234),
                ContentAlignment.MiddleCenter);
            DrawActionButton(
                graphics,
                "timerStartCountdown",
                new Rectangle(
                    bounds.Right - 170,
                    bounds.Top + 69,
                    138,
                    58),
                Localization.Get("timerStartCountdown"),
                true);
            DrawText(
                graphics,
                Localization.Get("timerWheelHint"),
                microFont,
                new Rectangle(
                    bounds.Right - 270,
                    bounds.Top + 169,
                    250,
                    17),
                Color.FromArgb(105, 207, 210, 210));
        }

        private void DrawTimerModeButton(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string zone,
            bool selected)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 10))
            using (SolidBrush fill = new SolidBrush(
                selected
                    ? Color.FromArgb(
                        56,
                        timerAccentColor.R,
                        timerAccentColor.G,
                        timerAccentColor.B)
                    : Color.FromArgb(
                        hoverZone == zone ? 25 : 13,
                        255,
                        255,
                        255)))
            using (Pen border = new Pen(
                selected
                    ? Color.FromArgb(
                        125,
                        timerAccentColor.R,
                        timerAccentColor.G,
                        timerAccentColor.B)
                    : Color.FromArgb(24, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                label,
                smallFont,
                bounds,
                selected
                    ? Color.FromArgb(242, 242, 237)
                    : Color.FromArgb(155, 207, 210, 211),
                ContentAlignment.MiddleCenter);
            SmartTimerMode mode =
                zone == "timerModeStopwatch"
                    ? SmartTimerMode.Stopwatch
                    : SmartTimerMode.Countdown;
            if (TimerModeActive(mode))
            {
                using (SolidBrush runningDot = new SolidBrush(
                    timerAccentColor))
                {
                    graphics.FillEllipse(
                        runningDot,
                        bounds.Right - 13,
                        bounds.Top + 8,
                        5,
                        5);
                }
            }
        }

        private void DrawTimerStatusCard(
            Graphics graphics,
            Rectangle bounds)
        {
            DrawGlassCard(graphics, "timerStatus", bounds);
            bool active = TimerModeActive(timerSetupMode);
            string display = active
                ? TimerModeDisplayTime(timerSetupMode)
                : "--:--";
            DrawText(
                graphics,
                display,
                performanceFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 14,
                    220,
                    55),
                active
                    ? timerAccentColor
                    : Color.FromArgb(115, 205, 209, 209),
                ContentAlignment.MiddleLeft);
            DrawText(
                graphics,
                active
                    ? (timerSetupMode == SmartTimerMode.Countdown
                        ? Localization.Get("timerCountdown")
                        : Localization.Get("timerStopwatch"))
                    : Localization.Get("timerReady"),
                smallFont,
                new Rectangle(
                    bounds.Left + 24,
                    bounds.Top + 66,
                    220,
                    20),
                Color.FromArgb(137, 206, 211, 211));

            if (!active)
            {
                if (timerSetupMode == SmartTimerMode.Stopwatch)
                {
                    DrawActionButton(
                        graphics,
                        "timerStartStopwatch",
                        new Rectangle(
                            bounds.Right - 190,
                            bounds.Top + 28,
                            166,
                            44),
                        Localization.Get("timerStart"),
                        true);
                }
                else
                {
                    DrawText(
                        graphics,
                        Localization.Get("timerChoosePreset"),
                        smallFont,
                        new Rectangle(
                            bounds.Right - 282,
                            bounds.Top + 34,
                            254,
                            32),
                        Color.FromArgb(126, 207, 211, 211),
                        ContentAlignment.MiddleRight);
                }
                return;
            }

            int count = timerSetupMode == SmartTimerMode.Stopwatch
                ? 3
                : 2;
            int gap = 8;
            int buttonWidth = timerSetupMode == SmartTimerMode.Stopwatch
                ? 92
                : 112;
            int startX = bounds.Right -
                24 -
                buttonWidth * count -
                gap * (count - 1);
            DrawActionButton(
                graphics,
                "timerPause",
                new Rectangle(
                    startX,
                    bounds.Top + 27,
                    buttonWidth,
                    46),
                TimerModeCompleted(timerSetupMode)
                    ? Localization.Get("timerClose")
                    : (TimerModePaused(timerSetupMode)
                        ? Localization.Get("timerResume")
                        : Localization.Get("timerPause")),
                true);
            int second = startX + buttonWidth + gap;
            if (timerSetupMode == SmartTimerMode.Stopwatch)
            {
                DrawActionButton(
                    graphics,
                    "timerLap",
                    new Rectangle(
                        second,
                        bounds.Top + 27,
                        buttonWidth,
                        46),
                    Localization.Get("timerLap"),
                    false);
                second += buttonWidth + gap;
            }
            DrawActionButton(
                graphics,
                "timerCancel",
                new Rectangle(
                    second,
                    bounds.Top + 27,
                    buttonWidth,
                    46),
                Localization.Get("timerCancel"),
                false);
        }

        private void DrawTimerPreset(
            Graphics graphics,
            Rectangle bounds,
            int index,
            TimerPreset preset)
        {
            string zone = "timerPreset" + index;
            DrawGlassCard(
                graphics,
                hoverZone == zone ? zone : "",
                bounds);
            DrawText(
                graphics,
                preset.Name,
                smallFont,
                new Rectangle(
                    bounds.Left + 15,
                    bounds.Top + 7,
                    bounds.Width - 98,
                    21),
                Color.FromArgb(232, 235, 231, 223));
            DrawText(
                graphics,
                FormatPresetDuration(preset.DurationSeconds),
                microFont,
                new Rectangle(
                    bounds.Left + 15,
                    bounds.Top + 27,
                    180,
                    17),
                Color.FromArgb(125, 211, 214, 213));
            using (Pen deletePen = new Pen(
                Color.FromArgb(
                    hoverZone == "timerPresetDelete" + index
                        ? 190
                        : 80,
                    224,
                    226,
                    222),
                1.4f))
            {
                int x = bounds.Right - 23;
                int y = bounds.Top + 20;
                graphics.DrawLine(
                    deletePen,
                    x - 5,
                    y - 5,
                    x + 5,
                    y + 5);
                graphics.DrawLine(
                    deletePen,
                    x + 5,
                    y - 5,
                    x - 5,
                    y + 5);
            }
        }

        private void DrawTimerLaps(
            Graphics graphics,
            Rectangle bounds)
        {
            TimeSpan[] laps = timerSnapshot == null
                ? null
                : timerSnapshot.Laps;
            if (laps == null || laps.Length == 0)
            {
                DrawGlassCard(graphics, "timerLapList", bounds);
                DrawText(
                    graphics,
                    Localization.Get("timerNoLaps"),
                    smallFont,
                    bounds,
                    Color.FromArgb(118, 207, 210, 210),
                    ContentAlignment.MiddleCenter);
                return;
            }

            int count = Math.Min(24, laps.Length);
            double maximumScroll = Math.Max(
                0.0,
                count * 59.0 - bounds.Height);
            timerLapScroll = Math.Max(
                0.0,
                Math.Min(maximumScroll, timerLapScroll));
            Rectangle listBounds = new Rectangle(
                bounds.Left,
                bounds.Top,
                bounds.Width - (count > 3 ? 14 : 0),
                bounds.Height);
            GraphicsState state = graphics.Save();
            graphics.SetClip(listBounds);
            for (int index = 0; index < count; index++)
            {
                Rectangle row = new Rectangle(
                    bounds.Left,
                    bounds.Top +
                        index * 59 -
                        (int)Math.Round(timerLapScroll),
                    listBounds.Width,
                    52);
                if (row.Bottom < bounds.Top ||
                    row.Top > bounds.Bottom)
                {
                    continue;
                }
                DrawGlassCard(graphics, "timerLap" + index, row);
                if (index == 0)
                {
                    using (SolidBrush marker = new SolidBrush(timerAccentColor))
                    {
                        graphics.FillRectangle(
                            marker,
                            row.Left + 2,
                            row.Top + 13,
                            3,
                            row.Height - 26);
                    }
                }
                DrawText(
                    graphics,
                    string.Format(
                        Localization.Get("timerLapNumber"),
                        laps.Length - index),
                    smallFont,
                    new Rectangle(
                        row.Left + 18,
                        row.Top,
                        180,
                        row.Height),
                    Color.FromArgb(232, 235, 231, 223));
                DrawText(
                    graphics,
                    FormatLapDuration(laps[index]),
                    smallFont,
                    new Rectangle(
                        row.Right - 186,
                        row.Top,
                        164,
                        row.Height),
                    timerAccentColor,
                    ContentAlignment.MiddleRight);
            }
            graphics.Restore(state);

            if (count > 3)
            {
                Rectangle track = new Rectangle(
                    bounds.Right - 7,
                    bounds.Top + 5,
                    4,
                    bounds.Height - 10);
                using (SolidBrush trackFill = new SolidBrush(
                    Color.FromArgb(20, 255, 255, 255)))
                {
                    graphics.FillRectangle(trackFill, track);
                }
                double visibleRatio =
                    bounds.Height / (double)(count * 59);
                int thumbHeight = Math.Max(
                    24,
                    (int)Math.Round(track.Height * visibleRatio));
                int available = track.Height - thumbHeight;
                int thumbTop = track.Top +
                    (maximumScroll <= 0.0
                        ? 0
                        : (int)Math.Round(
                            available *
                            timerLapScroll /
                            maximumScroll));
                using (SolidBrush thumb = new SolidBrush(
                    Color.FromArgb(112, 225, 227, 224)))
                {
                    graphics.FillRectangle(
                        thumb,
                        track.Left,
                        thumbTop,
                        track.Width,
                        thumbHeight);
                }
            }
        }

        private static string FormatLapDuration(TimeSpan value)
        {
            long totalSeconds = Math.Max(
                0,
                (long)Math.Floor(value.TotalSeconds));
            long hours = totalSeconds / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;
            int hundredths = Math.Max(
                0,
                Math.Min(99, value.Milliseconds / 10));
            if (hours > 0)
            {
                return hours.ToString("00") +
                    ":" +
                    minutes.ToString("00") +
                    ":" +
                    seconds.ToString("00") +
                    "." +
                    hundredths.ToString("00");
            }
            return minutes.ToString("00") +
                ":" +
                seconds.ToString("00") +
                "." +
                hundredths.ToString("00");
        }

        private void DrawTimerColorCard(
            Graphics graphics,
            Rectangle bounds)
        {
            DrawGlassCard(graphics, "timerColor", bounds);
            DrawText(
                graphics,
                Localization.Get("timerColor"),
                sectionFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 14,
                    240,
                    24),
                Color.FromArgb(242, 240, 234));
            EnsureTimerColorWheel(126);
            if (timerColorWheel != null)
            {
                Rectangle wheelBounds = TimerColorWheelBounds;
                graphics.DrawImage(
                    timerColorWheel,
                    wheelBounds);
                using (Pen ring = new Pen(
                    Color.FromArgb(214, 239, 240, 237),
                    1.2f))
                {
                    graphics.DrawEllipse(
                        ring,
                        wheelBounds);
                }
                PointF indicator = TimerWheelIndicatorPoint();
                using (SolidBrush marker = new SolidBrush(Color.White))
                using (Pen outline = new Pen(
                    Color.FromArgb(180, 20, 23, 30),
                    1.3f))
                {
                    graphics.FillEllipse(
                        marker,
                        indicator.X - 4f,
                        indicator.Y - 4f,
                        8f,
                        8f);
                    graphics.DrawEllipse(
                        outline,
                        indicator.X - 4f,
                        indicator.Y - 4f,
                        8f,
                        8f);
                }
            }

            Rectangle brightness = TimerBrightnessBounds;
            using (SolidBrush track = new SolidBrush(
                Color.FromArgb(31, 255, 255, 255)))
            {
                graphics.FillRectangle(track, brightness);
            }
            int brightnessY = brightness.Bottom -
                (int)Math.Round(brightness.Height * timerValue);
            using (Pen progress = new Pen(
                Color.FromArgb(180, 222, 226, 228),
                2f))
            {
                graphics.DrawLine(
                    progress,
                    brightness.Left + brightness.Width / 2f,
                    brightness.Bottom,
                    brightness.Left + brightness.Width / 2f,
                    brightnessY);
            }
            using (SolidBrush knob = new SolidBrush(Color.White))
            {
                graphics.FillEllipse(
                    knob,
                    brightness.Left - 3,
                    brightnessY - 6,
                    brightness.Width + 6,
                    12);
            }
            DrawText(
                graphics,
                Localization.Get("timerBrightness"),
                microFont,
                new Rectangle(
                    brightness.Left - 35,
                    brightness.Bottom + 5,
                    88,
                    16),
                Color.FromArgb(120, 210, 213, 212),
                ContentAlignment.MiddleCenter);

            DrawTimerRgbField(
                graphics,
                new Rectangle(bounds.Left + 240, bounds.Top + 54, 62, 42),
                "R",
                timerAccentColor.R);
            DrawTimerRgbField(
                graphics,
                new Rectangle(bounds.Left + 310, bounds.Top + 54, 62, 42),
                "G",
                timerAccentColor.G);
            DrawTimerRgbField(
                graphics,
                new Rectangle(bounds.Left + 380, bounds.Top + 54, 62, 42),
                "B",
                timerAccentColor.B);
            Rectangle preview = new Rectangle(
                bounds.Left + 470,
                bounds.Top + 54,
                116,
                76);
            using (GraphicsPath path = RoundedRectangle(preview, 13))
            using (SolidBrush fill = new SolidBrush(timerAccentColor))
            using (Pen border = new Pen(
                Color.FromArgb(120, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                string.Format(
                    "#{0:X2}{1:X2}{2:X2}",
                    timerAccentColor.R,
                    timerAccentColor.G,
                    timerAccentColor.B),
                microFont,
                new Rectangle(
                    bounds.Left + 240,
                    bounds.Top + 116,
                    360,
                    18),
                Color.FromArgb(145, 209, 212, 211));
            DrawText(
                graphics,
                Localization.Get("timerColorHint"),
                smallFont,
                new Rectangle(
                    bounds.Left + 240,
                    bounds.Top + 144,
                    420,
                    42),
                Color.FromArgb(110, 207, 210, 210));
        }

        private void DrawTimerRgbField(
            Graphics graphics,
            Rectangle bounds,
            string channel,
            int value)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 9))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(19, 255, 255, 255)))
            using (Pen border = new Pen(
                Color.FromArgb(31, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                channel,
                microFont,
                new Rectangle(bounds.Left + 8, bounds.Top + 3, 18, 14),
                Color.FromArgb(120, 210, 213, 211));
            DrawText(
                graphics,
                value.ToString(),
                smallFont,
                new Rectangle(
                    bounds.Left + 5,
                    bounds.Top + 17,
                    bounds.Width - 10,
                    22),
                Color.FromArgb(235, 239, 236, 229),
                ContentAlignment.MiddleCenter);
        }

        private void DrawTimerSoundCard(
            Graphics graphics,
            Rectangle bounds)
        {
            DrawGlassCard(graphics, "timerSound", bounds);
            DrawText(
                graphics,
                Localization.Get("timerSound"),
                sectionFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 14,
                    220,
                    24),
                Color.FromArgb(242, 240, 234));
            string name = string.IsNullOrWhiteSpace(timerSoundPath)
                ? Localization.Get("timerBuiltInSound")
                : System.IO.Path.GetFileName(timerSoundPath);
            DrawText(
                graphics,
                name,
                smallFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 49,
                    250,
                    28),
                Color.FromArgb(152, 211, 213, 211));
            DrawActionButton(
                graphics,
                "timerSoundBrowse",
                new Rectangle(
                    bounds.Left + 286,
                    bounds.Top + 28,
                    118,
                    42),
                Localization.Get("timerChooseSound"),
                false);
            DrawActionButton(
                graphics,
                "timerSoundPreview",
                new Rectangle(
                    bounds.Left + 416,
                    bounds.Top + 28,
                    108,
                    42),
                Localization.Get("timerPreviewSound"),
                false);
            DrawActionButton(
                graphics,
                "timerSoundReset",
                new Rectangle(
                    bounds.Left + 536,
                    bounds.Top + 28,
                    138,
                    42),
                Localization.Get("timerResetSound"),
                false);
        }

        private bool TimerPickerVisible
        {
            get
            {
                return timerPage &&
                    timerSetupMode == SmartTimerMode.Countdown &&
                    !TimerModeActive(SmartTimerMode.Countdown);
            }
        }

        private bool TimerModeActive(SmartTimerMode mode)
        {
            return timerSnapshot != null &&
                timerSnapshot.IsModeActive(mode);
        }

        private bool TimerModeRunning(SmartTimerMode mode)
        {
            if (timerSnapshot == null)
            {
                return false;
            }
            return mode == SmartTimerMode.Countdown
                ? timerSnapshot.CountdownRunning
                : timerSnapshot.StopwatchRunning;
        }

        private bool TimerModePaused(SmartTimerMode mode)
        {
            if (timerSnapshot == null)
            {
                return false;
            }
            return mode == SmartTimerMode.Countdown
                ? timerSnapshot.CountdownPaused
                : timerSnapshot.StopwatchPaused;
        }

        private bool TimerModeCompleted(SmartTimerMode mode)
        {
            return timerSnapshot != null &&
                mode == SmartTimerMode.Countdown &&
                timerSnapshot.CountdownCompleted;
        }

        private string TimerModeDisplayTime(SmartTimerMode mode)
        {
            if (timerSnapshot == null)
            {
                return "00:00";
            }
            return timerSnapshot.FormatForMode(mode);
        }

        private int TimerPresetTop
        {
            get { return TimerPickerVisible ? 490 : 386; }
        }

        private Rectangle TimerAddPresetBounds
        {
            get
            {
                return new Rectangle(
                    246,
                    TimerPresetTop + 181,
                    700,
                    42);
            }
        }

        private Rectangle TimerLapsBounds
        {
            get
            {
                return new Rectangle(
                    246,
                    TimerPresetTop,
                    700,
                    181);
            }
        }

        private Rectangle TimerColorCardBounds
        {
            get
            {
                return new Rectangle(
                    246,
                    TimerPresetTop + 243,
                    700,
                    246);
            }
        }

        private Rectangle TimerSoundCardBounds
        {
            get
            {
                return new Rectangle(
                    246,
                    TimerPresetTop + 503,
                    700,
                    108);
            }
        }

        private int TimerColumnAtPoint(Point point)
        {
            if (!TimerPickerVisible ||
                point.Y < 232 ||
                point.Y > 432)
            {
                return -1;
            }
            int[] centers = new int[] { 350, 472, 594 };
            for (int index = 0; index < centers.Length; index++)
            {
                if (point.X >= centers[index] - 48 &&
                    point.X <= centers[index] + 48)
                {
                    return index;
                }
            }
            return -1;
        }

        private int TimerColumnValue(int column)
        {
            if (column == 0)
            {
                return timerHours;
            }
            if (column == 1)
            {
                return timerMinutes;
            }
            return timerSeconds;
        }

        private void SetTimerColumnValue(int column, int value)
        {
            int previous = TimerColumnValue(column);
            value = Math.Max(0, value);
            if (column == 0)
            {
                timerHours = Math.Min(23, value);
            }
            else if (column == 1)
            {
                timerMinutes = Math.Min(59, value);
            }
            else
            {
                timerSeconds = Math.Min(59, value);
            }
            if (TimerColumnValue(column) != previous)
            {
                StartTimerWheelAnimation();
                RequestRender();
            }
        }

        private void StartTimerWheelAnimation()
        {
            if (Visible &&
                timerPage &&
                !timerWheelTimer.Enabled)
            {
                timerWheelTimer.Start();
            }
        }

        private void AdvanceTimerWheelAnimation()
        {
            if (!Visible ||
                !timerPage ||
                timerSetupMode != SmartTimerMode.Countdown)
            {
                timerWheelTimer.Stop();
                return;
            }

            bool active = false;
            const double frameSeconds = 1.0 / 60.0;
            for (int column = 0; column < 3; column++)
            {
                int limit = column == 0 ? 24 : 60;
                double target = TimerColumnValue(column);
                double current = timerWheelValues[column];
                double targetOffset = target - current;
                if (targetOffset > limit / 2.0)
                {
                    targetOffset -= limit;
                }
                else if (targetOffset < -limit / 2.0)
                {
                    targetOffset += limit;
                }
                double velocity = timerWheelVelocities[column];
                double acceleration =
                    targetOffset * 280.0 -
                    velocity * 34.0;
                velocity += acceleration * frameSeconds;
                current += velocity * frameSeconds;
                if (Math.Abs(targetOffset) < 0.002 &&
                    Math.Abs(velocity) < 0.015)
                {
                    timerWheelValues[column] = target;
                    timerWheelVelocities[column] = 0.0;
                }
                else
                {
                    timerWheelValues[column] = current;
                    timerWheelVelocities[column] = velocity;
                    active = true;
                }
            }
            RequestRender();
            if (!active)
            {
                timerWheelTimer.Stop();
            }
        }

        private Rectangle TimerColorWheelBounds
        {
            get
            {
                Rectangle card = TimerColorCardBounds;
                return new Rectangle(
                    card.Left + 24,
                    card.Top + 36,
                    126,
                    126);
            }
        }

        private Rectangle TimerBrightnessBounds
        {
            get
            {
                Rectangle wheel = TimerColorWheelBounds;
                return new Rectangle(
                    wheel.Right + 25,
                    wheel.Top,
                    16,
                    wheel.Height);
            }
        }

        private void EnsureTimerColorWheel(int size)
        {
            if (timerColorWheel != null &&
                timerColorWheelSize == size)
            {
                return;
            }
            if (timerColorWheel != null)
            {
                timerColorWheel.Dispose();
            }
            timerColorWheel = new Bitmap(
                size,
                size,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            timerColorWheelSize = size;
            float radius = size / 2f - 1f;
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x + 0.5 - center;
                    double dy = y + 0.5 - center;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > radius)
                    {
                        timerColorWheel.SetPixel(
                            x,
                            y,
                            Color.Transparent);
                        continue;
                    }
                    double hue =
                        (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
                    double saturation = Math.Min(1.0, distance / radius);
                    timerColorWheel.SetPixel(
                        x,
                        y,
                        HsvToColor(hue, saturation, 1.0));
                }
            }
        }

        private PointF TimerWheelIndicatorPoint()
        {
            Rectangle bounds = TimerColorWheelBounds;
            double angle = timerHue * Math.PI / 180.0;
            double radius =
                (bounds.Width / 2.0 - 2.0) *
                Math.Max(0.0, Math.Min(1.0, timerSaturation));
            return new PointF(
                bounds.Left + bounds.Width / 2f +
                    (float)(Math.Cos(angle) * radius),
                bounds.Top + bounds.Height / 2f +
                    (float)(Math.Sin(angle) * radius));
        }

        private void UpdateTimerColorFromWheel(Point point)
        {
            Rectangle bounds = TimerColorWheelBounds;
            double dx = point.X - (bounds.Left + bounds.Width / 2.0);
            double dy = point.Y - (bounds.Top + bounds.Height / 2.0);
            double radius = bounds.Width / 2.0 - 2.0;
            timerSaturation = Math.Max(
                0.0,
                Math.Min(1.0, Math.Sqrt(dx * dx + dy * dy) / radius));
            timerHue =
                (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
            ApplyTimerColor(
                HsvToColor(timerHue, timerSaturation, timerValue));
        }

        private void UpdateTimerBrightnessFromPoint(int y)
        {
            Rectangle bounds = TimerBrightnessBounds;
            timerValue = Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    (bounds.Bottom - y) /
                    (double)Math.Max(1, bounds.Height)));
            ApplyTimerColor(
                HsvToColor(timerHue, timerSaturation, timerValue));
        }

        private void ApplyTimerColor(Color color)
        {
            timerAccentColor = Color.FromArgb(255, color);
            ColorToHsv(
                timerAccentColor,
                out timerHue,
                out timerSaturation,
                out timerValue);
            EventHandler<Color> handler = TimerColorChanged;
            if (handler != null)
            {
                handler(this, timerAccentColor);
            }
            RequestRender();
        }

        private static Color HsvToColor(
            double hue,
            double saturation,
            double value)
        {
            double normalizedHue = ((hue % 360.0) + 360.0) % 360.0;
            double h = normalizedHue / 60.0;
            int sector = (int)Math.Floor(h) % 6;
            double fraction = h - Math.Floor(h);
            double p = value * (1.0 - saturation);
            double q = value * (1.0 - fraction * saturation);
            double t = value * (1.0 - (1.0 - fraction) * saturation);
            double r;
            double g;
            double b;
            switch (sector)
            {
                case 0:
                    r = value;
                    g = t;
                    b = p;
                    break;
                case 1:
                    r = q;
                    g = value;
                    b = p;
                    break;
                case 2:
                    r = p;
                    g = value;
                    b = t;
                    break;
                case 3:
                    r = p;
                    g = q;
                    b = value;
                    break;
                case 4:
                    r = t;
                    g = p;
                    b = value;
                    break;
                default:
                    r = value;
                    g = p;
                    b = q;
                    break;
            }
            return Color.FromArgb(
                (int)Math.Round(r * 255.0),
                (int)Math.Round(g * 255.0),
                (int)Math.Round(b * 255.0));
        }

        private static void ColorToHsv(
            Color color,
            out double hue,
            out double saturation,
            out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double maximum = Math.Max(r, Math.Max(g, b));
            double minimum = Math.Min(r, Math.Min(g, b));
            double delta = maximum - minimum;
            value = maximum;
            saturation = maximum <= 0.0 ? 0.0 : delta / maximum;
            if (delta <= 0.00001)
            {
                hue = 0.0;
            }
            else if (maximum == r)
            {
                hue = 60.0 * (((g - b) / delta) % 6.0);
            }
            else if (maximum == g)
            {
                hue = 60.0 * ((b - r) / delta + 2.0);
            }
            else
            {
                hue = 60.0 * ((r - g) / delta + 4.0);
            }
            if (hue < 0.0)
            {
                hue += 360.0;
            }
        }

        private static string FormatPresetDuration(int totalSeconds)
        {
            int minutes = Math.Max(1, totalSeconds / 60);
            if (minutes >= 60)
            {
                int hours = minutes / 60;
                int remainder = minutes % 60;
                return remainder == 0
                    ? hours + " h"
                    : hours + " h " + remainder + " min";
            }
            return minutes + " min";
        }

        private void DrawDashboard(Graphics graphics)
        {
            int left = 246;
            int width = 700;
            DrawText(
                graphics,
                Localization.Get("controlCenter"),
                titleFont,
                new Rectangle(left, 116, 500, 34),
                Color.FromArgb(248, 247, 243));
            DrawText(
                graphics,
                Localization.Get("controlSubtitle"),
                bodyFont,
                new Rectangle(left, 151, 560, 24),
                Color.FromArgb(145, 211, 215, 218));

            DrawSettingCard(
                graphics,
                "notification",
                new Rectangle(left, 195, width, 92),
                Localization.Get("notificationService"),
                paused
                    ? Localization.Get("notificationPaused")
                    : Localization.Get("notificationActive"),
                !paused);
            DrawSettingCard(
                graphics,
                "music",
                new Rectangle(left, 298, width, 92),
                Localization.Get("musicMode"),
                musicEnabled
                    ? Localization.Get("musicOn")
                    : Localization.Get("musicOff"),
                musicEnabled);
            DrawSettingCard(
                graphics,
                "persistent",
                new Rectangle(left, 401, width, 92),
                Localization.Get("musicPersistent"),
                musicPersistent
                    ? Localization.Get("persistentOn")
                    : Localization.Get("persistentOff"),
                musicPersistent);

            DrawSettingCard(
                graphics,
                "transparent",
                new Rectangle(left, 504, width, 92),
                Localization.Get("transparentWindow"),
                transparentWindow
                    ? Localization.Get("transparentOn")
                    : Localization.Get("transparentOff"),
                transparentWindow);

            DrawSettingCard(
                graphics,
                "quality",
                new Rectangle(left, 607, width, 92),
                Localization.Get("qualityEnhancement"),
                qualityEnhancement
                    ? Localization.Get("qualityOn")
                    : (transparentWindow
                        ? Localization.Get("qualityOff")
                        : Localization.Get("qualityRequiresGlass")),
                qualityEnhancement && transparentWindow,
                transparentWindow);

            int frostedY = 710;
            int frostedHeight = FrostedCardHeight;
            DrawFrostedGlassCard(
                graphics,
                new Rectangle(left, frostedY, width, frostedHeight));

            int opacityY = frostedY + frostedHeight + 11;
            int sizeY = opacityY + 103;
            int autoStartY = sizeY + 103;
            int movementY = autoStartY + 103;
            int clickThroughY = movementY + 103;
            int actionY = clickThroughY + 107;
            int utilityY = actionY + 60;
            int exitY = actionY + 118;
            int footerY = actionY + 183;

            DrawOpacityCard(
                graphics,
                new Rectangle(left, opacityY, width, 92));

            DrawSizeCard(
                graphics,
                new Rectangle(left, sizeY, width, 92));

            DrawSettingCard(
                graphics,
                "autoStart",
                new Rectangle(left, autoStartY, width, 92),
                Localization.Get("autoStart"),
                autoStart
                    ? Localization.Get("autoStartOn")
                    : Localization.Get("autoStartOff"),
                autoStart);

            DrawSettingCard(
                graphics,
                "movement",
                new Rectangle(left, movementY, width, 92),
                Localization.Get("windowMovement"),
                movementEnabled
                    ? Localization.Get("windowMovementOn")
                    : Localization.Get("windowMovementOff"),
                movementEnabled);

            DrawSettingCard(
                graphics,
                "clickThrough",
                new Rectangle(left, clickThroughY, width, 92),
                Localization.Get("antiOcclusion"),
                clickThroughEnabled
                    ? Localization.Get("antiOcclusionOn")
                    : Localization.Get("antiOcclusionOff"),
                clickThroughEnabled);

            DrawActionButton(
                graphics,
                "test",
                new Rectangle(left, actionY, 202, 48),
                Localization.Get("testNotification"),
                true);
            DrawActionButton(
                graphics,
                "history",
                new Rectangle(left + 218, actionY, 202, 48),
                Localization.Get("notificationHistory"),
                false);
            DrawActionButton(
                graphics,
                "filters",
                new Rectangle(left + 436, actionY, 202, 48),
                Localization.Get("appFilters"),
                false);

            int utilityWidth = 166;
            DrawActionButton(
                graphics,
                "export",
                new Rectangle(left, utilityY, utilityWidth, 46),
                Localization.Get("exportSettings"),
                false);
            DrawActionButton(
                graphics,
                "import",
                new Rectangle(left + 178, utilityY, utilityWidth, 46),
                Localization.Get("importSettings"),
                false);
            DrawActionButton(
                graphics,
                "reset",
                new Rectangle(left + 356, utilityY, utilityWidth, 46),
                Localization.Get("resetSettings"),
                false);
            DrawActionButton(
                graphics,
                "restore",
                new Rectangle(left + 534, utilityY, utilityWidth, 46),
                Localization.Get("notificationSettings"),
                false);

            DrawActionButton(
                graphics,
                "exit",
                new Rectangle(left, exitY, 202, 46),
                Localization.Get("exitApp"),
                false);

            DrawText(
                graphics,
                Localization.Get("footerHint"),
                smallFont,
                new Rectangle(left, footerY, width, 24),
                Color.FromArgb(122, 211, 211, 206));
        }

        private int FrostedCardHeight
        {
            get
            {
                return transparentWindow && frostedGlassEnabled
                    ? 152
                    : 92;
            }
        }

        private void DrawFrostedGlassCard(
            Graphics graphics,
            Rectangle bounds)
        {
            bool available = transparentWindow;
            DrawGlassCard(
                graphics,
                available ? "frosted" : "",
                bounds);
            DrawText(
                graphics,
                Localization.Get("frostedGlass"),
                sectionFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 17,
                    300,
                    24),
                Color.FromArgb(available ? 243 : 112, 242, 237));

            string description;
            if (!available)
            {
                description = Localization.Get("frostedRequiresGlass");
            }
            else
            {
                description = frostedGlassEnabled
                    ? Localization.Get("frostedOn")
                    : Localization.Get("frostedOff");
            }
            DrawText(
                graphics,
                description,
                smallFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 47,
                    bounds.Width - 125,
                    24),
                Color.FromArgb(available ? 145 : 82, 205, 209, 211));
            DrawToggle(
                graphics,
                new Rectangle(
                    bounds.Right - 76,
                    bounds.Top + 31,
                    54,
                    28),
                available && frostedGlassEnabled,
                available);

            if (!available || !frostedGlassEnabled)
            {
                return;
            }

            using (Pen divider = new Pen(
                Color.FromArgb(24, 255, 255, 255),
                1f))
            {
                graphics.DrawLine(
                    divider,
                    bounds.Left + 20,
                    bounds.Top + 91,
                    bounds.Right - 20,
                    bounds.Top + 91);
            }

            DrawText(
                graphics,
                Localization.Get("frostedIntensity"),
                smallFont,
                new Rectangle(
                    bounds.Left + 20,
                    bounds.Top + 99,
                    220,
                    22),
                Color.FromArgb(176, 219, 224, 228));

            int trackLeft = bounds.Left + 292;
            int trackWidth = bounds.Width - 430;
            int trackY = bounds.Top + 131;
            int knobX = trackLeft +
                (int)Math.Round(trackWidth * frostedIntensity);
            using (Pen track = new Pen(
                Color.FromArgb(45, 255, 255, 255),
                4f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                graphics.DrawLine(
                    track,
                    trackLeft,
                    trackY,
                    trackLeft + trackWidth,
                    trackY);
            }
            using (Pen progress = new Pen(
                Color.FromArgb(185, 206, 224, 240),
                4f))
            {
                progress.StartCap = LineCap.Round;
                progress.EndCap = LineCap.Round;
                graphics.DrawLine(
                    progress,
                    trackLeft,
                    trackY,
                    knobX,
                    trackY);
            }
            using (SolidBrush knob = new SolidBrush(
                hoverZone == "frostedIntensity" || draggingFrostedSlider
                    ? Color.White
                    : Color.FromArgb(235, 238, 238, 234)))
            {
                graphics.FillEllipse(
                    knob,
                    knobX - 7,
                    trackY - 7,
                    14,
                    14);
            }
            DrawText(
                graphics,
                ((int)Math.Round(frostedIntensity * 100)).ToString() + "%",
                bodyFont,
                new Rectangle(
                    bounds.Right - 112,
                    bounds.Top + 95,
                    88,
                    28),
                Color.FromArgb(230, 238, 235, 224),
                ContentAlignment.MiddleRight);
        }

        private void DrawOpacityCard(Graphics graphics, Rectangle bounds)
        {
            DrawGlassCard(graphics, "consoleOpacity", bounds);
            DrawText(
                graphics,
                Localization.Get("consoleTransparency"),
                sectionFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 10, 250, 24),
                Color.FromArgb(243, 242, 237));
            DrawText(
                graphics,
                Localization.Get("consoleTransparencyHint"),
                smallFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 34, 260, 22),
                Color.FromArgb(145, 205, 209, 211));

            int trackLeft = bounds.Left + 292;
            int trackWidth = bounds.Width - 430;
            int trackY = bounds.Top + 45;
            int knobX = trackLeft +
                (int)Math.Round(trackWidth * consoleOpacity);
            using (Pen track = new Pen(
                Color.FromArgb(45, 255, 255, 255),
                4f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                graphics.DrawLine(
                    track,
                    trackLeft,
                    trackY,
                    trackLeft + trackWidth,
                    trackY);
            }
            using (Pen progress = new Pen(
                Color.FromArgb(175, 152, 203, 221),
                4f))
            {
                progress.StartCap = LineCap.Round;
                progress.EndCap = LineCap.Round;
                graphics.DrawLine(
                    progress,
                    trackLeft,
                    trackY,
                    knobX,
                    trackY);
            }
            using (SolidBrush knob = new SolidBrush(
                hoverZone == "consoleOpacity" || draggingOpacitySlider
                    ? Color.White
                    : Color.FromArgb(235, 238, 238, 234)))
            {
                graphics.FillEllipse(
                    knob,
                    knobX - 7,
                    trackY - 7,
                    14,
                    14);
            }

            string[] labels = new string[] { "0%", "50%", "100%" };
            for (int index = 0; index < labels.Length; index++)
            {
                float x = trackLeft + trackWidth * index / 2f;
                DrawText(
                    graphics,
                    labels[index],
                    microFont,
                    new Rectangle(
                        (int)x - 34,
                        trackY + 12,
                        68,
                        17),
                    Color.FromArgb(
                        index == 1 ? 205 : 125,
                        224,
                        225,
                        219),
                    ContentAlignment.MiddleCenter);
            }
            DrawText(
                graphics,
                ((int)Math.Round(consoleOpacity * 100)).ToString() + "%",
                bodyFont,
                new Rectangle(bounds.Right - 112, bounds.Top + 31, 88, 28),
                Color.FromArgb(230, 238, 235, 224),
                ContentAlignment.MiddleRight);
        }

        private void DrawSizeCard(Graphics graphics, Rectangle bounds)
        {
            DrawGlassCard(graphics, "size", bounds);
            DrawText(
                graphics,
                Localization.Get("sizeTitle"),
                sectionFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 10, 180, 24),
                Color.FromArgb(243, 242, 237));
            DrawText(
                graphics,
                Localization.Get("sizeHint"),
                smallFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 34, 250, 22),
                Color.FromArgb(145, 205, 209, 211));

            int trackLeft = bounds.Left + 292;
            int trackWidth = bounds.Width - 430;
            int trackY = bounds.Top + 45;
            double normalized = (sizeScale - 0.80) / 0.40;
            int knobX = trackLeft + (int)Math.Round(trackWidth * normalized);
            using (Pen track = new Pen(
                Color.FromArgb(45, 255, 255, 255),
                4f))
            {
                track.StartCap = LineCap.Round;
                track.EndCap = LineCap.Round;
                graphics.DrawLine(
                    track,
                    trackLeft,
                    trackY,
                    trackLeft + trackWidth,
                    trackY);
            }
            using (Pen progress = new Pen(
                Color.FromArgb(175, 132, 212, 174),
                4f))
            {
                progress.StartCap = LineCap.Round;
                progress.EndCap = LineCap.Round;
                graphics.DrawLine(
                    progress,
                    trackLeft,
                    trackY,
                    knobX,
                    trackY);
            }
            using (SolidBrush knob = new SolidBrush(
                hoverZone == "size" || draggingSizeSlider
                    ? Color.White
                    : Color.FromArgb(235, 238, 238, 234)))
            {
                graphics.FillEllipse(
                    knob,
                    knobX - 7,
                    trackY - 7,
                    14,
                    14);
            }

            string[] labels = new string[]
            {
                Localization.Get("sizeSmall"),
                Localization.Get("sizeMedium"),
                Localization.Get("sizeLarge")
            };
            for (int index = 0; index < labels.Length; index++)
            {
                float x = trackLeft + trackWidth * index / 2f;
                DrawText(
                    graphics,
                    labels[index],
                    microFont,
                    new Rectangle(
                        (int)x - 34,
                        trackY + 12,
                        68,
                        17),
                    Color.FromArgb(
                        index == 1 ? 205 : 125,
                        224,
                        225,
                        219),
                    ContentAlignment.MiddleCenter);
            }
            DrawText(
                graphics,
                ((int)Math.Round(sizeScale * 100)).ToString() + "%",
                bodyFont,
                new Rectangle(bounds.Right - 112, bounds.Top + 31, 88, 28),
                Color.FromArgb(230, 238, 235, 224),
                ContentAlignment.MiddleRight);
        }

        private void DrawSettingCard(
            Graphics graphics,
            string zone,
            Rectangle bounds,
            string title,
            string description,
            bool enabled,
            bool available = true)
        {
            DrawGlassCard(graphics, available ? zone : "", bounds);
            DrawText(
                graphics,
                title,
                sectionFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 17, 300, 24),
                Color.FromArgb(available ? 243 : 112, 242, 237));
            DrawText(
                graphics,
                description,
                smallFont,
                new Rectangle(bounds.Left + 20, bounds.Top + 47, bounds.Width - 125, 24),
                Color.FromArgb(available ? 145 : 82, 205, 209, 211));
            DrawToggle(
                graphics,
                new Rectangle(bounds.Right - 76, bounds.Top + 31, 54, 28),
                enabled,
                available);
        }

        private void DrawAuthorPage(Graphics graphics)
        {
            int left = 246;
            DrawText(
                graphics,
                Localization.Get("aboutAuthor"),
                titleFont,
                new Rectangle(left, 116, 500, 34),
                Color.FromArgb(248, 247, 243));
            DrawText(
                graphics,
                Localization.Get("aboutSubtitle"),
                bodyFont,
                new Rectangle(left, 151, 560, 24),
                Color.FromArgb(145, 211, 215, 218));

            Rectangle card = new Rectangle(left, 195, 700, 338);
            DrawGlassCard(graphics, "authorPage", card);
            DrawLogo(
                graphics,
                new Rectangle(left + 28, card.Top + 28, 82, 82));
            DrawText(
                graphics,
                Localization.Get("appName"),
                titleFont,
                new Rectangle(left + 132, card.Top + 28, 430, 34),
                Color.FromArgb(248, 247, 243));
            DrawText(
                graphics,
                "Sendyun Technology Unit",
                bodyFont,
                new Rectangle(left + 132, card.Top + 62, 430, 24),
                Color.FromArgb(180, 214, 217, 220));
            DrawText(
                graphics,
                Localization.Get("projectDescription"),
                smallFont,
                new Rectangle(left + 132, card.Top + 88, 430, 22),
                Color.FromArgb(128, 210, 213, 214));
            DrawText(
                graphics,
                DisplayVersion,
                smallFont,
                new Rectangle(card.Right - 130, card.Top + 31, 102, 24),
                Color.FromArgb(174, 224, 231, 238),
                ContentAlignment.MiddleRight);

            using (Pen divider = new Pen(
                Color.FromArgb(35, 255, 255, 255),
                1f))
            {
                graphics.DrawLine(
                    divider,
                    left + 28,
                    card.Top + 135,
                    card.Right - 28,
                    card.Top + 135);
            }

            DrawText(
                graphics,
                Localization.Get("author"),
                microFont,
                new Rectangle(left + 28, card.Top + 160, 150, 18),
                Color.FromArgb(120, 211, 211, 207));
            DrawText(
                graphics,
                "Mr.Dove",
                sectionFont,
                new Rectangle(left + 28, card.Top + 181, 260, 28),
                Color.FromArgb(245, 244, 239));
            DrawText(
                graphics,
                Localization.Get("feedback"),
                microFont,
                new Rectangle(left + 28, card.Top + 218, 240, 18),
                Color.FromArgb(120, 211, 211, 207));
            DrawText(
                graphics,
                "Mr.Dove@aliyun.com",
                bodyFont,
                new Rectangle(left + 28, card.Top + 239, 360, 28),
                Color.FromArgb(147, 209, 226, 245));
            Rectangle githubButton = new Rectangle(
                left + 28,
                card.Top + 263,
                30,
                30);
            if (githubLogo != null)
            {
                graphics.DrawImage(
                    githubLogo,
                    githubButton);
            }

            Rectangle sponsorButton = new Rectangle(
                card.Right - 208,
                card.Top + 255,
                180,
                40);
            using (GraphicsPath path = RoundedRectangle(
                sponsorButton,
                20))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(
                    hoverZone == "sponsor" ? 54 : 28,
                    222,
                    185,
                    116)))
            using (Pen border = new Pen(
                Color.FromArgb(
                    hoverZone == "sponsor" ? 140 : 72,
                    236,
                    203,
                    139),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawCoffeeCup(
                graphics,
                new Rectangle(
                    sponsorButton.Left + 14,
                    sponsorButton.Top + 9,
                    20,
                    22),
                Color.FromArgb(235, 238, 207, 151));
            DrawText(
                graphics,
                Localization.Get("sponsorCoffeeButton"),
                smallFont,
                new Rectangle(
                    sponsorButton.Left + 40,
                    sponsorButton.Top,
                    sponsorButton.Width - 48,
                    sponsorButton.Height),
                Color.FromArgb(240, 239, 226, 196),
                ContentAlignment.MiddleLeft);

            DrawText(
                graphics,
                Localization.Get("emailHint"),
                smallFont,
                new Rectangle(left + 28, card.Top + 301, 440, 22),
                Color.FromArgb(120, 206, 208, 206));
        }

        private static void DrawCoffeeCup(
            Graphics graphics,
            Rectangle bounds,
            Color color)
        {
            using (Pen pen = new Pen(color, 1.5f))
            {
                Rectangle cup = new Rectangle(
                    bounds.Left + 1,
                    bounds.Top + 6,
                    bounds.Width - 7,
                    bounds.Height - 9);
                graphics.DrawRectangle(pen, cup);
                graphics.DrawArc(
                    pen,
                    bounds.Right - 10,
                    bounds.Top + 9,
                    9,
                    11,
                    -90,
                    180);
                graphics.DrawLine(
                    pen,
                    bounds.Left + 4,
                    bounds.Bottom - 1,
                    bounds.Right - 8,
                    bounds.Bottom - 1);
                graphics.DrawLine(
                    pen,
                    bounds.Left + 6,
                    bounds.Top + 1,
                    bounds.Left + 6,
                    bounds.Top + 5);
                graphics.DrawLine(
                    pen,
                    bounds.Left + 11,
                    bounds.Top,
                    bounds.Left + 11,
                    bounds.Top + 4);
            }
        }

        private void DrawGlassCard(
            Graphics graphics,
            string zone,
            Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 16))
            using (LinearGradientBrush fill = new LinearGradientBrush(
                bounds,
                Color.FromArgb(
                    hoverZone == zone ? 29 : 19,
                    255,
                    255,
                    255),
                Color.FromArgb(9, 255, 255, 255),
                115f))
            using (Pen border = new Pen(
                Color.FromArgb(
                    hoverZone == zone ? 52 : 31,
                    255,
                    255,
                    255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
        }

        private void DrawToggle(
            Graphics graphics,
            Rectangle bounds,
            bool enabled,
            bool available = true)
        {
            using (GraphicsPath path = RoundedRectangle(
                bounds,
                bounds.Height / 2))
            using (SolidBrush fill = new SolidBrush(
                enabled
                    ? Color.FromArgb(112, 123, 211, 171)
                    : Color.FromArgb(
                        available ? 52 : 24,
                        255,
                        255,
                        255)))
            {
                graphics.FillPath(fill, path);
            }
            int knob = bounds.Height - 8;
            int x = enabled ? bounds.Right - knob - 4 : bounds.Left + 4;
            using (SolidBrush knobBrush = new SolidBrush(
                enabled
                    ? Color.White
                    : Color.FromArgb(
                        available ? 175 : 72,
                        226,
                        226,
                        224)))
            {
                graphics.FillEllipse(knobBrush, x, bounds.Top + 4, knob, knob);
            }
        }

        private void DrawActionButton(
            Graphics graphics,
            string zone,
            Rectangle bounds,
            string label,
            bool primary)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 12))
            using (SolidBrush fill = new SolidBrush(
                primary
                    ? Color.FromArgb(
                        hoverZone == zone ? 185 : 150,
                        219,
                        226,
                        237)
                    : Color.FromArgb(
                        hoverZone == zone ? 32 : 18,
                        255,
                        255,
                        255)))
            {
                graphics.FillPath(fill, path);
            }
            DrawText(
                graphics,
                label,
                bodyFont,
                bounds,
                primary
                    ? Color.FromArgb(23, 27, 33)
                    : Color.FromArgb(224, 232, 232, 228),
                ContentAlignment.MiddleCenter);
        }

        private void DrawNavButton(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string zone,
            bool selected)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 11))
            using (SolidBrush fill = new SolidBrush(
                selected
                    ? Color.FromArgb(32, 255, 255, 255)
                    : Color.FromArgb(
                        hoverZone == zone ? 20 : 0,
                        255,
                        255,
                        255)))
            {
                graphics.FillPath(fill, path);
            }
            DrawText(
                graphics,
                label,
                bodyFont,
                new Rectangle(bounds.Left + 16, bounds.Top, bounds.Width - 24, bounds.Height),
                selected
                    ? Color.FromArgb(245, 244, 240)
                    : Color.FromArgb(150, 206, 209, 209),
                ContentAlignment.MiddleLeft);
        }

        private void DrawPerformanceNavButton(
            Graphics graphics,
            Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 11))
            using (SolidBrush fill = new SolidBrush(
                performancePage
                    ? Color.FromArgb(33, 255, 255, 255)
                    : Color.FromArgb(
                        hoverZone == "navPerformance" ? 20 : 0,
                        255,
                        255,
                        255)))
            {
                graphics.FillPath(fill, path);
            }
            DrawPerformanceGlyph(
                graphics,
                new Rectangle(
                    bounds.Left + 13,
                    bounds.Top + 12,
                    18,
                    20),
                performancePage
                    ? Color.FromArgb(245, 225, 198, 132)
                    : Color.FromArgb(145, 210, 212, 211));
            DrawText(
                graphics,
                Localization.Get("fpsMonitorShort"),
                smallFont,
                new Rectangle(
                    bounds.Left + 40,
                    bounds.Top,
                    bounds.Width - 48,
                    bounds.Height),
                performancePage
                    ? Color.FromArgb(245, 244, 240)
                    : Color.FromArgb(150, 206, 209, 209),
                ContentAlignment.MiddleLeft);
            if (performanceSnapshot != null)
            {
                DrawText(
                    graphics,
                    performanceSnapshot.CurrentFps.ToString("0"),
                    microFont,
                    new Rectangle(
                        bounds.Right - 42,
                        bounds.Top,
                        30,
                        bounds.Height),
                    PerformanceColor(performanceSnapshot.Health),
                    ContentAlignment.MiddleRight);
            }
        }

        private static void DrawPerformanceGlyph(
            Graphics graphics,
            Rectangle bounds,
            Color color)
        {
            using (Pen pen = new Pen(color, 1.4f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(
                    pen,
                    bounds.Left,
                    bounds.Bottom - 3,
                    bounds.Right,
                    bounds.Bottom - 3);
                float step = bounds.Width / 4f;
                graphics.DrawLines(
                    pen,
                    new PointF[]
                    {
                        new PointF(bounds.Left + 1, bounds.Top + 15),
                        new PointF(bounds.Left + step, bounds.Top + 9),
                        new PointF(bounds.Left + step * 2, bounds.Top + 12),
                        new PointF(bounds.Left + step * 3, bounds.Top + 3),
                        new PointF(bounds.Right - 1, bounds.Top + 6)
                    });
            }
        }

        private void DrawTimerNavButton(
            Graphics graphics,
            Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 11))
            using (SolidBrush fill = new SolidBrush(
                timerPage
                    ? Color.FromArgb(33, 255, 255, 255)
                    : Color.FromArgb(
                        hoverZone == "navTimer" ? 20 : 0,
                        255,
                        255,
                        255)))
            {
                graphics.FillPath(fill, path);
            }
            Color accent = timerSnapshot != null && timerSnapshot.IsActive
                ? timerSnapshot.AccentColor
                : Color.FromArgb(145, 210, 212, 211);
            DrawMiniHourglass(
                graphics,
                new Rectangle(
                    bounds.Left + 14,
                    bounds.Top + 11,
                    20,
                    22),
                accent);
            DrawText(
                graphics,
                Localization.Get("timer"),
                smallFont,
                new Rectangle(
                    bounds.Left + 42,
                    bounds.Top,
                    bounds.Width - 50,
                    bounds.Height),
                timerPage
                    ? Color.FromArgb(245, 244, 240)
                    : Color.FromArgb(150, 206, 209, 209),
                ContentAlignment.MiddleLeft);
            if (timerSnapshot != null && timerSnapshot.IsActive)
            {
                DrawText(
                    graphics,
                    timerSnapshot.FormattedTime,
                    microFont,
                    new Rectangle(
                        bounds.Right - 70,
                        bounds.Top,
                        58,
                        bounds.Height),
                    accent,
                    ContentAlignment.MiddleRight);
            }
        }

        private static void DrawMiniHourglass(
            Graphics graphics,
            Rectangle bounds,
            Color color)
        {
            PointF[] upper = new PointF[]
            {
                new PointF(bounds.Left + 3, bounds.Top + 2),
                new PointF(bounds.Right - 3, bounds.Top + 2),
                new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f)
            };
            PointF[] lower = new PointF[]
            {
                new PointF(bounds.Left + bounds.Width / 2f, bounds.Top + bounds.Height / 2f),
                new PointF(bounds.Right - 3, bounds.Bottom - 2),
                new PointF(bounds.Left + 3, bounds.Bottom - 2)
            };
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPolygon(brush, upper);
                graphics.FillPolygon(brush, lower);
            }
        }

        private void DrawShortcut(
            Graphics graphics,
            int x,
            int y,
            string keys,
            string label)
        {
            using (GraphicsPath path = RoundedRectangle(
                new Rectangle(x, y, 164, 29),
                8))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(18, 255, 255, 255)))
            using (Pen border = new Pen(
                Color.FromArgb(30, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                keys,
                microFont,
                new Rectangle(x + 10, y, 92, 29),
                Color.FromArgb(205, 232, 231, 226),
                ContentAlignment.MiddleLeft);
            DrawText(
                graphics,
                label,
                microFont,
                new Rectangle(x + 98, y, 62, 29),
                Color.FromArgb(115, 210, 212, 210),
                ContentAlignment.MiddleRight);
        }

        private void DrawWindowButton(
            Graphics graphics,
            Rectangle bounds,
            string kind)
        {
            Color color = hoverZone == kind
                ? Color.FromArgb(42, 255, 255, 255)
                : Color.Transparent;
            using (SolidBrush fill = new SolidBrush(color))
            using (Pen pen = new Pen(Color.FromArgb(185, 235, 235, 232), 1.5f))
            {
                graphics.FillEllipse(fill, bounds);
                if (kind == "close")
                {
                    graphics.DrawLine(
                        pen,
                        bounds.Left + 11,
                        bounds.Top + 10,
                        bounds.Right - 11,
                        bounds.Bottom - 10);
                    graphics.DrawLine(
                        pen,
                        bounds.Right - 11,
                        bounds.Top + 10,
                        bounds.Left + 11,
                        bounds.Bottom - 10);
                }
                else
                {
                    graphics.DrawLine(
                        pen,
                        bounds.Left + 10,
                        bounds.Bottom - 10,
                        bounds.Right - 10,
                        bounds.Bottom - 10);
                }
            }
        }

        private void DrawLanguageButton(Graphics graphics, Rectangle bounds)
        {
            bool active = hoverZone == "language" || languageMenuOpen;
            using (GraphicsPath path = RoundedRectangle(bounds, 10))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(active ? 34 : 16, 255, 255, 255)))
            using (Pen border = new Pen(
                Color.FromArgb(active ? 52 : 28, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            DrawText(
                graphics,
                Localization.DisplayName(Localization.Current),
                smallFont,
                new Rectangle(bounds.Left + 12, bounds.Top, 106, bounds.Height),
                Color.FromArgb(210, 233, 233, 229),
                ContentAlignment.MiddleLeft);
            using (Pen chevron = new Pen(
                Color.FromArgb(150, 228, 228, 224),
                1.5f))
            {
                graphics.DrawLines(
                    chevron,
                    new Point[]
                    {
                        new Point(bounds.Right - 20, bounds.Top + 12),
                        new Point(bounds.Right - 16, bounds.Top + 16),
                        new Point(bounds.Right - 12, bounds.Top + 12)
                    });
            }
        }

        private void DrawLanguageMenu(Graphics graphics)
        {
            UiLanguage[] languages = Localization.Languages;
            Rectangle menuBounds = new Rectangle(720, 64, 150, 16 + languages.Length * 31);
            using (GraphicsPath path = RoundedRectangle(menuBounds, 12))
            using (SolidBrush fill = new SolidBrush(
                Color.FromArgb(246, 17, 20, 27)))
            using (Pen border = new Pen(
                Color.FromArgb(62, 255, 255, 255),
                1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }

            for (int index = 0; index < languages.Length; index++)
            {
                Rectangle row = new Rectangle(
                    menuBounds.Left + 6,
                    menuBounds.Top + 8 + index * 31,
                    menuBounds.Width - 12,
                    28);
                if (hoverZone == "language:" + index)
                {
                    using (GraphicsPath rowPath = RoundedRectangle(row, 7))
                    using (SolidBrush highlight = new SolidBrush(
                        Color.FromArgb(28, 255, 255, 255)))
                    {
                        graphics.FillPath(highlight, rowPath);
                    }
                }
                DrawText(
                    graphics,
                    Localization.DisplayName(languages[index]),
                    smallFont,
                    new Rectangle(row.Left + 10, row.Top, row.Width - 30, row.Height),
                    Color.FromArgb(223, 235, 234, 230),
                    ContentAlignment.MiddleLeft);
                if (languages[index] == Localization.Current)
                {
                    DrawText(
                        graphics,
                        "✓",
                        bodyFont,
                        new Rectangle(row.Right - 26, row.Top, 20, row.Height),
                        Color.FromArgb(165, 228, 198, 133),
                        ContentAlignment.MiddleCenter);
                }
            }
        }

        private const double MaxContentScroll = 940.0;

        private Rectangle ScrollTrackBounds
        {
            get { return new Rectangle(960, 112, 8, 572); }
        }

        private int ScrollThumbHeight
        {
            get
            {
                int viewport = 598;
                double ratio = viewport / (viewport + MaxContentScroll);
                return Math.Max(56, (int)Math.Round(572 * ratio));
            }
        }

        private int ScrollThumbTop
        {
            get
            {
                int available = 572 - ScrollThumbHeight;
                double ratio = MaxContentScroll <= 0.0
                    ? 0.0
                    : contentScroll / MaxContentScroll;
                return 112 + (int)Math.Round(available * ratio);
            }
        }

        private void DrawScrollbar(Graphics graphics)
        {
            Rectangle track = ScrollTrackBounds;
            using (GraphicsPath trackPath = RoundedRectangle(track, 4))
            using (SolidBrush trackFill = new SolidBrush(
                Color.FromArgb(18, 255, 255, 255)))
            {
                graphics.FillPath(trackFill, trackPath);
            }

            Rectangle thumb = new Rectangle(
                track.Left,
                ScrollThumbTop,
                track.Width,
                ScrollThumbHeight);
            using (GraphicsPath thumbPath = RoundedRectangle(thumb, 4))
            using (SolidBrush thumbFill = new SolidBrush(
                Color.FromArgb(
                    draggingScrollbar || hoverZone == "scrollbar"
                        ? 112
                        : 62,
                    230,
                    231,
                    228)))
            {
                graphics.FillPath(thumbFill, thumbPath);
            }
        }

        private void DrawLogo(Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(
                bounds,
                Math.Max(4, bounds.Width / 7)))
            {
                GraphicsState state = graphics.Save();
                graphics.SetClip(path);
                DrawBitmapFade(
                    graphics,
                    logoLight,
                    bounds,
                    1.0 - logoBlend);
                DrawBitmapFade(
                    graphics,
                    logoDark,
                    bounds,
                    logoBlend);
                graphics.Restore(state);
            }
        }

        private static void DrawBitmapFade(
            Graphics graphics,
            Bitmap bitmap,
            Rectangle bounds,
            double opacity)
        {
            if (bitmap == null || opacity <= 0.002)
            {
                return;
            }
            ColorMatrix matrix = new ColorMatrix();
            matrix.Matrix33 = (float)Math.Max(
                0.0,
                Math.Min(1.0, opacity));
            using (ImageAttributes attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(
                    bitmap,
                    bounds,
                    0,
                    0,
                    bitmap.Width,
                    bitmap.Height,
                    GraphicsUnit.Pixel,
                    attributes);
            }
        }

        private void DrawText(
            Graphics graphics,
            string text,
            Font font,
            Rectangle bounds,
            Color color,
            ContentAlignment alignment = ContentAlignment.MiddleLeft)
        {
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Alignment =
                    alignment == ContentAlignment.MiddleCenter ||
                    alignment == ContentAlignment.TopCenter
                        ? StringAlignment.Center
                        : (alignment == ContentAlignment.MiddleRight ||
                           alignment == ContentAlignment.TopRight
                            ? StringAlignment.Far
                            : StringAlignment.Near);
                format.LineAlignment =
                    alignment == ContentAlignment.MiddleLeft ||
                    alignment == ContentAlignment.MiddleCenter ||
                    alignment == ContentAlignment.MiddleRight
                        ? StringAlignment.Center
                        : StringAlignment.Near;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private void OnConsoleMouseMove(object sender, MouseEventArgs e)
        {
            Point logical = ToLogical(e.Location);
            if (draggingScrollbar)
            {
                UpdateScrollFromPoint(logical.Y);
                return;
            }
            Point contentPoint = new Point(
                logical.X,
                logical.Y + (int)Math.Round(contentScroll));
            if (draggingTimerWheel)
            {
                UpdateTimerColorFromWheel(contentPoint);
                return;
            }
            if (draggingTimerBrightness)
            {
                UpdateTimerBrightnessFromPoint(contentPoint.Y);
                return;
            }
            if (draggingTimerColumn >= 0)
            {
                int limit = draggingTimerColumn == 0 ? 24 : 60;
                int delta = (timerDragStartY - contentPoint.Y) / 34;
                int adjusted = (timerDragStartValue + delta) % limit;
                if (adjusted < 0)
                {
                    adjusted += limit;
                }
                SetTimerColumnValue(draggingTimerColumn, adjusted);
                return;
            }
            if (draggingSizeSlider)
            {
                UpdateSizeFromPoint(logical.X);
                return;
            }
            if (draggingFrostedSlider)
            {
                UpdateFrostedIntensityFromPoint(logical.X);
                return;
            }
            string next = HitZone(logical);
            if (next != hoverZone)
            {
                string previous = hoverZone;
                hoverZone = next;
                Cursor = next.Length > 0 ? Cursors.Hand : Cursors.Default;
                if (NeedsHoverRender(previous) || NeedsHoverRender(next))
                {
                    hoverRenderTimer.Stop();
                    hoverRenderTimer.Start();
                }
            }
        }

        private void OnConsoleMouseLeave(object sender, EventArgs e)
        {
            if (hoverZone.Length == 0)
            {
                return;
            }
            string previous = hoverZone;
            hoverZone = "";
            Cursor = Cursors.Default;
            if (NeedsHoverRender(previous))
            {
                hoverRenderTimer.Stop();
                hoverRenderTimer.Start();
            }
        }

        private static bool NeedsHoverRender(string zone)
        {
            if (string.IsNullOrEmpty(zone))
            {
                return false;
            }
            return zone == "language" ||
                zone.StartsWith("language:", StringComparison.Ordinal) ||
                zone == "close" ||
                zone == "minimize" ||
                zone == "logo" ||
                zone == "navDashboard" ||
                zone == "navAuthor" ||
                zone == "navPerformance" ||
                zone == "navTimer" ||
                zone.StartsWith("fpsLimit", StringComparison.Ordinal) ||
                zone.StartsWith("timer", StringComparison.Ordinal) ||
                zone == "installationInfo" ||
                zone == "author" ||
                zone == "sponsor" ||
                zone == "scrollbar" ||
                zone == "frosted" ||
                zone == "frostedIntensity" ||
                zone == "test" ||
                zone == "history" ||
                zone == "filters" ||
                zone == "export" ||
                zone == "import" ||
                zone == "reset" ||
                zone == "restore" ||
                zone == "exit" ||
                zone == "email";
        }

        private void OnConsoleMouseDown(object sender, MouseEventArgs e)
        {
            Point logical = ToLogical(e.Location);
            if (e.Button == MouseButtons.Left &&
                authorPage &&
                HitZone(logical) == "sponsor")
            {
                Raise(SponsorRequested);
                return;
            }
            if (e.Button == MouseButtons.Left &&
                !performancePage &&
                new Rectangle(
                    ScrollTrackBounds.Left - 4,
                    ScrollTrackBounds.Top,
                    ScrollTrackBounds.Width + 8,
                    ScrollTrackBounds.Height).Contains(logical))
            {
                draggingScrollbar = true;
                UpdateScrollFromPoint(logical.Y);
                return;
            }
            Point contentPoint = new Point(
                logical.X,
                logical.Y + (int)Math.Round(contentScroll));
            if (e.Button == MouseButtons.Left &&
                timerPage &&
                TimerColorWheelBounds.Contains(contentPoint))
            {
                draggingTimerWheel = true;
                Capture = true;
                UpdateTimerColorFromWheel(contentPoint);
                return;
            }
            int timeColumn = TimerColumnAtPoint(contentPoint);
            if (e.Button == MouseButtons.Left && timeColumn >= 0)
            {
                draggingTimerColumn = timeColumn;
                timerDragStartY = contentPoint.Y;
                timerDragStartValue = TimerColumnValue(timeColumn);
                Capture = true;
                return;
            }
            if (e.Button == MouseButtons.Left &&
                timerPage &&
                TimerBrightnessBounds.Contains(contentPoint))
            {
                draggingTimerBrightness = true;
                Capture = true;
                UpdateTimerBrightnessFromPoint(contentPoint.Y);
                return;
            }
            if (e.Button == MouseButtons.Left &&
                !authorPage &&
                !performancePage &&
                !timerPage &&
                transparentWindow &&
                frostedGlassEnabled &&
                new Rectangle(
                    246,
                    710 + 92,
                    700,
                    Math.Max(1, FrostedCardHeight - 92)).Contains(contentPoint))
            {
                draggingFrostedSlider = true;
                UpdateFrostedIntensityFromPoint(logical.X);
                return;
            }
            int opacityY = 710 + FrostedCardHeight + 11;
            if (e.Button == MouseButtons.Left &&
                !authorPage &&
                !performancePage &&
                !timerPage &&
                new Rectangle(246, opacityY, 700, 92).Contains(contentPoint))
            {
                draggingOpacitySlider = true;
                UpdateOpacityFromPoint(logical.X);
                return;
            }
            int sizeY = opacityY + 103;
            if (e.Button == MouseButtons.Left &&
                !authorPage &&
                !performancePage &&
                !timerPage &&
                new Rectangle(246, sizeY, 700, 92).Contains(contentPoint))
            {
                draggingSizeSlider = true;
                UpdateSizeFromPoint(logical.X);
                return;
            }
            if (e.Button == MouseButtons.Left &&
                logical.Y < HeaderHeight &&
                HitZone(logical).Length == 0)
            {
                ReleaseCapture();
                SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            }
        }

        private void OnConsoleMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingSizeSlider = false;
                draggingOpacitySlider = false;
                draggingFrostedSlider = false;
                draggingTimerWheel = false;
                draggingTimerBrightness = false;
                draggingTimerColumn = -1;
                draggingScrollbar = false;
                Capture = false;
                FlushRender();
            }
        }

        private void OnConsoleMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta == 0 || authorPage || performancePage)
            {
                return;
            }
            if (timerPage)
            {
                Point contentPoint = new Point(
                    ToLogical(e.Location).X,
                    ToLogical(e.Location).Y +
                        (int)Math.Round(contentScroll));
                int column = TimerColumnAtPoint(contentPoint);
                if (column >= 0)
                {
                    int value = TimerColumnValue(column) +
                        (e.Delta > 0 ? 1 : -1);
                    int limit = column == 0 ? 24 : 60;
                    if (value >= limit)
                    {
                        value = 0;
                    }
                    else if (value < 0)
                    {
                        value = limit - 1;
                    }
                    SetTimerColumnValue(column, value);
                    RequestRender();
                    return;
                }
                if (timerSetupMode == SmartTimerMode.Stopwatch &&
                    TimerLapsBounds.Contains(contentPoint) &&
                    timerSnapshot != null &&
                    timerSnapshot.Laps != null &&
                    timerSnapshot.Laps.Length > 3)
                {
                    double maximum = Math.Max(
                        0.0,
                        Math.Min(24, timerSnapshot.Laps.Length) * 59.0 -
                        TimerLapsBounds.Height);
                    timerLapScroll = Math.Max(
                        0.0,
                        Math.Min(
                            maximum,
                            timerLapScroll +
                            (e.Delta > 0 ? -54.0 : 54.0)));
                    RequestRender();
                    return;
                }
            }
            contentScroll = Math.Max(
                0.0,
                Math.Min(
                    MaxContentScroll,
                    contentScroll - Math.Sign(e.Delta) * 36.0));
            RequestRender();
        }

        private void OnConsoleMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            Point logical = ToLogical(e.Location);
            if (languageMenuOpen)
            {
                string languageZone = HitZone(logical);
                if (languageZone.StartsWith("language:", StringComparison.Ordinal))
                {
                    int index = int.Parse(languageZone.Substring(9));
                    UiLanguage[] languages = Localization.Languages;
                    if (index >= 0 && index < languages.Length)
                    {
                        EventHandler<UiLanguage> languageHandler =
                            LanguageChangeRequested;
                        if (languageHandler != null)
                        {
                            languageHandler(this, languages[index]);
                        }
                    }
                }
                languageMenuOpen = false;
                RequestRender();
                return;
            }

            string zone = HitZone(logical);
            if (zone == "scrollbar")
            {
                return;
            }
            if (zone.StartsWith("fpsLimit", StringComparison.Ordinal))
            {
                int requestedRate;
                if (int.TryParse(
                    zone.Substring("fpsLimit".Length),
                    out requestedRate))
                {
                    frameRateLimit = Math.Max(
                        60,
                        Math.Min(165, requestedRate));
                    EventHandler<int> handler = FrameRateLimitChanged;
                    if (handler != null)
                    {
                        handler(this, frameRateLimit);
                    }
                    RequestRender();
                }
                return;
            }
            if (zone == "timerModeCountdown")
            {
                timerSetupMode = SmartTimerMode.Countdown;
                RequestRender();
                return;
            }
            if (zone == "timerModeStopwatch")
            {
                timerSetupMode = SmartTimerMode.Stopwatch;
                RequestRender();
                return;
            }
            if (zone == "timerStartCountdown")
            {
                int seconds = timerHours * 3600 +
                    timerMinutes * 60 +
                    timerSeconds;
                if (seconds < 1)
                {
                    seconds = 1;
                }
                TimerStartRequest request = new TimerStartRequest();
                request.Mode = SmartTimerMode.Countdown;
                request.Duration = TimeSpan.FromSeconds(seconds);
                EventHandler<TimerStartRequest> handler = TimerStartRequested;
                if (handler != null)
                {
                    handler(this, request);
                }
                return;
            }
            if (zone == "timerStartStopwatch")
            {
                TimerStartRequest request = new TimerStartRequest();
                request.Mode = SmartTimerMode.Stopwatch;
                request.Duration = TimeSpan.Zero;
                EventHandler<TimerStartRequest> handler = TimerStartRequested;
                if (handler != null)
                {
                    handler(this, request);
                }
                return;
            }
            if (zone == "timerPause")
            {
                EventHandler<SmartTimerMode> handler =
                    TimerPauseToggleRequested;
                if (handler != null)
                {
                    handler(this, timerSetupMode);
                }
                return;
            }
            if (zone == "timerCancel")
            {
                EventHandler<SmartTimerMode> handler =
                    TimerCancelRequested;
                if (handler != null)
                {
                    handler(this, timerSetupMode);
                }
                return;
            }
            if (zone == "timerLap")
            {
                EventHandler<SmartTimerMode> handler =
                    TimerLapRequested;
                if (handler != null)
                {
                    handler(this, SmartTimerMode.Stopwatch);
                }
                return;
            }
            if (zone == "timerAddPreset")
            {
                using (TimerPresetDialog dialog = new TimerPresetDialog(
                    timerHours * 3600 +
                    timerMinutes * 60 +
                    timerSeconds))
                {
                    if (timerPresets.Length >= 6)
                    {
                        return;
                    }
                    if (dialog.ShowDialog(this) == DialogResult.OK &&
                        dialog.Preset != null)
                    {
                        List<TimerPreset> next =
                            new List<TimerPreset>(timerPresets);
                        next.Add(dialog.Preset);
                        timerPresets = next.ToArray();
                        EventHandler<TimerPreset> handler =
                            TimerPresetAddRequested;
                        if (handler != null)
                        {
                            handler(this, dialog.Preset);
                        }
                        RequestRender();
                    }
                }
                return;
            }
            if (zone.StartsWith(
                "timerPresetDelete",
                StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(
                    zone.Substring("timerPresetDelete".Length),
                    out index) &&
                    index >= 0 &&
                    index < timerPresets.Length)
                {
                    List<TimerPreset> next =
                        new List<TimerPreset>(timerPresets);
                    next.RemoveAt(index);
                    timerPresets = next.ToArray();
                    EventHandler<int> handler = TimerPresetDeleteRequested;
                    if (handler != null)
                    {
                        handler(this, index);
                    }
                    RequestRender();
                }
                return;
            }
            if (zone.StartsWith(
                "timerPreset",
                StringComparison.Ordinal))
            {
                int index;
                if (int.TryParse(
                    zone.Substring("timerPreset".Length),
                    out index) &&
                    index >= 0 &&
                    index < timerPresets.Length)
                {
                    timerSetupMode = SmartTimerMode.Countdown;
                    int total = Math.Max(
                        1,
                        timerPresets[index].DurationSeconds);
                    timerHours = Math.Min(23, total / 3600);
                    timerMinutes = (total % 3600) / 60;
                    timerSeconds = total % 60;
                    StartTimerWheelAnimation();
                    TimerStartRequest request = new TimerStartRequest();
                    request.Mode = SmartTimerMode.Countdown;
                    request.Duration = TimeSpan.FromSeconds(
                        timerPresets[index].DurationSeconds);
                    EventHandler<TimerStartRequest> handler =
                        TimerStartRequested;
                    if (handler != null)
                    {
                        handler(this, request);
                    }
                }
                return;
            }
            if (zone == "timerRgb")
            {
                using (RgbColorDialog dialog =
                    new RgbColorDialog(timerAccentColor))
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        ApplyTimerColor(dialog.SelectedColor);
                    }
                }
                return;
            }
            if (zone == "timerSoundBrowse")
            {
                Raise(TimerSoundBrowseRequested);
                return;
            }
            if (zone == "timerSoundPreview")
            {
                Raise(TimerSoundPreviewRequested);
                return;
            }
            if (zone == "timerSoundReset")
            {
                Raise(TimerSoundResetRequested);
                return;
            }
            if (zone == "size" ||
                zone == "consoleOpacity" ||
                zone == "frostedIntensity")
            {
                return;
            }
            else if (zone == "language")
            {
                languageMenuOpen = true;
                RequestRender();
            }
            else if (zone == "close")
            {
                Hide();
            }
            else if (zone == "logo")
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        "https://cnsendyun.com");
                }
                catch (Exception exception)
                {
                    Logger.Write("Open official website: " + exception);
                }
            }
            else if (zone == "minimize")
            {
                WindowState = FormWindowState.Minimized;
            }
            else if (zone == "navDashboard")
            {
                authorPage = false;
                performancePage = false;
                timerPage = false;
                performanceTimer.Interval = 500;
                contentScroll = 0.0;
                RequestRender();
            }
            else if (zone == "navAuthor" || zone == "author")
            {
                authorPage = true;
                performancePage = false;
                timerPage = false;
                performanceTimer.Interval = 500;
                contentScroll = 0.0;
                if (installationTextNeedsScroll && Visible)
                {
                    installationMarqueeTimer.Start();
                }
                RequestRender();
            }
            else if (zone == "navPerformance")
            {
                authorPage = false;
                performancePage = true;
                timerPage = false;
                contentScroll = 0.0;
                performanceHistory.Clear();
                performanceSnapshot = null;
                performanceTimer.Interval = 350;
                performanceTimer.Start();
                UpdatePerformance();
                RequestRender();
            }
            else if (zone == "navTimer")
            {
                authorPage = false;
                performancePage = false;
                timerPage = true;
                contentScroll = 0.0;
                timerWheelValues[0] = timerHours;
                timerWheelValues[1] = timerMinutes;
                timerWheelValues[2] = timerSeconds;
                performanceTimer.Interval = 500;
                performanceTimer.Start();
                RequestRender();
            }
            else if (zone == "installationInfo")
            {
                ShowInstallationInfo();
            }
            else if (zone == "notification")
            {
                Raise(PauseToggleRequested);
            }
            else if (zone == "music")
            {
                Raise(MusicToggleRequested);
            }
            else if (zone == "persistent")
            {
                Raise(PersistentToggleRequested);
            }
            else if (zone == "transparent")
            {
                Raise(GlassModeToggleRequested);
            }
            else if (zone == "quality")
            {
                if (transparentWindow)
                {
                    Raise(QualityEnhanceToggleRequested);
                }
            }
            else if (zone == "frosted")
            {
                if (transparentWindow)
                {
                    Raise(FrostedGlassToggleRequested);
                }
            }
            else if (zone == "autoStart")
            {
                Raise(AutoStartToggleRequested);
            }
            else if (zone == "movement")
            {
                Raise(WindowMovementToggleRequested);
            }
            else if (zone == "clickThrough")
            {
                Raise(ClickThroughToggleRequested);
            }
            else if (zone == "test")
            {
                Raise(TestNotificationRequested);
            }
            else if (zone == "history")
            {
                Raise(NotificationHistoryRequested);
            }
            else if (zone == "filters")
            {
                Raise(AppFiltersRequested);
            }
            else if (zone == "export")
            {
                Raise(ExportSettingsRequested);
            }
            else if (zone == "import")
            {
                Raise(ImportSettingsRequested);
            }
            else if (zone == "reset")
            {
                Raise(ResetSettingsRequested);
            }
            else if (zone == "restore")
            {
                Raise(SettingsRequested);
            }
            else if (zone == "exit")
            {
                Raise(ExitRequested);
            }
            else if (zone == "email")
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        "mailto:Mr.Dove@aliyun.com");
                }
                catch (Exception exception)
                {
                    Logger.Write("Open feedback email: " + exception);
                }
            }
            else if (zone == "github")
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        "https://github.com/Sendyun/SmartWindow");
                }
                catch (Exception exception)
                {
                    Logger.Write("Open GitHub repository: " + exception);
                }
            }
        }

        private void UpdateScrollFromPoint(int y)
        {
            int available = 572 - ScrollThumbHeight;
            if (available <= 0)
            {
                return;
            }
            double normalized = (y - 112.0) / available;
            normalized = Math.Max(0.0, Math.Min(1.0, normalized));
            double next = normalized * MaxContentScroll;
            if (Math.Abs(next - contentScroll) < 0.5)
            {
                return;
            }
            contentScroll = next;
            RequestRender();
        }

        private void UpdateSizeFromPoint(int x)
        {
            int trackLeft = 246 + 292;
            int trackWidth = 700 - 430;
            double normalized = Math.Max(
                0.0,
                Math.Min(1.0, (x - trackLeft) / (double)trackWidth));
            double nextScale = 0.80 + normalized * 0.40;
            if (Math.Abs(nextScale - sizeScale) < 0.002)
            {
                return;
            }
            sizeScale = nextScale;
            EventHandler<double> handler = SizeScaleChanged;
            if (handler != null)
            {
                handler(this, sizeScale);
            }
            RequestRender();
        }

        private void UpdateOpacityFromPoint(int x)
        {
            int trackLeft = 246 + 292;
            int trackWidth = 700 - 430;
            double normalized = Math.Max(
                0.0,
                Math.Min(1.0, (x - trackLeft) / (double)trackWidth));
            if (Math.Abs(normalized - consoleOpacity) < 0.002)
            {
                return;
            }
            consoleOpacity = normalized;
            RequestRender();
            EventHandler<double> handler = ConsoleOpacityChanged;
            if (handler != null)
            {
                handler(this, consoleOpacity);
            }
        }

        private void UpdateFrostedIntensityFromPoint(int x)
        {
            int trackLeft = 246 + 292;
            int trackWidth = 700 - 430;
            double normalized = Math.Max(
                0.0,
                Math.Min(1.0, (x - trackLeft) / (double)trackWidth));
            if (Math.Abs(normalized - frostedIntensity) < 0.002)
            {
                return;
            }
            frostedIntensity = normalized;
            RequestRender();
            EventHandler<double> handler = FrostedIntensityChanged;
            if (handler != null)
            {
                handler(this, frostedIntensity);
            }
        }

        private string HitZone(Point point)
        {
            if (languageMenuOpen)
            {
                for (int index = 0; index < Localization.Languages.Length; index++)
                {
                    Rectangle row = new Rectangle(
                        726,
                        72 + index * 31,
                        138,
                        28);
                    if (row.Contains(point))
                    {
                        return "language:" + index;
                    }
                }
            }
            if (new Rectangle(720, 28, 150, 30).Contains(point))
            {
                return "language";
            }
            if (new Rectangle(34, 27, 50, 50).Contains(point))
            {
                return "logo";
            }
            if (new Rectangle(928, 28, 34, 30).Contains(point))
            {
                return "close";
            }
            if (new Rectangle(886, 28, 34, 30).Contains(point))
            {
                return "minimize";
            }
            if (new Rectangle(30, 122, 184, 44).Contains(point))
            {
                return "navDashboard";
            }
            if (new Rectangle(30, 174, 184, 44).Contains(point))
            {
                return "navAuthor";
            }
            if (new Rectangle(30, 226, 184, 44).Contains(point))
            {
                return "navPerformance";
            }
            if (new Rectangle(30, 510, 184, 44).Contains(point))
            {
                return "navTimer";
            }
            if (new Rectangle(28, 568, 188, 52).Contains(point))
            {
                return "author";
            }
            if (new Rectangle(
                ScrollTrackBounds.Left - 4,
                ScrollTrackBounds.Top,
                ScrollTrackBounds.Width + 8,
                ScrollTrackBounds.Height).Contains(point))
            {
                return "scrollbar";
            }

            Point contentPoint = new Point(
                point.X,
                point.Y + (int)Math.Round(contentScroll));

            if (new Rectangle(28, 632, 188, 50).Contains(point))
            {
                return "installationInfo";
            }
            if (authorPage)
            {
                if (new Rectangle(274, 434, 360, 32).Contains(contentPoint))
                {
                    return "email";
                }
                if (new Rectangle(274, 458, 36, 36).Contains(contentPoint))
                {
                    return "github";
                }
                if (new Rectangle(710, 420, 236, 112).Contains(contentPoint))
                {
                    return "sponsor";
                }
                return "";
            }
            if (performancePage)
            {
                int[] rates = new int[] { 60, 90, 120, 165 };
                int startX = 660;
                int buttonWidth = 54;
                int gap = 5;
                for (int index = 0; index < rates.Length; index++)
                {
                    Rectangle button = new Rectangle(
                        startX + index * (buttonWidth + gap),
                        147,
                        buttonWidth,
                        30);
                    if (button.Contains(contentPoint))
                    {
                        return "fpsLimit" + rates[index];
                    }
                }
                return "";
            }
            if (timerPage)
            {
                if (new Rectangle(246, 184, 120, 36).Contains(contentPoint))
                {
                    return "timerModeCountdown";
                }
                if (new Rectangle(374, 184, 120, 36).Contains(contentPoint))
                {
                    return "timerModeStopwatch";
                }
                if (new Rectangle(756, 260, 166, 44).Contains(contentPoint) &&
                    (timerSnapshot == null || !timerSnapshot.IsActive) &&
                    timerSetupMode == SmartTimerMode.Stopwatch)
                {
                    return "timerStartStopwatch";
                }
                if (TimerModeActive(timerSetupMode))
                {
                    int count = timerSetupMode == SmartTimerMode.Stopwatch
                        ? 3
                        : 2;
                    int gap = 8;
                    int buttonWidth = timerSetupMode == SmartTimerMode.Stopwatch
                        ? 92
                        : 112;
                    int startX = 946 -
                        24 -
                        buttonWidth * count -
                        gap * (count - 1);
                    for (int index = 0; index < count; index++)
                    {
                        Rectangle button = new Rectangle(
                            startX + index * (buttonWidth + gap),
                            259,
                            buttonWidth,
                            46);
                        if (button.Contains(contentPoint))
                        {
                            return index == 0
                                ? "timerPause"
                                : (timerSetupMode == SmartTimerMode.Stopwatch &&
                                   index == 1
                                    ? "timerLap"
                                    : "timerCancel");
                        }
                    }
                }
                if (TimerPickerVisible &&
                    new Rectangle(776, 301, 138, 58).Contains(contentPoint))
                {
                    return "timerStartCountdown";
                }
                if (timerSetupMode == SmartTimerMode.Countdown)
                {
                    int maximum = Math.Min(6, timerPresets.Length);
                    for (int index = 0; index < maximum; index++)
                    {
                        int column = index % 2;
                        int row = index / 2;
                        Rectangle bounds = new Rectangle(
                            246 + column * 352,
                            TimerPresetTop + row * 59,
                            340,
                            52);
                        if (new Rectangle(
                            bounds.Right - 34,
                            bounds.Top + 7,
                            28,
                            34).Contains(contentPoint))
                        {
                            return "timerPresetDelete" + index;
                        }
                        if (bounds.Contains(contentPoint))
                        {
                            return "timerPreset" + index;
                        }
                    }
                    if (TimerAddPresetBounds.Contains(contentPoint))
                    {
                        return "timerAddPreset";
                    }
                }
                if (TimerColorWheelBounds.Contains(contentPoint))
                {
                    return "timerWheel";
                }
                if (new Rectangle(
                    TimerBrightnessBounds.Left - 8,
                    TimerBrightnessBounds.Top,
                    TimerBrightnessBounds.Width + 16,
                    TimerBrightnessBounds.Height).Contains(contentPoint))
                {
                    return "timerBrightness";
                }
                Rectangle colorCard = TimerColorCardBounds;
                if (new Rectangle(
                    colorCard.Left + 240,
                    colorCard.Top + 54,
                    202,
                    42).Contains(contentPoint))
                {
                    return "timerRgb";
                }
                Rectangle soundCard = TimerSoundCardBounds;
                if (new Rectangle(
                    soundCard.Left + 286,
                    soundCard.Top + 28,
                    118,
                    42).Contains(contentPoint))
                {
                    return "timerSoundBrowse";
                }
                if (new Rectangle(
                    soundCard.Left + 416,
                    soundCard.Top + 28,
                    108,
                    42).Contains(contentPoint))
                {
                    return "timerSoundPreview";
                }
                if (new Rectangle(
                    soundCard.Left + 536,
                    soundCard.Top + 28,
                    138,
                    42).Contains(contentPoint))
                {
                    return "timerSoundReset";
                }
                return "";
            }

            if (new Rectangle(246, 195, 700, 92).Contains(contentPoint))
            {
                return "notification";
            }
            if (new Rectangle(246, 298, 700, 92).Contains(contentPoint))
            {
                return "music";
            }
            if (new Rectangle(246, 401, 700, 92).Contains(contentPoint))
            {
                return "persistent";
            }
            if (new Rectangle(246, 504, 700, 92).Contains(contentPoint))
            {
                return "transparent";
            }
            if (new Rectangle(246, 607, 700, 92).Contains(contentPoint))
            {
                return "quality";
            }
            int frostedY = 710;
            int frostedHeight = FrostedCardHeight;
            if (new Rectangle(
                246,
                frostedY,
                700,
                frostedHeight).Contains(contentPoint))
            {
                if (transparentWindow &&
                    frostedGlassEnabled &&
                    contentPoint.Y >= frostedY + 92)
                {
                    return "frostedIntensity";
                }
                return "frosted";
            }
            int opacityY = frostedY + frostedHeight + 11;
            if (new Rectangle(246, opacityY, 700, 92).Contains(contentPoint))
            {
                return "consoleOpacity";
            }
            int sizeY = opacityY + 103;
            if (new Rectangle(246, sizeY, 700, 92).Contains(contentPoint))
            {
                return "size";
            }
            int autoStartY = sizeY + 103;
            if (new Rectangle(246, autoStartY, 700, 92).Contains(contentPoint))
            {
                return "autoStart";
            }
            int movementY = autoStartY + 103;
            if (new Rectangle(246, movementY, 700, 92).Contains(contentPoint))
            {
                return "movement";
            }
            int clickThroughY = movementY + 103;
            if (new Rectangle(246, clickThroughY, 700, 92).Contains(contentPoint))
            {
                return "clickThrough";
            }
            int actionY = clickThroughY + 107;
            if (new Rectangle(246, actionY, 202, 48).Contains(contentPoint))
            {
                return "test";
            }
            if (new Rectangle(464, actionY, 202, 48).Contains(contentPoint))
            {
                return "history";
            }
            if (new Rectangle(682, actionY, 202, 48).Contains(contentPoint))
            {
                return "filters";
            }
            int utilityY = actionY + 60;
            if (new Rectangle(246, utilityY, 166, 46).Contains(contentPoint))
            {
                return "export";
            }
            if (new Rectangle(424, utilityY, 166, 46).Contains(contentPoint))
            {
                return "import";
            }
            if (new Rectangle(602, utilityY, 166, 46).Contains(contentPoint))
            {
                return "reset";
            }
            if (new Rectangle(780, utilityY, 166, 46).Contains(contentPoint))
            {
                return "restore";
            }
            int exitY = actionY + 118;
            if (new Rectangle(246, exitY, 202, 46).Contains(contentPoint))
            {
                return "exit";
            }
            return "";
        }

        private Point ToLogical(Point point)
        {
            return new Point(
                (int)Math.Round(
                    point.X *
                    LogicalWidth /
                    (double)Math.Max(1, ClientSize.Width)),
                (int)Math.Round(
                    point.Y *
                    LogicalHeight /
                    (double)Math.Max(1, ClientSize.Height)));
        }

        private static void Raise(EventHandler handler)
        {
            if (handler != null)
            {
                handler(null, EventArgs.Empty);
            }
        }

        private Font CreateFont(float pointSize, FontStyle style)
        {
            float pixels = pointSize * 96f / 72f;
            return RuntimeAssets.CreateUiFont(pixels, style);
        }

        private void CreateFonts()
        {
            if (titleFont != null)
            {
                titleFont.Dispose();
                sectionFont.Dispose();
                bodyFont.Dispose();
                smallFont.Dispose();
                microFont.Dispose();
                performanceFont.Dispose();
            }
            titleFont = CreateFont(20f, FontStyle.Bold);
            sectionFont = CreateFont(11f, FontStyle.Bold);
            bodyFont = CreateFont(9.5f, FontStyle.Regular);
            smallFont = CreateFont(8.2f, FontStyle.Regular);
            microFont = CreateFont(7.3f, FontStyle.Regular);
            performanceFont = CreateFont(29f, FontStyle.Bold);
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

        private void ApplyBlur()
        {
            NativeRect region = new NativeRect();
            region.Left = 14;
            region.Top = 14;
            region.Right = Width - 14;
            region.Bottom = Height - 14;
            BlurBehind blur = new BlurBehind();
            blur.Enabled = true;
            blur.Region = CreateRectRgn(
                region.Left,
                region.Top,
                region.Right,
                region.Bottom);
            DwmEnableBlurBehindWindow(Handle, ref blur);
            if (blur.Region != IntPtr.Zero)
            {
                DeleteObject(blur.Region);
            }
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
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BlurBehind
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool Enabled;
            public IntPtr Region;
            [MarshalAs(UnmanagedType.Bool)]
            public bool TransitionOnMaximized;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(
            IntPtr window,
            int message,
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

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("dwmapi.dll")]
        private static extern int DwmEnableBlurBehindWindow(
            IntPtr window,
            ref BlurBehind blur);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                renderTimer.Stop();
                renderTimer.Dispose();
                hoverRenderTimer.Stop();
                hoverRenderTimer.Dispose();
                logoTimer.Stop();
                logoTimer.Dispose();
                logoTransitionTimer.Stop();
                logoTransitionTimer.Dispose();
                installationMarqueeTimer.Stop();
                installationMarqueeTimer.Dispose();
                if (surfaceGraphics != null)
                {
                    surfaceGraphics.Dispose();
                    surfaceGraphics = null;
                }
                if (surface != null)
                {
                    surface.Dispose();
                    surface = null;
                }
                if (logoLight != null)
                {
                    logoLight.Dispose();
                }
                if (logoDark != null)
                {
                    logoDark.Dispose();
                }
                if (githubLogo != null)
                {
                    githubLogo.Dispose();
                }
                titleFont.Dispose();
                sectionFont.Dispose();
                bodyFont.Dispose();
                smallFont.Dispose();
                microFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
