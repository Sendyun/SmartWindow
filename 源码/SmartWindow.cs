using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;

namespace SmartWindow
{
    internal sealed class NotificationItem
    {
        public string AppName;
        public string Title;
        public string Body;
        public DateTime ArrivedAt;
        public long CursorOrder;
        public bool IsMedia;
        public bool IsSizePreview;
        public string ArtworkPath;
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool restoreOnly = HasArgument(args, "--restore");
            bool muteBanners = !HasArgument(args, "--no-mute");
            bool showConsole = !HasArgument(args, "--background");

            bool firstInstance;
            using (Mutex instanceMutex = new Mutex(
                true,
                "Local\\SmartWindow.SingleInstance",
                out firstInstance))
            {
                if (!firstInstance)
                {
                    try
                    {
                        using (EventWaitHandle showEvent =
                            EventWaitHandle.OpenExisting(
                                "Local\\SmartWindow.ShowConsole"))
                        {
                            showEvent.Set();
                        }
                    }
                    catch
                    {
                    }
                    return;
                }

                BannerMuter muter = new BannerMuter();
                try
                {
                    RuntimeAssets.Initialize();
                    // A previous forced shutdown can leave banner settings muted.
                    muter.RestoreBackup();
                    if (restoreOnly)
                    {
                        return;
                    }

                    if (muteBanners)
                    {
                        muter.MuteCurrentApps();
                    }

                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(
                        UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate(
                        object sender,
                        System.Threading.ThreadExceptionEventArgs eventArgs)
                    {
                        Logger.Write(
                            "Unhandled UI exception: " +
                            eventArgs.Exception);
                    };
                    AppDomain.CurrentDomain.UnhandledException += delegate(
                        object sender,
                        UnhandledExceptionEventArgs eventArgs)
                    {
                        Logger.Write(
                            "Unhandled process exception: " +
                            eventArgs.ExceptionObject);
                    };
                    using (IslandApplication application =
                        new IslandApplication(
                            muter,
                            muteBanners,
                            showConsole))
                    {
                        Application.Run(application);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Write(exception.ToString());
                    muter.RestoreBackup();
                    if (!restoreOnly)
                    {
                        MessageBox.Show(
                            "智慧窗启动失败。\r\n\r\n" + exception.Message +
                            "\r\n\r\n日志：" + Logger.LogPath,
                            "智慧窗",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                finally
                {
                    muter.Dispose();
                    instanceMutex.ReleaseMutex();
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(
                    args[index],
                    expected,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }

    internal static class Logger
    {
        private static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartWindow");
        private static readonly object Sync = new object();

        public static readonly string LogPath = Path.Combine(
            DataDirectory,
            "SmartWindow.log");

        public static void Write(string message)
        {
            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(DataDirectory);
                    File.AppendAllText(
                        LogPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                        " " +
                        message +
                        Environment.NewLine,
                        Encoding.UTF8);
                }
                catch
                {
                    // Logging must never prevent the overlay from shutting down.
                }
            }
        }
    }

    internal static class AutoStartManager
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SmartWindow";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    RunKeyPath,
                    false))
                {
                    return key != null &&
                        key.GetValue(ValueName) != null;
                }
            }
            catch (Exception exception)
            {
                Logger.Write("AutoStartManager.IsEnabled: " + exception);
                return false;
            }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    RunKeyPath))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    if (enabled)
                    {
                        key.SetValue(
                            ValueName,
                            "\"" + Application.ExecutablePath +
                                "\" --background",
                            RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch (Exception exception)
            {
                Logger.Write("AutoStartManager.SetEnabled: " + exception);
                return false;
            }
        }

        public static void RefreshCommandIfEnabled()
        {
            if (IsEnabled())
            {
                SetEnabled(true);
            }
        }
    }

    internal sealed class BannerState
    {
        public bool Exists;
        public int Value;
    }

    internal sealed class BannerMuter : IDisposable
    {
        private const string SettingsPath =
            @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

        private const string ValueName = "ShowBanner";

        private readonly object sync = new object();
        private readonly Dictionary<string, BannerState> states =
            new Dictionary<string, BannerState>(StringComparer.OrdinalIgnoreCase);

        private readonly string dataDirectory;
        private readonly string backupPath;
        private bool stopping;
        private bool disposed;

        public BannerMuter()
        {
            dataDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "SmartWindow");
            backupPath = Path.Combine(dataDirectory, "banner-backup.dat");
        }

