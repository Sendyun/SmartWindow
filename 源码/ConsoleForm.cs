using System;
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
        private const string DisplayVersion = "Ver 1.1.0";

        private readonly Bitmap logo;
        private readonly Bitmap githubLogo;
        private readonly Timer renderTimer;
        private readonly Timer hoverRenderTimer;
        private Font titleFont;
        private Font sectionFont;
        private Font bodyFont;
        private Font smallFont;
        private Font microFont;
        private float dpiScale;
        private Bitmap surface;
        private Graphics surfaceGraphics;
        private bool authorPage;
        private bool languageMenuOpen;
        private bool draggingSizeSlider;
        private bool draggingOpacitySlider;
        private bool draggingScrollbar;
        private string hoverZone = "";
        private double contentScroll;
        private bool paused;
        private bool musicEnabled;
        private bool musicPersistent;
        private bool transparentWindow;
        private bool qualityEnhancement;
        private bool autoStart;
        private bool movementEnabled;
        private bool clickThroughEnabled;
        private double sizeScale = 1.0;
        private double consoleOpacity = 0.5;
        private string runtimeStatus = Localization.Get("statusRunning");
        private bool allowClose;
        private bool rendering;
        private bool renderPending;

        public event EventHandler PauseToggleRequested;
        public event EventHandler MusicToggleRequested;
        public event EventHandler PersistentToggleRequested;
        public event EventHandler GlassModeToggleRequested;
        public event EventHandler QualityEnhanceToggleRequested;
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

            logo = RuntimeAssets.LoadLogo();
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

            MouseMove += OnConsoleMouseMove;
            MouseLeave += OnConsoleMouseLeave;
            MouseDown += OnConsoleMouseDown;
            MouseUp += OnConsoleMouseUp;
            MouseClick += OnConsoleMouseClick;
            MouseWheel += OnConsoleMouseWheel;
        }

        public void SetState(
            bool isPaused,
            bool isMusicEnabled,
            bool isMusicPersistent,
            bool isTransparent,
            bool isQualityEnhanced,
            bool isAutoStart,
            bool isMovementEnabled,
            bool isClickThrough,
            string status,
            double scale,
            double opacity)
        {
            paused = isPaused;
            musicEnabled = isMusicEnabled;
            musicPersistent = isMusicPersistent;
            transparentWindow = isTransparent;
            qualityEnhancement = isQualityEnhanced;
            autoStart = isAutoStart;
            movementEnabled = isMovementEnabled;
            clickThroughEnabled = isClickThrough;
            sizeScale = Math.Max(0.80, Math.Min(1.20, scale));
            consoleOpacity = Math.Max(0.0, Math.Min(1.0, opacity));
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
            RequestRender();
        }

        public void RefreshLocalization()
        {
            CreateFonts();
            Text = Localization.Get("consoleTitle");
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
            else
            {
                DrawDashboard(graphics);
            }
            graphics.Restore(contentState);
            DrawScrollbar(graphics);
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
            if (logo != null)
            {
                DrawLogo(graphics, new Rectangle(34, 27, 50, 50));
            }

            DrawText(
                graphics,
                Localization.Get("appName"),
                titleFont,
                new Rectangle(98, 26, 230, 30),
                Color.FromArgb(247, 246, 242));
            DrawText(
                graphics,
                "Sendyun Technology Unit",
                smallFont,
                new Rectangle(99, 57, 270, 20),
                Color.FromArgb(155, 214, 218, 225));

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
            DrawNavButton(
                graphics,
                overview,
                Localization.Get("controlCenter"),
                "navDashboard",
                !authorPage);
            DrawNavButton(
                graphics,
                about,
                Localization.Get("aboutAuthor"),
                "navAuthor",
                authorPage);
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

            DrawOpacityCard(
                graphics,
                new Rectangle(left, 710, width, 92));

            DrawSizeCard(
                graphics,
                new Rectangle(left, 813, width, 92));

            DrawSettingCard(
                graphics,
                "autoStart",
                new Rectangle(left, 916, width, 92),
                Localization.Get("autoStart"),
                autoStart
                    ? Localization.Get("autoStartOn")
                    : Localization.Get("autoStartOff"),
                autoStart);

            DrawSettingCard(
                graphics,
                "movement",
                new Rectangle(left, 1019, width, 92),
                Localization.Get("windowMovement"),
                movementEnabled
                    ? Localization.Get("windowMovementOn")
                    : Localization.Get("windowMovementOff"),
                movementEnabled);

            DrawSettingCard(
                graphics,
                "clickThrough",
                new Rectangle(left, 1122, width, 92),
                Localization.Get("antiOcclusion"),
                clickThroughEnabled
                    ? Localization.Get("antiOcclusionOn")
                    : Localization.Get("antiOcclusionOff"),
                clickThroughEnabled);

            DrawActionButton(
                graphics,
                "test",
                new Rectangle(left, 1229, 202, 48),
                Localization.Get("testNotification"),
                true);
            DrawActionButton(
                graphics,
                "history",
                new Rectangle(left + 218, 1229, 202, 48),
                Localization.Get("notificationHistory"),
                false);
            DrawActionButton(
                graphics,
                "filters",
                new Rectangle(left + 436, 1229, 202, 48),
                Localization.Get("appFilters"),
                false);

            int utilityWidth = 166;
            DrawActionButton(
                graphics,
                "export",
                new Rectangle(left, 1289, utilityWidth, 46),
                Localization.Get("exportSettings"),
                false);
            DrawActionButton(
                graphics,
                "import",
                new Rectangle(left + 178, 1289, utilityWidth, 46),
                Localization.Get("importSettings"),
                false);
            DrawActionButton(
                graphics,
                "reset",
                new Rectangle(left + 356, 1289, utilityWidth, 46),
                Localization.Get("resetSettings"),
                false);
            DrawActionButton(
                graphics,
                "restore",
                new Rectangle(left + 534, 1289, utilityWidth, 46),
                Localization.Get("notificationSettings"),
                false);

            DrawActionButton(
                graphics,
                "exit",
                new Rectangle(left, 1347, 202, 46),
                Localization.Get("exitApp"),
                false);

            DrawText(
                graphics,
                Localization.Get("footerHint"),
                smallFont,
                new Rectangle(left, 1412, width, 24),
                Color.FromArgb(122, 211, 211, 206));
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
            if (logo != null)
            {
                DrawLogo(
                    graphics,
                    new Rectangle(left + 28, card.Top + 28, 82, 82));
            }
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

        private const double MaxContentScroll = 760.0;

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
            if (logo == null)
            {
                return;
            }
            graphics.DrawImage(logo, bounds);
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
            if (draggingSizeSlider)
            {
                UpdateSizeFromPoint(logical.X);
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
                zone == "navDashboard" ||
                zone == "navAuthor" ||
                zone == "author" ||
                zone == "sponsor" ||
                zone == "scrollbar" ||
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
                !authorPage &&
                new Rectangle(246, 710, 700, 92).Contains(contentPoint))
            {
                draggingOpacitySlider = true;
                UpdateOpacityFromPoint(logical.X);
                return;
            }
            if (e.Button == MouseButtons.Left &&
                !authorPage &&
                new Rectangle(246, 813, 700, 92).Contains(contentPoint))
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
                draggingScrollbar = false;
                FlushRender();
            }
        }

        private void OnConsoleMouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta == 0 || authorPage)
            {
                return;
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
            if (zone == "size" || zone == "consoleOpacity")
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
            else if (zone == "minimize")
            {
                WindowState = FormWindowState.Minimized;
            }
            else if (zone == "navDashboard")
            {
                authorPage = false;
                contentScroll = 0.0;
                RequestRender();
            }
            else if (zone == "navAuthor" || zone == "author")
            {
                authorPage = true;
                contentScroll = 0.0;
                RequestRender();
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
            if (new Rectangle(246, 710, 700, 92).Contains(contentPoint))
            {
                return "consoleOpacity";
            }
            if (new Rectangle(246, 813, 700, 92).Contains(contentPoint))
            {
                return "size";
            }
            if (new Rectangle(246, 916, 700, 92).Contains(contentPoint))
            {
                return "autoStart";
            }
            if (new Rectangle(246, 1019, 700, 92).Contains(contentPoint))
            {
                return "movement";
            }
            if (new Rectangle(246, 1122, 700, 92).Contains(contentPoint))
            {
                return "clickThrough";
            }
            if (new Rectangle(246, 1229, 202, 48).Contains(contentPoint))
            {
                return "test";
            }
            if (new Rectangle(464, 1229, 202, 48).Contains(contentPoint))
            {
                return "history";
            }
            if (new Rectangle(682, 1229, 202, 48).Contains(contentPoint))
            {
                return "filters";
            }
            if (new Rectangle(246, 1289, 166, 46).Contains(contentPoint))
            {
                return "export";
            }
            if (new Rectangle(424, 1289, 166, 46).Contains(contentPoint))
            {
                return "import";
            }
            if (new Rectangle(602, 1289, 166, 46).Contains(contentPoint))
            {
                return "reset";
            }
            if (new Rectangle(780, 1289, 166, 46).Contains(contentPoint))
            {
                return "restore";
            }
            if (new Rectangle(246, 1347, 202, 46).Contains(contentPoint))
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
            }
            titleFont = CreateFont(20f, FontStyle.Bold);
            sectionFont = CreateFont(11f, FontStyle.Bold);
            bodyFont = CreateFont(9.5f, FontStyle.Regular);
            smallFont = CreateFont(8.2f, FontStyle.Regular);
            microFont = CreateFont(7.3f, FontStyle.Regular);
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
                if (logo != null)
                {
                    logo.Dispose();
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
