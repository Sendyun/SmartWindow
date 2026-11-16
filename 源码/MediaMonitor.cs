using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SmartWindow
{
    internal sealed class MediaPlaybackState
    {
        public bool IsPlaying;
        public string AppId;
        public string AppName;
        public string Title;
        public string Artist;
        public string Album;
        public string Signature;
        public string ArtworkPath;
        public double PositionSeconds;
        public double DurationSeconds;
    }

    internal sealed class MediaMonitor : IDisposable
    {
        private readonly string scriptPath;
        private readonly object processSync = new object();
        private Process process;
        private Process commandProcess;
        private bool stopping;
        private string lastAppId;
        private int restartPending;

        public event EventHandler<MediaPlaybackState> StateChanged;

        public MediaMonitor()
        {
            scriptPath = RuntimeAssets.MediaMonitorScriptPath;
        }

        public bool IsRunning
        {
            get
            {
                Process current = process;
                return current != null && !current.HasExited;
            }
        }

        public string CurrentAppId
        {
            get { return lastAppId ?? ""; }
        }

        public void Start()
        {
            if (IsRunning)
            {
                EnsureCommandDaemon();
                return;
            }
            if (!File.Exists(scriptPath))
            {
                Logger.Write("MediaMonitor script missing: " + scriptPath);
                RaiseStopped();
                return;
            }

            stopping = false;
            Process previous = process;
            if (previous != null)
            {
                try
                {
                    previous.Dispose();
                }
                catch
                {
                }
                process = null;
            }
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            startInfo.Arguments =
                "-NoLogo -NoProfile -NonInteractive -WindowStyle Hidden " +
                "-STA " +
                "-ExecutionPolicy Bypass -File \"" + scriptPath + "\" " +
                "-ParentPid " + Process.GetCurrentProcess().Id;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = new UTF8Encoding(false);
            startInfo.StandardErrorEncoding = new UTF8Encoding(false);
            startInfo.WorkingDirectory =
                AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += OnOutputDataReceived;
                process.ErrorDataReceived += OnErrorDataReceived;
                process.Exited += OnProcessExited;
                if (!process.Start())
                {
                    throw new InvalidOperationException(
                        "Windows PowerShell did not start.");
                }
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                EnsureCommandDaemon();
            }
            catch (Exception exception)
            {
                Logger.Write("MediaMonitor.Start: " + exception);
                Stop();
                RaiseStopped();
            }
        }

        private void OnOutputDataReceived(
            object sender,
            DataReceivedEventArgs eventArgs)
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }

            try
            {
                string[] parts = eventArgs.Data.Split('\t');
                if (parts.Length < 2 ||
                    !string.Equals(parts[0], "SW1", StringComparison.Ordinal))
                {
                    return;
                }

                MediaPlaybackState state = new MediaPlaybackState();
                state.IsPlaying = parts[1] == "1";
                if (state.IsPlaying && parts.Length >= 8)
                {
                    state.AppId = Decode(parts[2]);
                    state.Title = Decode(parts[3]);
                    state.Artist = Decode(parts[4]);
                    state.Album = Decode(parts[5]);
                    state.Signature = Decode(parts[6]);
                    state.ArtworkPath = Decode(parts[7]);
                    if (parts.Length >= 10)
                    {
                        double position;
                        double duration;
                        double.TryParse(
                            parts[8],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out position);
                        double.TryParse(
                            parts[9],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out duration);
                        state.PositionSeconds = Math.Max(0, position);
                        state.DurationSeconds = Math.Max(0, duration);
                    }
                    state.AppName = FriendlyMediaAppName(state.AppId);
                    lastAppId = state.AppId;

                    if (!string.IsNullOrWhiteSpace(state.ArtworkPath) &&
                        !File.Exists(state.ArtworkPath))
                    {
                        state.ArtworkPath = "";
                    }
                }

                EventHandler<MediaPlaybackState> handler = StateChanged;
                if (handler != null)
                {
                    handler(this, state);
                }
            }
            catch (Exception exception)
            {
                Logger.Write("MediaMonitor output: " + exception);
            }
        }

        public void SendCommand(MediaCommandAction action)
        {
            string command;
            if (action == MediaCommandAction.Previous)
            {
                command = "previous";
            }
            else if (action == MediaCommandAction.Next)
            {
                command = "next";
            }
            else
            {
                command = "toggle";
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                if (!TrySendSessionCommand(command))
                {
                    SendMediaKey(action);
                }
            });
        }

        public void SendSeek(double seconds)
        {
            string command = "seek:" +
                Math.Max(0, seconds).ToString(
                    "0.###",
                    System.Globalization.CultureInfo.InvariantCulture);
            ThreadPool.QueueUserWorkItem(delegate
            {
                TrySendSessionCommand(command);
            });
        }

        private bool TrySendSessionCommand(string command)
        {
            try
            {
                EnsureCommandDaemon();
                string pipeName =
                    "SmartWindow.MediaCommands." +
                    Process.GetCurrentProcess().Id;
                using (NamedPipeClientStream client =
                    new NamedPipeClientStream(
                        ".",
                        pipeName,
                        PipeDirection.InOut,
                        PipeOptions.None))
                {
                    try
                    {
                        client.Connect(220);
                    }
                    catch (TimeoutException)
                    {
                        Logger.Write("Media command pipe connection timed out.");
                        return false;
                    }
                    using (StreamWriter writer = new StreamWriter(
                        client,
                        new UTF8Encoding(false),
                        1024,
                        true))
                    using (StreamReader reader = new StreamReader(
                        client,
                        Encoding.UTF8,
                        false,
                        1024,
                        true))
                    {
                        string encodedAppId = Convert.ToBase64String(
                            Encoding.UTF8.GetBytes(lastAppId ?? ""));
                        writer.WriteLine(command + "\t" + encodedAppId);
                        writer.Flush();
                        string output = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(output))
                        {
                            return false;
                        }
                        output = output.Trim();
                        return string.Equals(
                            output,
                            "True",
                            StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception exception)
            {
                Logger.Write("MediaMonitor.SendCommand: " + exception);
                return false;
            }
        }

        private void EnsureCommandDaemon()
        {
            lock (processSync)
            {
                Process current = commandProcess;
                try
                {
                    if (current != null && !current.HasExited)
                    {
                        return;
                    }
                }
                catch
                {
                }
                if (current != null)
                {
                    try
                    {
                        current.Dispose();
                    }
                    catch
                    {
                    }
                    commandProcess = null;
                }

                string helper = RuntimeAssets.MediaCommandScriptPath;
                if (!File.Exists(helper))
                {
                    Logger.Write("MediaCommand script missing: " + helper);
                    return;
                }
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.Windows),
                        "System32",
                        "WindowsPowerShell",
                        "v1.0",
                        "powershell.exe");
                    startInfo.Arguments =
                        "-NoLogo -NoProfile -NonInteractive -STA " +
                        "-WindowStyle Hidden -ExecutionPolicy Bypass " +
                        "-File \"" + helper + "\" -Server -ParentPid " +
                        Process.GetCurrentProcess().Id;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                    startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.StandardOutputEncoding = new UTF8Encoding(false);
                    startInfo.StandardErrorEncoding = new UTF8Encoding(false);
                    startInfo.WorkingDirectory =
                        AppDomain.CurrentDomain.BaseDirectory;
                    Process daemon = new Process();
                    daemon.StartInfo = startInfo;
                    daemon.EnableRaisingEvents = true;
                    daemon.OutputDataReceived += delegate(
                        object sender,
                        DataReceivedEventArgs eventArgs)
                    {
                        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                        {
                            Logger.Write(
                                "MediaCommand daemon: " +
                                eventArgs.Data);
                        }
                    };
                    daemon.ErrorDataReceived += delegate(
                        object sender,
                        DataReceivedEventArgs eventArgs)
                    {
                        if (!string.IsNullOrWhiteSpace(eventArgs.Data))
                        {
                            Logger.Write(
                                "MediaCommand daemon error: " +
                                eventArgs.Data);
                        }
                    };
                    if (!daemon.Start())
                    {
                        daemon.Dispose();
                        return;
                    }
                    daemon.BeginOutputReadLine();
                    daemon.BeginErrorReadLine();
                    commandProcess = daemon;
                }
                catch (Exception exception)
                {
                    Logger.Write(
                        "MediaMonitor.EnsureCommandDaemon: " +
                        exception);
                }
            }
        }

        private static void SendMediaKey(MediaCommandAction action)
        {
            int key = action == MediaCommandAction.Previous
                ? 0xB1
                : (action == MediaCommandAction.Next ? 0xB0 : 0xB3);
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            Thread.Sleep(18);
            keybd_event((byte)key, 0, 2, UIntPtr.Zero);
        }

        private void OnErrorDataReceived(
            object sender,
            DataReceivedEventArgs eventArgs)
        {
            if (!string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                Logger.Write("MediaMonitor: " + eventArgs.Data);
            }
        }

        private void OnProcessExited(object sender, EventArgs eventArgs)
        {
            if (!stopping)
            {
                Logger.Write("MediaMonitor process exited unexpectedly.");
                if (Interlocked.Exchange(ref restartPending, 1) == 0)
                {
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            Thread.Sleep(1200);
                            if (!stopping && !IsRunning)
                            {
                                Logger.Write(
                                    "Restarting media monitor.");
                                Start();
                            }
                        }
                        finally
                        {
                            Interlocked.Exchange(ref restartPending, 0);
                        }
                    });
                }
            }
        }

        private void RaiseStopped()
        {
            MediaPlaybackState state = new MediaPlaybackState();
            EventHandler<MediaPlaybackState> handler = StateChanged;
            if (handler != null)
            {
                handler(this, state);
            }
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return Encoding.UTF8.GetString(
                Convert.FromBase64String(value));
        }

        private static string FriendlyMediaAppName(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
            {
                return Localization.Get("media");
            }
            if (appId.IndexOf("Spotify", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Spotify";
            }
            if (appId.IndexOf("QQMusic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                appId.IndexOf("qqmusic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "QQ音乐";
            }
            if (appId.IndexOf("cloudmusic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                appId.IndexOf("NetEase", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "网易云音乐";
            }
            if (appId.IndexOf("ZuneMusic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                appId.IndexOf("MediaPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "媒体播放器";
            }
            if (appId.IndexOf("MSEdge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Microsoft Edge";
            }
            if (appId.IndexOf("Chrome", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Chrome";
            }
            if (appId.IndexOf("Firefox", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Firefox";
            }
            if (appId.IndexOf("foobar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "foobar2000";
            }
            if (appId.IndexOf("AIMP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "AIMP";
            }
            if (appId.IndexOf("PotPlayer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "PotPlayer";
            }

            try
            {
                string leaf = Path.GetFileNameWithoutExtension(appId);
                if (!string.IsNullOrWhiteSpace(leaf))
                {
                    return leaf;
                }
            }
            catch
            {
            }
            return Localization.Get("media");
        }

        public void Stop()
        {
            stopping = true;
            Process current = process;
            process = null;
            Process daemon;
            lock (processSync)
            {
                daemon = commandProcess;
                commandProcess = null;
            }

            if (current != null)
            {
                try
                {
                    current.OutputDataReceived -= OnOutputDataReceived;
                    current.ErrorDataReceived -= OnErrorDataReceived;
                    current.Exited -= OnProcessExited;
                    if (!current.HasExited)
                    {
                        current.Kill();
                        current.WaitForExit(1000);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Write("MediaMonitor.Stop: " + exception);
                }
                finally
                {
                    current.Dispose();
                }
            }

            if (daemon != null)
            {
                try
                {
                    if (!daemon.HasExited)
                    {
                        daemon.Kill();
                        daemon.WaitForExit(500);
                    }
                }
                catch
                {
                }
                finally
                {
                    daemon.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Stop();
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(
            byte virtualKey,
            byte scanCode,
            uint flags,
            UIntPtr extraInfo);
    }
}