        public void MuteCurrentApps()
        {
            lock (sync)
            {
                if (disposed || stopping)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(dataDirectory);
                    using (RegistryKey settings =
                        Registry.CurrentUser.OpenSubKey(SettingsPath, true))
                    {
                        if (settings == null)
                        {
                            return;
                        }

                        bool changed = false;
                        string[] appNames = settings.GetSubKeyNames();
                        for (int index = 0; index < appNames.Length; index++)
                        {
                            string appName = appNames[index];
                            if (states.ContainsKey(appName))
                            {
                                continue;
                            }

                            using (RegistryKey appKey =
                                settings.OpenSubKey(appName, true))
                            {
                                if (appKey == null)
                                {
                                    continue;
                                }

                                BannerState state = new BannerState();
                                object current = appKey.GetValue(
                                    ValueName,
                                    null,
                                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                                state.Exists = current != null;
                                if (current != null)
                                {
                                    if (!(current is int))
                                    {
                                        continue;
                                    }
                                    state.Value = (int)current;
                                }

                                states[appName] = state;
                                appKey.SetValue(
                                    ValueName,
                                    0,
                                    RegistryValueKind.DWord);
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            SaveBackup();
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logger.Write("MuteCurrentApps: " + exception);
                }
            }
        }

        public void RestoreBackup()
        {
            lock (sync)
            {
                try
                {
                    if (!File.Exists(backupPath))
                    {
                        return;
                    }

                    states.Clear();
                    LoadBackup();
                    ApplyRestore();
                }
                catch (Exception exception)
                {
                    Logger.Write("RestoreBackup: " + exception);
                }
            }
        }

        public void StopAndRestore()
        {
            lock (sync)
            {
                stopping = true;
                RestoreBackup();
            }
        }

        private void ApplyRestore()
        {
            using (RegistryKey settings =
                Registry.CurrentUser.OpenSubKey(SettingsPath, true))
            {
                if (settings != null)
                {
                    foreach (KeyValuePair<string, BannerState> pair in states)
                    {
                        try
                        {
                            using (RegistryKey appKey =
                                settings.OpenSubKey(pair.Key, true))
                            {
                                if (appKey == null)
                                {
                                    continue;
                                }

                                if (pair.Value.Exists)
                                {
                                    appKey.SetValue(
                                        ValueName,
                                        pair.Value.Value,
                                        RegistryValueKind.DWord);
                                }
                                else
                                {
                                    appKey.DeleteValue(
                                        ValueName,
                                        false);
                                }
                            }
                        }
                        catch (Exception exception)
                        {
                            Logger.Write(
                                "Restore app " + pair.Key + ": " + exception);
                        }
                    }
                }
            }

            states.Clear();
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }

        private void SaveBackup()
        {
            Directory.CreateDirectory(dataDirectory);
            List<string> lines = new List<string>();
            lines.Add("SMARTWINDOW_BANNER_BACKUP_V1");
            foreach (KeyValuePair<string, BannerState> pair in states)
            {
                string key = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(pair.Key));
                string value = pair.Value.Exists
                    ? "1:" + pair.Value.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                    : "0";
                lines.Add(key + "\t" + value);
            }

            string temporaryPath = backupPath + ".tmp";
            File.WriteAllLines(temporaryPath, lines.ToArray(), Encoding.UTF8);
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            File.Move(temporaryPath, backupPath);
        }

        private void LoadBackup()
        {
            string[] lines = File.ReadAllLines(backupPath, Encoding.UTF8);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("SMARTWINDOW_", StringComparison.Ordinal))
                {
                    continue;
                }

                string[] parts = line.Split('\t');
                if (parts.Length != 2)
                {
                    continue;
                }

                string appName = Encoding.UTF8.GetString(
                    Convert.FromBase64String(parts[0]));
                BannerState state = new BannerState();
                if (parts[1].StartsWith("1:", StringComparison.Ordinal))
                {
                    state.Exists = true;
                    int value;
                    if (int.TryParse(
                        parts[1].Substring(2),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value))
                    {
                        state.Value = value;
                    }
                }
                states[appName] = state;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                stopping = true;
                try
                {
                    if (states.Count > 0)
                    {
                        ApplyRestore();
                    }
                }
                catch (Exception exception)
                {
                    Logger.Write("Dispose: " + exception);
                }
                disposed = true;
            }
        }
    }

    internal sealed class NotificationDatabase : IDisposable
    {
        private const int SqliteOk = 0;
        private const int SqliteRow = 100;
        private const int SqliteDone = 101;
        private const int OpenReadOnly = 0x00000001;
        private const int OpenUri = 0x00000040;
        private const int BusyTimeoutMilliseconds = 1800;

        private readonly string databasePath;
        private IntPtr database;
        private bool disposed;

        public NotificationDatabase()
        {
            databasePath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Notifications",
                "wpndatabase.db");
        }

        public long GetLatestOrder()
        {
            EnsureOpen();
            using (Statement statement = Prepare(
                "select coalesce(max([Order]), 0) from Notification " +
                "where lower([Type]) = 'toast';"))
            {
                if (statement.Step() != SqliteRow)
                {
                    return 0;
                }
                return statement.ColumnInt64(0);
            }
        }

        public List<NotificationItem> GetNewNotifications(long afterOrder)
        {
            EnsureOpen();
            List<NotificationItem> result = new List<NotificationItem>();
            const string sql =
                "select n.[Order], n.Payload, h.PrimaryId " +
                "from Notification n " +
                "left join NotificationHandler h " +
                "on h.RecordId = n.HandlerId " +
                "where lower(n.[Type]) = 'toast' and n.[Order] > ?1 " +
                "order by n.[Order] asc limit 20;";

            using (Statement statement = Prepare(sql))
            {
                statement.BindInt64(1, afterOrder);
                while (true)
                {
                    int step = statement.Step();
                    if (step == SqliteDone)
                    {
                        break;
                    }
                    if (step != SqliteRow)
                    {
                        throw CreateError("读取新通知失败");
                    }

                    byte[] payload = statement.ColumnBytes(1);
                    string handler = Clean(statement.ColumnText(2));
                    NotificationItem item = ToastParser.Parse(payload, handler);
                    if (item != null)
                    {
                        item.CursorOrder = statement.ColumnInt64(0);
                        result.Add(item);
                    }
                }
            }

            return result;
        }

        private void EnsureOpen()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("NotificationDatabase");
            }
            if (database != IntPtr.Zero)
            {
                return;
            }
            if (!File.Exists(databasePath))
            {
                throw new FileNotFoundException(
                    "找不到 Windows 通知数据库",
                    databasePath);
            }

            byte[] utf8Path = Encoding.UTF8.GetBytes(databasePath);
            int result = sqlite3_open_v2(
                utf8Path,
                out database,
                OpenReadOnly | OpenUri,
                IntPtr.Zero);
            if (result != SqliteOk)
            {
                if (database != IntPtr.Zero)
                {
                    sqlite3_close_v2(database);
                    database = IntPtr.Zero;
                }
                throw new InvalidOperationException(
                    "无法打开 Windows 通知数据库，错误码 " + result);
            }
            sqlite3_busy_timeout(database, BusyTimeoutMilliseconds);
        }

        private Statement Prepare(string sql)
        {
            EnsureOpen();
            IntPtr statement;
            IntPtr tail;
            byte[] utf8Sql = Encoding.UTF8.GetBytes(sql);
            int result = sqlite3_prepare_v2(
                database,
                utf8Sql,
                utf8Sql.Length,
                out statement,
                out tail);
            if (result != SqliteOk)
            {
                throw CreateError("准备通知查询失败");
            }
            return new Statement(this, statement);
        }

        private Exception CreateError(string prefix)
        {
            string detail = "";
            if (database != IntPtr.Zero)
            {
                IntPtr pointer = sqlite3_errmsg(database);
                if (pointer != IntPtr.Zero)
                {
                    detail = Marshal.PtrToStringAnsi(pointer);
                }
            }
            return new InvalidOperationException(
                prefix + (string.IsNullOrEmpty(detail) ? "" : "：" + detail));
        }

        private static string Clean(string value)
        {
            return value == null ? "" : value.Trim();
        }

        private void Close()
        {
            if (database != IntPtr.Zero)
            {
                sqlite3_close_v2(database);
                database = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Close();
        }

        private sealed class Statement : IDisposable
        {
            private readonly NotificationDatabase owner;
            private IntPtr handle;

            public Statement(NotificationDatabase owner, IntPtr handle)
            {
                this.owner = owner;
                this.handle = handle;
            }

            public int Step()
            {
                return sqlite3_step(handle);
            }

            public void BindInt64(int index, long value)
            {
                int result = sqlite3_bind_int64(handle, index, value);
                if (result != SqliteOk)
                {
                    throw owner.CreateError("绑定通知游标失败");
                }
            }

            public long ColumnInt64(int index)
            {
                return sqlite3_column_int64(handle, index);
            }

            public string ColumnText(int index)
            {
                IntPtr pointer = sqlite3_column_text(handle, index);
                int length = sqlite3_column_bytes(handle, index);
                if (pointer == IntPtr.Zero || length <= 0)
                {
                    return "";
                }
                byte[] bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, length);
                return Encoding.UTF8.GetString(bytes);
            }

            public byte[] ColumnBytes(int index)
            {
                IntPtr pointer = sqlite3_column_blob(handle, index);
                int length = sqlite3_column_bytes(handle, index);
                if (pointer == IntPtr.Zero || length <= 0)
                {
                    return new byte[0];
                }
                byte[] bytes = new byte[length];
                Marshal.Copy(pointer, bytes, 0, length);
                return bytes;
            }

            public void Dispose()
            {
                if (handle != IntPtr.Zero)
                {
                    sqlite3_finalize(handle);
                    handle = IntPtr.Zero;
                }
            }
        }

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(
            byte[] filename,
            out IntPtr database,
            int flags,
            IntPtr virtualFileSystem);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close_v2(IntPtr database);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_busy_timeout(
            IntPtr database,
            int milliseconds);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(
            IntPtr database,
            byte[] sql,
            int byteCount,
            out IntPtr statement,
            out IntPtr tail);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_int64(
            IntPtr statement,
            int index,
            long value);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(
            IntPtr statement,
            int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(
            IntPtr statement,
            int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(
            IntPtr statement,
            int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(
            IntPtr statement,
            int index);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr statement);

        [DllImport("winsqlite3.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr database);
    }

    internal static class ToastParser
    {
        public static NotificationItem Parse(byte[] payload, string handler)
        {
            if (payload == null || payload.Length == 0)
            {
                return null;
            }

            try
            {
                string xml = Decode(payload);
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                document.LoadXml(xml);

                List<string> texts = new List<string>();
                XmlNodeList nodes = document.SelectNodes(
                    "//*[local-name()='text']");
                if (nodes != null)
                {
                    for (int index = 0; index < nodes.Count; index++)
                    {
                        string text = Normalize(nodes[index].InnerText);
                        if (!string.IsNullOrEmpty(text))
                        {
                            texts.Add(text);
                        }
                    }
                }

                string title = texts.Count > 0
                    ? texts[0]
                    : Localization.Get("newNotification");
                string body = texts.Count > 1
                    ? texts[1]
                    : (texts.Count == 1
                        ? ""
                        : Localization.Get("newNotificationBody"));

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Localization.Get("newNotification");
                }

                NotificationItem item = new NotificationItem();
                item.AppName = FriendlyAppName(handler);
                item.Title = title.Trim();
                item.Body = body.Trim();
                item.ArrivedAt = DateTime.Now;
                return item;
            }
            catch (Exception exception)
            {
                Logger.Write("ToastParser: " + exception.Message);
                return null;
            }
        }

        private static string Decode(byte[] payload)
        {
            try
            {
                return new UTF8Encoding(false, true).GetString(payload);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Unicode.GetString(payload);
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            string cleaned = value.Replace("\r", " ").Replace("\n", " ");
            while (cleaned.Contains("  "))
            {
                cleaned = cleaned.Replace("  ", " ");
            }
            return cleaned.Trim();
        }

        private static string FriendlyAppName(string handler)
        {
            if (string.IsNullOrWhiteSpace(handler))
            {
                return Localization.Get("windows");
            }

            if (handler.IndexOf("OpenAI.Codex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Codex";
            }
            if (handler.Equals("QQ", StringComparison.OrdinalIgnoreCase))
            {
                return "QQ";
            }
            if (handler.IndexOf("YourPhone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "手机连接";
            }
            if (handler.IndexOf("DeepSeek", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "DeepSeek";
            }
            if (handler.IndexOf("Alarm", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "时钟";
            }
            if (handler.IndexOf("ScreenSketch", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "截图工具";
            }
            if (handler.IndexOf("WindowsStore", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Microsoft Store";
            }
            if (handler.IndexOf("alimail", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "阿里邮箱";
            }
            if (handler.IndexOf("WXWork", StringComparison.OrdinalIgnoreCase) >= 0 ||
                handler.IndexOf("WeChat", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "微信";
            }
            if (handler.IndexOf("SystemToast", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Localization.Get("windows");
            }

            string name = handler;
            int bang = name.IndexOf('!');
            if (bang > 0)
            {
                name = name.Substring(0, bang);
            }
            int publisher = name.IndexOf("_8weky", StringComparison.OrdinalIgnoreCase);
            if (publisher > 0)
            {
                name = name.Substring(0, publisher);
            }
            int slash = name.LastIndexOfAny(new char[] { '\\', '/' });
            if (slash >= 0 && slash + 1 < name.Length)
            {
                name = name.Substring(slash + 1);
            }
            return string.IsNullOrWhiteSpace(name)
                ? Localization.Get("appNotification")
                : name;
        }
    }

    internal sealed class IslandApplication : ApplicationContext
    {
        private readonly BannerMuter bannerMuter;
        private readonly bool muteBanners;
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip menu;
        private readonly IslandForm island;
        private readonly NotificationMonitor monitor;
        private readonly MediaMonitor mediaMonitor;
        private readonly GlobalHotkeys hotkeys;
        private readonly ConsoleForm console;
        private readonly EventWaitHandle showConsoleEvent;
        private readonly RegisteredWaitHandle showConsoleWait;
        private readonly ToolStripMenuItem consoleItem;
        private readonly ToolStripMenuItem showItem;
        private readonly ToolStripMenuItem demoItem;
        private readonly ToolStripMenuItem pauseItem;
        private readonly ToolStripMenuItem musicModeItem;
        private readonly ToolStripMenuItem musicPersistentItem;
        private readonly ToolStripMenuItem exitItem;
        private readonly Icon generatedIcon;
        private readonly System.Windows.Forms.Timer bannerRefreshTimer;
        private readonly System.Windows.Forms.Timer settingsSaveTimer;
        private readonly string settingsPath;
        private readonly bool showConsole;
        private bool paused;
        private bool musicMode;
        private bool musicPersistent;
        private bool transparentWindow;
        private bool qualityEnhancement;
        private double uiScale = 1.0;
        private double consoleOpacity = 0.5;
        private bool autoStart;
        private bool cleanedUp;

        public IslandApplication(
            BannerMuter bannerMuter,
            bool muteBanners,
            bool showConsole)
        {
            this.bannerMuter = bannerMuter;
            this.muteBanners = muteBanners;
            this.showConsole = showConsole;
            showConsoleEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                "Local\\SmartWindow.ShowConsole");
            settingsPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "SmartWindow",
                "settings.ini");
            Localization.SetLanguage(ReadLanguage());
            transparentWindow = ReadTransparentWindow();
            qualityEnhancement =
                transparentWindow && ReadBooleanSetting("qualityEnhancement");
            consoleOpacity = ReadConsoleOpacity();
            autoStart = AutoStartManager.IsEnabled();
            AutoStartManager.RefreshCommandIfEnabled();

            settingsSaveTimer = new System.Windows.Forms.Timer();
            settingsSaveTimer.Interval = 220;
            settingsSaveTimer.Tick += delegate
            {
                settingsSaveTimer.Stop();
                SaveSettings();
            };

            menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Font = new Font(
                Localization.PreferredFontFamily,
                9f);
            consoleItem = new ToolStripMenuItem(
                Localization.Get("openConsole"));
            showItem = new ToolStripMenuItem(
                Localization.Get("showWindow"));
            demoItem = new ToolStripMenuItem(
                Localization.Get("demoNotification"));
            musicModeItem = new ToolStripMenuItem(
                Localization.Get("musicMode"));
            musicModeItem.CheckOnClick = true;
            musicPersistentItem = new ToolStripMenuItem(
                Localization.Get("musicPersistent"));
            musicPersistentItem.CheckOnClick = true;
            pauseItem = new ToolStripMenuItem(
                Localization.Get("pauseReceive"));
            exitItem = new ToolStripMenuItem(
                Localization.Get("exitRestore"));

            showItem.Click += delegate
            {
                island.Show();
                island.Demonstrate();
            };
            consoleItem.Click += delegate { ShowConsole(); };
            demoItem.Click += delegate { island.Demonstrate(); };
            musicModeItem.Click += delegate { SetMusicMode(musicModeItem.Checked); };
            musicPersistentItem.Click += delegate
            {
                SetMusicPersistent(musicPersistentItem.Checked);
            };
            pauseItem.Click += delegate { TogglePause(); };
            exitItem.Click += delegate { ExitThread(); };
            menu.Items.Add(consoleItem);
            menu.Items.Add(showItem);
            menu.Items.Add(demoItem);
            menu.Items.Add(musicModeItem);
            menu.Items.Add(musicPersistentItem);
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            generatedIcon = CreateTrayIcon();
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = generatedIcon;
            notifyIcon.Text = FitTrayText(
                Localization.Get("appName") +
                " • " +
                Localization.Get(
                    muteBanners
                        ? "statusRunning"
                        : "demoBody"));
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { ShowConsole(); };

            island = new IslandForm();
            island.MediaCommandRequested += delegate(
                object sender,
                MediaCommandAction action)
            {
                mediaMonitor.SendCommand(action);
            };
            island.MusicWindowCloseRequested += delegate
            {
                if (musicMode)
                {
                    SetMusicMode(false);
                }
            };
            uiScale = ReadUiScale();
            island.SetUiScale(uiScale);
            island.SetGlassMode(transparentWindow);
            island.SetQualityEnhancement(qualityEnhancement);
            island.Show();
            island.DemonstrateStartup(muteBanners);

            monitor = new NotificationMonitor();
            monitor.NotificationReceived += OnNotificationReceived;
            monitor.StatusChanged += OnMonitorStatusChanged;
            monitor.Start();

            mediaMonitor = new MediaMonitor();
            mediaMonitor.StateChanged += OnMediaStateChanged;
            musicMode = ReadMusicMode();
            musicPersistent = ReadMusicPersistent();
            musicModeItem.Checked = musicMode;
            musicPersistentItem.Checked = musicPersistent;
            island.SetMusicMode(musicMode);
            island.SetMusicPinned(musicPersistent);
            if (musicMode)
            {
                mediaMonitor.Start();
            }
            hotkeys = new GlobalHotkeys(
                delegate { SetMusicMode(!musicMode); },
                delegate { SetMusicPersistent(!musicPersistent); });

            console = new ConsoleForm();
            console.PauseToggleRequested += delegate { TogglePause(); };
            console.MusicToggleRequested += delegate
            {
                SetMusicMode(!musicMode);
            };
            console.PersistentToggleRequested += delegate
            {
                SetMusicPersistent(!musicPersistent);
            };
            console.TestNotificationRequested += delegate
            {
                island.Demonstrate();
            };
            console.SettingsRequested += delegate
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        "ms-settings:notifications");
                }
                catch (Exception exception)
                {
                    Logger.Write(
                        "Open notification settings: " + exception);
                }
            };
            console.ExitRequested += delegate { ExitThread(); };
            console.GlassModeToggleRequested += delegate
            {
                transparentWindow = !transparentWindow;
                if (!transparentWindow)
                {
                    qualityEnhancement = false;
                }
                island.SetGlassMode(transparentWindow);
                island.SetQualityEnhancement(qualityEnhancement);
                SaveSettings();
                RefreshConsoleState();
            };
            console.QualityEnhanceToggleRequested += delegate
            {
                if (!transparentWindow)
                {
                    return;
                }
                qualityEnhancement = !qualityEnhancement;
                island.SetQualityEnhancement(qualityEnhancement);
                SaveSettings();
                RefreshConsoleState();
            };
            console.AutoStartToggleRequested += delegate
            {
                bool enabled = !autoStart;
                if (AutoStartManager.SetEnabled(enabled))
                {
                    autoStart = enabled;
                    RefreshConsoleState();
                }
            };
            console.ConsoleOpacityChanged += delegate(
                object sender,
                double opacity)
            {
                consoleOpacity = Math.Max(0.0, Math.Min(1.0, opacity));
                ScheduleSaveSettings();
            };
            console.SizeScaleChanged += delegate(
                object sender,
                double scale)
            {
                uiScale = Math.Max(0.80, Math.Min(1.20, scale));
                island.SetUiScale(uiScale);
                island.ShowScalePreview(uiScale);
                ScheduleSaveSettings();
                RefreshConsoleState();
            };
            console.LanguageChangeRequested += delegate(
                object sender,
                UiLanguage language)
            {
                Localization.SetLanguage(language);
                SaveSettings();
                RefreshLocalizedText();
                island.RefreshLanguage();
                RefreshConsoleState();
                console.RefreshLocalization();
            };
            RefreshConsoleState();
            if (showConsole)
            {
                console.ShowConsole();
            }
            showConsoleWait = ThreadPool.RegisterWaitForSingleObject(
                showConsoleEvent,
                delegate
                {
                    if (!island.IsDisposed)
                    {
                        island.BeginInvoke(new Action(ShowConsole));
                    }
                },
                null,
                Timeout.Infinite,
                false);

            bannerRefreshTimer = new System.Windows.Forms.Timer();
            bannerRefreshTimer.Interval = 3500;
            bannerRefreshTimer.Tick += delegate
            {
                if (muteBanners)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        bannerMuter.MuteCurrentApps();
                    });
                }
            };
            bannerRefreshTimer.Start();
        }

        private void OnNotificationReceived(object sender, NotificationItem item)
        {
            try
            {
                if (island.IsDisposed)
                {
                    return;
                }
                if (island.InvokeRequired)
                {
                    island.BeginInvoke(
                        new Action<object, NotificationItem>(
                            OnNotificationReceived),
                        new object[] { sender, item });
                    return;
                }
                if (!paused)
                {
                    island.Receive(item);
                }
            }
            catch (Exception exception)
            {
                Logger.Write("OnNotificationReceived: " + exception);
            }
        }

        private void OnMonitorStatusChanged(object sender, string status)
        {
            try
            {
                if (island.IsDisposed)
                {
                    return;
                }
                if (island.InvokeRequired)
                {
                    island.BeginInvoke(
                        new Action<object, string>(OnMonitorStatusChanged),
                        new object[] { sender, status });
                    return;
                }
                notifyIcon.Text = FitTrayText(status);
                RefreshConsoleState(status);
            }
            catch
            {
            }
        }

        private void TogglePause()
        {
            paused = !paused;
            pauseItem.Text = Localization.Get(
                paused ? "resumeReceive" : "pauseReceive");
            island.SetPaused(paused);
            RefreshConsoleState();
        }

        private void SetMusicMode(bool enabled)
        {
            musicMode = enabled;
            musicModeItem.Checked = enabled;
            SaveSettings();
            island.SetMusicMode(enabled);
            if (enabled)
            {
                mediaMonitor.Start();
            }
            else
            {
                mediaMonitor.Stop();
            }
            RefreshConsoleState();
        }

        private void SetMusicPersistent(bool enabled)
        {
            musicPersistent = enabled;
            musicPersistentItem.Checked = enabled;
            if (enabled && !musicMode)
            {
                SetMusicMode(true);
            }
            island.SetMusicPinned(enabled);
            SaveSettings();
            RefreshConsoleState();
        }

        private void ShowConsole()
        {
            if (console == null || console.IsDisposed)
            {
                return;
            }
            RefreshConsoleState();
            console.ShowConsole();
        }

        private void RefreshConsoleState(string status = null)
        {
            if (console == null || console.IsDisposed)
            {
                return;
            }
            console.SetState(
                paused,
                musicMode,
                musicPersistent,
                transparentWindow,
                qualityEnhancement,
                autoStart,
                status ??
                    Localization.Get(
                        paused ? "statusPaused" : "statusRunning"),
                uiScale,
                consoleOpacity);
        }

        private void RefreshLocalizedText()
        {
            Font previousFont = menu.Font;
            menu.Font = new Font(
                Localization.PreferredFontFamily,
                9f);
            if (previousFont != null)
            {
                previousFont.Dispose();
            }
            consoleItem.Text = Localization.Get("openConsole");
            showItem.Text = Localization.Get("showWindow");
            demoItem.Text = Localization.Get("demoNotification");
            musicModeItem.Text = Localization.Get("musicMode");
            musicPersistentItem.Text = Localization.Get("musicPersistent");
            pauseItem.Text = Localization.Get(
                paused ? "resumeReceive" : "pauseReceive");
            exitItem.Text = Localization.Get("exitRestore");
            notifyIcon.Text = FitTrayText(
                Localization.Get("appName") + " • " +
                Localization.Get(
                    paused ? "statusPaused" : "statusRunning"));
        }

        private static string FitTrayText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Smart Window";
            }
            return value.Length > 63
                ? value.Substring(0, 63)
                : value;
        }

        private void OnMediaStateChanged(
            object sender,
            MediaPlaybackState state)
        {
            try
            {
                if (island.IsDisposed)
                {
                    return;
                }
                if (island.InvokeRequired)
                {
                    island.BeginInvoke(
                        new Action<object, MediaPlaybackState>(
                            OnMediaStateChanged),
                        new object[] { sender, state });
                    return;
                }
                island.UpdateMedia(state);
            }
            catch (Exception exception)
            {
                Logger.Write("OnMediaStateChanged: " + exception);
            }
        }

        private bool ReadMusicMode()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return false;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    if (string.Equals(
                        lines[index].Trim(),
                        "musicMode=1",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadMusicMode: " + exception);
            }
            return false;
        }

        private bool ReadMusicPersistent()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return false;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    if (string.Equals(
                        lines[index].Trim(),
                        "musicPersistent=1",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadMusicPersistent: " + exception);
            }
            return false;
        }

        private UiLanguage ReadLanguage()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return UiLanguage.Chinese;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].Trim();
                    if (line.StartsWith(
                        "language=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return Localization.FromCode(
                            line.Substring("language=".Length));
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadLanguage: " + exception);
            }
            return UiLanguage.Chinese;
        }

        private double ReadUiScale()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return 1.0;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].Trim();
                    if (line.StartsWith(
                        "sizeScale=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        double value;
                        if (double.TryParse(
                            line.Substring("sizeScale=".Length),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out value))
                        {
                            return Math.Max(0.80, Math.Min(1.20, value));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadUiScale: " + exception);
            }
            return 1.0;
        }

        private bool ReadTransparentWindow()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return false;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    if (string.Equals(
                        lines[index].Trim(),
                        "transparentWindow=1",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadTransparentWindow: " + exception);
            }
            return false;
        }

        private bool ReadBooleanSetting(string key)
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return false;
                }
                string expected = key + "=1";
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    if (string.Equals(
                        lines[index].Trim(),
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadBooleanSetting: " + exception);
            }
            return false;
        }

        private double ReadConsoleOpacity()
        {
            try
            {
                if (!File.Exists(settingsPath))
                {
                    return 0.5;
                }
                string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index].Trim();
                    if (line.StartsWith(
                        "consoleOpacity=",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        double value;
                        if (double.TryParse(
                            line.Substring("consoleOpacity=".Length),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out value))
                        {
                            return Math.Max(0.0, Math.Min(1.0, value));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("ReadConsoleOpacity: " + exception);
            }
            return 0.5;
        }

        private void ScheduleSaveSettings()
        {
            settingsSaveTimer.Stop();
            settingsSaveTimer.Start();
        }

        private void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(settingsPath);
                Directory.CreateDirectory(directory);
                File.WriteAllText(
                    settingsPath,
                    "musicMode=" + (musicMode ? "1" : "0") +
                    Environment.NewLine +
                    "musicPersistent=" + (musicPersistent ? "1" : "0") +
                    Environment.NewLine +
                    "language=" + Localization.Code(Localization.Current) +
                    Environment.NewLine +
                    "sizeScale=" +
                    uiScale.ToString(
                        "0.###",
                        System.Globalization.CultureInfo.InvariantCulture) +
                    Environment.NewLine +
                    "transparentWindow=" +
                    (transparentWindow ? "1" : "0") +
                    Environment.NewLine +
                    "qualityEnhancement=" +
                    (qualityEnhancement ? "1" : "0") +
                    Environment.NewLine +
                    "consoleOpacity=" +
                    consoleOpacity.ToString(
                        "0.###",
                        System.Globalization.CultureInfo.InvariantCulture) +
                    Environment.NewLine,
                    Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Logger.Write("SaveSettings: " + exception);
            }
        }

        protected override void ExitThreadCore()
        {
            if (cleanedUp)
            {
                base.ExitThreadCore();
                return;
            }
            cleanedUp = true;
            bannerRefreshTimer.Stop();
            bannerRefreshTimer.Dispose();
            settingsSaveTimer.Stop();
            settingsSaveTimer.Dispose();
            monitor.Dispose();
            mediaMonitor.Dispose();
            hotkeys.Dispose();
            if (console != null && !console.IsDisposed)
            {
                console.CloseForExit();
                console.Dispose();
            }
            if (showConsoleWait != null)
            {
                showConsoleWait.Unregister(null);
            }
            showConsoleEvent.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            island.Close();
            island.Dispose();
            menu.Dispose();
            if (muteBanners)
            {
                bannerMuter.StopAndRestore();
            }
            generatedIcon.Dispose();
            base.ExitThreadCore();
        }

        private static Icon CreateTrayIcon()
        {
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(8, 8, 10)))
                {
                    graphics.FillEllipse(brush, 1, 1, 30, 30);
                }
                using (SolidBrush brush = new SolidBrush(Color.White))
                {
                    graphics.FillEllipse(brush, 10, 14, 4, 4);
                    graphics.FillEllipse(brush, 16, 12, 7, 7);
                    graphics.FillEllipse(brush, 25, 14, 4, 4);
                }
            }
            IntPtr handle = bitmap.GetHicon();
            bitmap.Dispose();
            try
            {
                using (Icon temporary = Icon.FromHandle(handle))
                {
                    return (Icon)temporary.Clone();
                }
            }
            finally
            {
                DestroyIcon(handle);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);
    }

    internal sealed class NotificationMonitor : IDisposable
    {
        private readonly object databaseSync = new object();
        private NotificationDatabase database;
        private System.Threading.Timer timer;
        private long lastOrder;
        private int queryInProgress;
        private bool initialized;
        private bool disposed;

        public event EventHandler<NotificationItem> NotificationReceived;
        public event EventHandler<string> StatusChanged;

        public void Start()
        {
            timer = new System.Threading.Timer(
                Poll,
                null,
                0,
                450);
        }

        private void Poll(object state)
        {
            if (disposed ||
                Interlocked.CompareExchange(ref queryInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                lock (databaseSync)
                {
                    if (database == null)
                    {
                        database = new NotificationDatabase();
                    }

                    if (!initialized)
                    {
                        lastOrder = database.GetLatestOrder();
                        initialized = true;
                        RaiseStatus(Localization.Get("statusRunning"));
                        return;
                    }

                    List<NotificationItem> items =
                        database.GetNewNotifications(lastOrder);
                    if (items.Count > 0)
                    {
                        long newest = lastOrder;
                        for (int index = 0; index < items.Count; index++)
                        {
                            if (items[index].CursorOrder > newest)
                            {
                                newest = items[index].CursorOrder;
                            }
                        }
                        lastOrder = newest;

                        for (int index = 0; index < items.Count; index++)
                        {
                            EventHandler<NotificationItem> handler =
                                NotificationReceived;
                            if (handler != null)
                            {
                                handler(this, items[index]);
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                lock (databaseSync)
                {
                    if (database != null)
                    {
                        database.Dispose();
                        database = null;
                    }
                    initialized = false;
                }
                RaiseStatus(Localization.Get("statusRetry"));
                Logger.Write("Monitor: " + exception);
            }
            finally
            {
                Interlocked.Exchange(ref queryInProgress, 0);
            }
        }

        private void RaiseStatus(string status)
        {
            EventHandler<string> handler = StatusChanged;
            if (handler != null)
            {
                handler(this, status);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (timer != null)
            {
                timer.Dispose();
                timer = null;
            }
            lock (databaseSync)
            {
                if (database != null)
                {
                    database.Dispose();
                    database = null;
                }
            }
        }
    }

    internal sealed class LegacyIslandForm : Form
    {
        private const int CollapsedWidth = 154;
        private const int CollapsedHeight = 34;
        private const int ExpandedWidth = 510;
        private const int ExpandedHeight = 112;

        private readonly Queue<NotificationItem> queue =
            new Queue<NotificationItem>();

        private readonly System.Windows.Forms.Timer animationTimer;
        private readonly System.Windows.Forms.Timer displayTimer;
        private readonly System.Windows.Forms.Timer nextTimer;
        private readonly Font appFont;
        private readonly Font titleFont;
        private readonly Font bodyFont;
        private readonly Font tinyFont;

        private NotificationItem current;
        private int targetWidth = CollapsedWidth;
        private int targetHeight = CollapsedHeight;
        private int currentWidth = CollapsedWidth;
        private int currentHeight = CollapsedHeight;
        private int radius = 17;
        private bool hovering;
        private bool paused;

        public LegacyIslandForm()
        {
            Text = "智慧窗";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(5, 5, 7);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            Width = CollapsedWidth;
            Height = CollapsedHeight;
            Cursor = Cursors.Hand;

            string fontName = "Microsoft YaHei UI";
            appFont = new Font(fontName, 8.2f, FontStyle.Regular);
            titleFont = new Font(fontName, 9.4f, FontStyle.Bold);
            bodyFont = new Font(fontName, 9.7f, FontStyle.Regular);
            tinyFont = new Font(fontName, 7.8f, FontStyle.Regular);

            animationTimer = new System.Windows.Forms.Timer();
            animationTimer.Interval = 16;
            animationTimer.Tick += Animate;

            displayTimer = new System.Windows.Forms.Timer();
            displayTimer.Interval = 5700;
            displayTimer.Tick += DisplayTimeout;

            nextTimer = new System.Windows.Forms.Timer();
            nextTimer.Interval = 260;
            nextTimer.Tick += ShowNext;

            MouseEnter += delegate
            {
                hovering = true;
                Invalidate();
            };
            MouseLeave += delegate
            {
                hovering = false;
                Invalidate();
            };
            MouseClick += OnIslandClick;
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
                return parameters;
            }
        }

        public void DemonstrateStartup(bool bannersMuted)
        {
            NotificationItem item = new NotificationItem();
            item.AppName = "智慧窗";
            item.Title = "已连接到屏幕顶部";
            item.Body = bannersMuted
                ? "右下角系统横幅已静默，通知会在这里展开"
                : "演示模式：系统横幅仍保持开启";
            Receive(item);
        }

        public void Demonstrate()
        {
            NotificationItem item = new NotificationItem();
            item.AppName = "智慧窗";
            item.Title = "这是一条演示通知";
            item.Body = "新消息会以胶囊形态出现，并自动展开、停留和收起";
            Receive(item);
        }

        public void SetPaused(bool value)
        {
            paused = value;
            if (paused)
            {
                current = null;
                queue.Clear();
                Collapse();
            }
            Invalidate();
        }

        public void Receive(NotificationItem item)
        {
            if (item == null || paused || IsDisposed)
            {
                return;
            }

            queue.Enqueue(item);
            if (current == null)
            {
                ShowNext(null, EventArgs.Empty);
            }
        }

        private void ShowNext(object sender, EventArgs e)
        {
            nextTimer.Stop();
            if (paused || queue.Count == 0)
            {
                Collapse();
                return;
            }

            current = queue.Dequeue();
            targetWidth = Scale(ExpandedWidth);
            targetHeight = Scale(ExpandedHeight);
            radius = Scale(27);
            animationTimer.Start();
            displayTimer.Stop();
            displayTimer.Start();
            Invalidate();
        }

        private void DisplayTimeout(object sender, EventArgs e)
        {
            displayTimer.Stop();
            if (hovering)
            {
                displayTimer.Start();
                return;
            }
            DismissCurrent();
        }

        private void DismissCurrent()
        {
            displayTimer.Stop();
            current = null;
            Collapse();
            if (queue.Count > 0)
            {
                nextTimer.Start();
            }
        }

        private void Collapse()
        {
            targetWidth = Scale(CollapsedWidth);
            targetHeight = Scale(CollapsedHeight);
            radius = Scale(17);
            animationTimer.Start();
            Invalidate();
        }

        private void Animate(object sender, EventArgs e)
        {
            int deltaWidth = targetWidth - currentWidth;
            int deltaHeight = targetHeight - currentHeight;
            if (Math.Abs(deltaWidth) <= 2 && Math.Abs(deltaHeight) <= 2)
            {
                currentWidth = targetWidth;
                currentHeight = targetHeight;
                animationTimer.Stop();
            }
            else
            {
                currentWidth += (int)Math.Round(deltaWidth * 0.24);
                currentHeight += (int)Math.Round(deltaHeight * 0.24);
            }
            ApplyGeometry();
        }

        private void ApplyGeometry()
        {
            Screen screen = Screen.PrimaryScreen;
            Rectangle bounds = screen.Bounds;
            int left = bounds.Left + (bounds.Width - currentWidth) / 2;
            int top = bounds.Top + Scale(8);
            SetBounds(left, top, currentWidth, currentHeight);

            using (GraphicsPath path = RoundedRectangle(
                new Rectangle(0, 0, currentWidth, currentHeight),
                Math.Min(radius, currentHeight / 2)))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
            Invalidate();
        }

        private int Scale(int logicalPixels)
        {
            using (Graphics graphics = CreateGraphics())
            {
                return (int)Math.Round(
                    logicalPixels * graphics.DpiX / 96f);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyGeometry();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle outer = new Rectangle(
                0,
                0,
                ClientSize.Width - 1,
                ClientSize.Height - 1);
            using (GraphicsPath path = RoundedRectangle(
                outer,
                Math.Min(radius, ClientSize.Height / 2)))
            {
                using (LinearGradientBrush background =
                    new LinearGradientBrush(
                        outer,
                        Color.FromArgb(8, 8, 10),
                        Color.FromArgb(2, 2, 3),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(background, path);
                }
                using (Pen border = new Pen(Color.FromArgb(52, 255, 255, 255), 1f))
                {
                    graphics.DrawPath(border, path);
                }
            }

            if (current == null)
            {
                DrawIdle(graphics);
            }
            else
            {
                DrawExpanded(graphics);
            }
        }

        private void DrawIdle(Graphics graphics)
        {
            int centerX = ClientSize.Width / 2;
            int centerY = ClientSize.Height / 2;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(105, 255, 255, 255)))
            using (SolidBrush bright = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            {
                graphics.FillEllipse(dim, centerX - Scale(18), centerY - Scale(2), Scale(4), Scale(4));
                graphics.FillEllipse(bright, centerX - Scale(5), centerY - Scale(2), Scale(5), Scale(5));
                graphics.FillEllipse(dim, centerX + Scale(10), centerY - Scale(2), Scale(4), Scale(4));
            }
        }

        private void DrawExpanded(Graphics graphics)
        {
            int padding = Scale(18);
            int avatarSize = Scale(38);
            int avatarTop = Scale(17);
            int textLeft = padding + avatarSize + Scale(14);
            int titleTop = Scale(15);
            int timeWidth = Scale(50);

            Color accent = AccentFor(current.AppName);
            Rectangle avatar = new Rectangle(padding, avatarTop, avatarSize, avatarSize);
            using (LinearGradientBrush avatarBrush =
                new LinearGradientBrush(
                    avatar,
                    Color.FromArgb(255, accent),
                    Color.FromArgb(185, accent),
                    45f))
            {
                graphics.FillEllipse(avatarBrush, avatar);
            }
            using (Pen highlight = new Pen(Color.FromArgb(55, 255, 255, 255), 1f))
            {
                graphics.DrawEllipse(highlight, avatar);
            }

            string monogram = Monogram(current.AppName);
            SizeF monogramSize = graphics.MeasureString(monogram, titleFont);
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                graphics.DrawString(
                    monogram,
                    titleFont,
                    white,
                    avatar.Left + (avatar.Width - monogramSize.Width) / 2f,
                    avatar.Top + (avatar.Height - monogramSize.Height) / 2f);
            }

            int rightPadding = Scale(20);
            Rectangle appRect = new Rectangle(
                textLeft,
                titleTop,
                ClientSize.Width - textLeft - timeWidth - rightPadding,
                Scale(20));
            TextRenderer.DrawText(
                graphics,
                current.AppName.ToUpperInvariant(),
                tinyFont,
                appRect,
                Color.FromArgb(160, 255, 255, 255),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            Rectangle titleRect = new Rectangle(
                textLeft,
                Scale(31),
                ClientSize.Width - textLeft - rightPadding,
                Scale(25));
            TextRenderer.DrawText(
                graphics,
                current.Title,
                titleFont,
                titleRect,
                Color.FromArgb(248, 255, 255, 255),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            if (!string.IsNullOrWhiteSpace(current.Body))
            {
                Rectangle bodyRect = new Rectangle(
                    textLeft,
                    Scale(56),
                    ClientSize.Width - textLeft - padding,
                    Scale(38));
                TextRenderer.DrawText(
                    graphics,
                    current.Body,
                    bodyFont,
                    bodyRect,
                    Color.FromArgb(215, 235, 235, 240),
                    TextFormatFlags.Left |
                    TextFormatFlags.Top |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.WordBreak |
                    TextFormatFlags.EndEllipsis);
            }

            Rectangle timeRect = new Rectangle(
                ClientSize.Width - timeWidth - Scale(13),
                titleTop,
                timeWidth,
                Scale(18));
            TextRenderer.DrawText(
                graphics,
                hovering ? "×" : "刚刚",
                tinyFont,
                timeRect,
                Color.FromArgb(
                    hovering ? 210 : 105,
                    255,
                    255,
                    255),
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            using (SolidBrush accentBrush = new SolidBrush(
                Color.FromArgb(185, accent)))
            {
                graphics.FillEllipse(
                    accentBrush,
                    ClientSize.Width - Scale(17),
                    Scale(54),
                    Scale(4),
                    Scale(4));
            }

            if (queue.Count > 0)
            {
                Rectangle queueRect = new Rectangle(
                    ClientSize.Width - Scale(44),
                    Scale(54),
                    Scale(25),
                    Scale(16));
                TextRenderer.DrawText(
                    graphics,
                    "+" + queue.Count.ToString(),
                    tinyFont,
                    queueRect,
                    Color.FromArgb(130, 255, 255, 255),
                    TextFormatFlags.Right |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
        }

        private void OnIslandClick(object sender, MouseEventArgs e)
        {
            if (current == null)
            {
                Demonstrate();
                return;
            }

            if (e.X > ClientSize.Width - Scale(54) &&
                e.Y < Scale(44))
            {
                DismissCurrent();
                return;
            }

            // A second click dismisses the current card; a third starts the
            // next queued notification without waiting for the full timeout.
            DismissCurrent();
        }

        private static Color AccentFor(string appName)
        {
            int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(appName ?? "");
            int index = hash % 5;
            if (index < 0)
            {
                index = -index;
            }
            Color[] palette = new Color[]
            {
                Color.FromArgb(45, 150, 255),
                Color.FromArgb(90, 200, 150),
                Color.FromArgb(245, 145, 55),
                Color.FromArgb(175, 100, 245),
                Color.FromArgb(230, 75, 115)
            };
            return palette[index];
        }

        private static string Monogram(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return "•";
            }
            string trimmed = appName.Trim();
            if (trimmed.Length == 1)
            {
                return trimmed.ToUpperInvariant();
            }
            return trimmed.Substring(0, 1).ToUpperInvariant();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
                displayTimer.Dispose();
                nextTimer.Dispose();
                appFont.Dispose();
                titleFont.Dispose();
                bodyFont.Dispose();
                tinyFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
