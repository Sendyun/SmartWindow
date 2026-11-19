using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SmartWindow
{
    internal enum PerformanceHealth
    {
        Idle,
        Stable,
        Variable,
        Jank
    }

    internal enum PerformanceEventKind
    {
        DroppedFrame,
        Jank,
        SevereJank
    }

    internal sealed class PerformanceEventItem
    {
        public DateTime Timestamp;
        public PerformanceEventKind Kind;
        public double FrameTimeMilliseconds;
        public double FramesPerSecond;
    }

    internal sealed class PerformanceSnapshot
    {
        public double CurrentFps;
        public int TargetFps;
        public double AverageFps;
        public double MinimumFps;
        public double OnePercentLowFps;
        public double AverageFrameTimeMs;
        public double LastFrameTimeMs;
        public double FrameTimeDeviationMs;
        public double AverageRenderTimeMs;
        public double DroppedFrameRatio;
        public int JankEventCount;
        public int SevereJankEventCount;
        public int StabilityScore;
        public PerformanceHealth Health;
        public double SystemCpuPercent;
        public double GpuUsagePercent;
        public double ProcessCpuPercent;
        public double WorkingSetMegabytes;
        public double SystemMemoryPercent;
        public int RefreshRateHz;
        public int ScreenWidth;
        public int ScreenHeight;
        public TimeSpan Uptime;
        public PerformanceEventItem[] Events;
    }

    internal sealed class IslandPerformanceMonitor
    {
        private struct FrameSample
        {
            public double Seconds;
            public double IntervalMilliseconds;
            public double RenderMilliseconds;
            public int TargetFps;
        }

        private readonly object sync = new object();
        private readonly Queue<FrameSample> samples =
            new Queue<FrameSample>();
        private readonly List<PerformanceEventItem> events =
            new List<PerformanceEventItem>();
        private readonly long originTimestamp = Stopwatch.GetTimestamp();
        private long previousFrameTimestamp;
        private int consecutiveJankFrames;
        private bool severeJankRecorded;

        public void RecordFrame(
            long renderStartTimestamp,
            long renderEndTimestamp,
            int targetFps)
        {
            double seconds =
                (renderStartTimestamp - originTimestamp) /
                (double)Stopwatch.Frequency;
            double interval = 0.0;
            if (previousFrameTimestamp != 0)
            {
                interval =
                    (renderStartTimestamp - previousFrameTimestamp) *
                    1000.0 /
                    Stopwatch.Frequency;
            }
            previousFrameTimestamp = renderStartTimestamp;

            double renderMilliseconds =
                (renderEndTimestamp - renderStartTimestamp) *
                1000.0 /
                Stopwatch.Frequency;
            FrameSample sample = new FrameSample();
            sample.Seconds = seconds;
            sample.IntervalMilliseconds = interval;
            sample.RenderMilliseconds = renderMilliseconds;
            sample.TargetFps = Math.Max(1, targetFps);

            lock (sync)
            {
                samples.Enqueue(sample);
                while (samples.Count > 0 &&
                    seconds - samples.Peek().Seconds > 12.0)
                {
                    samples.Dequeue();
                }

                if (interval <= 0.0)
                {
                    return;
                }

                double targetInterval = 1000.0 / sample.TargetFps;
                double currentFps = Math.Min(
                    sample.TargetFps * 2.0,
                    1000.0 / interval);
                if (interval > targetInterval * 2.0)
                {
                    consecutiveJankFrames++;
                    AddEvent(
                        DateTime.Now,
                        consecutiveJankFrames >= 3
                            ? PerformanceEventKind.SevereJank
                            : PerformanceEventKind.Jank,
                        interval,
                        currentFps);
                    if (consecutiveJankFrames >= 3)
                    {
                        severeJankRecorded = true;
                    }
                }
                else
                {
                    if (consecutiveJankFrames >= 3 &&
                        !severeJankRecorded)
                    {
                        AddEvent(
                            DateTime.Now,
                            PerformanceEventKind.SevereJank,
                            interval,
                            currentFps);
                    }
                    consecutiveJankFrames = 0;
                    severeJankRecorded = false;
                }

                if (interval > targetInterval * 1.5 &&
                    interval <= targetInterval * 2.0)
                {
                    AddEvent(
                        DateTime.Now,
                        PerformanceEventKind.DroppedFrame,
                        interval,
                        currentFps);
                }
            }
        }

        private void AddEvent(
            DateTime timestamp,
            PerformanceEventKind kind,
            double frameTime,
            double fps)
        {
            if (events.Count > 0)
            {
                PerformanceEventItem previous =
                    events[events.Count - 1];
                if (kind == previous.Kind &&
                    (timestamp - previous.Timestamp).TotalMilliseconds < 300.0)
                {
                    return;
                }
            }

            PerformanceEventItem item = new PerformanceEventItem();
            item.Timestamp = timestamp;
            item.Kind = kind;
            item.FrameTimeMilliseconds = frameTime;
            item.FramesPerSecond = fps;
            events.Add(item);
            if (events.Count > 30)
            {
                events.RemoveAt(0);
            }
        }

        public PerformanceSnapshot GetSnapshot()
        {
            PerformanceSnapshot snapshot;
            lock (sync)
            {
                double now =
                    (Stopwatch.GetTimestamp() - originTimestamp) /
                    (double)Stopwatch.Frequency;
                List<FrameSample> recent = samples
                    .Where(item => now - item.Seconds <= 10.0)
                    .ToList();
                List<FrameSample> lastSecond = recent
                    .Where(item => now - item.Seconds <= 1.0)
                    .ToList();
                List<double> intervals = recent
                    .Where(item => item.IntervalMilliseconds > 0.0)
                    .Select(item => item.IntervalMilliseconds)
                    .ToList();

                snapshot = new PerformanceSnapshot();
                snapshot.TargetFps = recent.Count > 0
                    ? recent[recent.Count - 1].TargetFps
                    : 30;
                snapshot.CurrentFps = CalculateFps(lastSecond);
                snapshot.AverageFps = CalculateFps(recent);
                snapshot.LastFrameTimeMs = lastSecond.Count > 0
                    ? lastSecond[lastSecond.Count - 1]
                        .IntervalMilliseconds
                    : 0.0;
                snapshot.AverageFrameTimeMs = intervals.Count > 0
                    ? intervals.Average()
                    : 0.0;

                if (intervals.Count > 0)
                {
                    List<double> ordered = intervals
                        .OrderBy(value => value)
                        .ToList();
                    int lowIndex = Math.Min(
                        ordered.Count - 1,
                        (int)Math.Floor((ordered.Count - 1) * 0.99));
                    double onePercentFrameTime = ordered[lowIndex];
                    snapshot.OnePercentLowFps =
                        onePercentFrameTime > 0.0
                            ? 1000.0 / onePercentFrameTime
                            : 0.0;

                    double average = snapshot.AverageFrameTimeMs;
                    double variance = intervals.Average(
                        value => (value - average) * (value - average));
                    snapshot.FrameTimeDeviationMs =
                        Math.Sqrt(Math.Max(0.0, variance));

                    double targetInterval =
                        1000.0 / snapshot.TargetFps;
                    int dropped = intervals.Count(
                        value => value > targetInterval * 1.5);
                    snapshot.DroppedFrameRatio =
                        dropped / (double)intervals.Count;
                }

                snapshot.MinimumFps = CalculateMinimumFps(recent);
                snapshot.AverageRenderTimeMs = recent.Count > 0
                    ? recent.Average(item => item.RenderMilliseconds)
                    : 0.0;
                snapshot.JankEventCount = events.Count(
                    item => item.Kind != PerformanceEventKind.DroppedFrame);
                snapshot.SevereJankEventCount = events.Count(
                    item => item.Kind == PerformanceEventKind.SevereJank);
                snapshot.StabilityScore = CalculateStability(
                    snapshot,
                    intervals.Count);
                snapshot.Events = events
                    .Skip(Math.Max(0, events.Count - 6))
                    .ToArray();
            }

            RuntimeTelemetry telemetry = RuntimeTelemetry.Capture();
            snapshot.SystemCpuPercent = telemetry.SystemCpuPercent;
            snapshot.GpuUsagePercent = telemetry.GpuUsagePercent;
            snapshot.ProcessCpuPercent = telemetry.ProcessCpuPercent;
            snapshot.WorkingSetMegabytes = telemetry.WorkingSetMegabytes;
            snapshot.SystemMemoryPercent = telemetry.SystemMemoryPercent;
            snapshot.RefreshRateHz = telemetry.RefreshRateHz;
            snapshot.ScreenWidth = telemetry.ScreenWidth;
            snapshot.ScreenHeight = telemetry.ScreenHeight;
            snapshot.Uptime = telemetry.Uptime;
            snapshot.Health = DetermineHealth(snapshot);
            return snapshot;
        }

        private static double CalculateFps(
            IList<FrameSample> frameSamples)
        {
            if (frameSamples.Count < 2)
            {
                return 0.0;
            }
            double duration =
                frameSamples[frameSamples.Count - 1].Seconds -
                frameSamples[0].Seconds;
            return duration > 0.001
                ? (frameSamples.Count - 1) / duration
                : 0.0;
        }

        private static double CalculateMinimumFps(
            IList<FrameSample> frameSamples)
        {
            if (frameSamples.Count < 2)
            {
                return 0.0;
            }
            double minimum = double.MaxValue;
            int bucket = -1;
            int count = 0;
            double bucketStart = 0.0;
            for (int index = 0; index < frameSamples.Count; index++)
            {
                int nextBucket = (int)Math.Floor(
                    frameSamples[index].Seconds);
                if (bucket >= 0 && nextBucket != bucket)
                {
                    if (count > 1)
                    {
                        minimum = Math.Min(minimum, count);
                    }
                    count = 0;
                }
                bucket = nextBucket;
                if (count == 0)
                {
                    bucketStart = frameSamples[index].Seconds;
                }
                count++;
            }
            if (count > 1)
            {
                minimum = Math.Min(minimum, count);
            }
            return minimum == double.MaxValue ? 0.0 : minimum;
        }

        private static int CalculateStability(
            PerformanceSnapshot snapshot,
            int sampleCount)
        {
            if (sampleCount < 3 || snapshot.AverageFps <= 0.0)
            {
                return 0;
            }
            double deviationPenalty = Math.Min(
                45.0,
                snapshot.FrameTimeDeviationMs * 3.0);
            double dropPenalty = Math.Min(
                45.0,
                snapshot.DroppedFrameRatio * 220.0);
            return Math.Max(
                0,
                Math.Min(
                    100,
                    (int)Math.Round(100.0 - deviationPenalty - dropPenalty)));
        }

        private static PerformanceHealth DetermineHealth(
            PerformanceSnapshot snapshot)
        {
            if (snapshot.CurrentFps < 0.1)
            {
                return PerformanceHealth.Idle;
            }
            double ratio = snapshot.CurrentFps /
                Math.Max(1.0, snapshot.TargetFps);
            if (ratio >= 0.88 && snapshot.StabilityScore >= 75)
            {
                return PerformanceHealth.Stable;
            }
            if (ratio >= 0.58 && snapshot.StabilityScore >= 42)
            {
                return PerformanceHealth.Variable;
            }
            return PerformanceHealth.Jank;
        }
    }

    internal sealed class RuntimeTelemetry
    {
        private struct FileTime
        {
            public uint Low;
            public uint High;

            public ulong Value
            {
                get { return ((ulong)High << 32) | Low; }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        private static readonly object sync = new object();
        private static ulong previousIdle;
        private static ulong previousKernel;
        private static ulong previousUser;
        private static TimeSpan previousProcessCpu;
        private static long previousProcessSample;
        private static DateTime previousWall = DateTime.MinValue;
        private static DateTime nextGpuSample = DateTime.MinValue;
        private static double cachedGpuUsage = -1.0;
        private static int gpuSamplePending;
        private static DateTime processStartedAt = DateTime.MinValue;

        public double SystemCpuPercent;
        public double GpuUsagePercent;
        public double ProcessCpuPercent;
        public double WorkingSetMegabytes;
        public double SystemMemoryPercent;
        public int RefreshRateHz;
        public int ScreenWidth;
        public int ScreenHeight;
        public TimeSpan Uptime;

        public static RuntimeTelemetry Capture()
        {
            RuntimeTelemetry result = new RuntimeTelemetry();
            DateTime now = DateTime.UtcNow;
            lock (sync)
            {
                FileTime idle;
                FileTime kernel;
                FileTime user;
                if (GetSystemTimes(
                    out idle,
                    out kernel,
                    out user))
                {
                    ulong idleValue = idle.Value;
                    ulong kernelValue = kernel.Value;
                    ulong userValue = user.Value;
                    if (previousKernel != 0 || previousUser != 0)
                    {
                        ulong idleDelta = idleValue - previousIdle;
                        ulong kernelDelta = kernelValue - previousKernel;
                        ulong userDelta = userValue - previousUser;
                        ulong total = kernelDelta + userDelta;
                        if (total > 0)
                        {
                            result.SystemCpuPercent =
                                (total - idleDelta) * 100.0 / total;
                        }
                    }
                    previousIdle = idleValue;
                    previousKernel = kernelValue;
                    previousUser = userValue;
                }

                try
                {
                    using (Process process = Process.GetCurrentProcess())
                    {
                        process.Refresh();
                        TimeSpan processCpu = process.TotalProcessorTime;
                        if (previousProcessSample != 0 &&
                            previousWall != DateTime.MinValue)
                        {
                            double wallSeconds =
                                (now - previousWall).TotalSeconds;
                            if (wallSeconds > 0.001)
                            {
                                result.ProcessCpuPercent =
                                    (processCpu - previousProcessCpu)
                                        .TotalSeconds *
                                    100.0 /
                                    wallSeconds /
                                    Math.Max(1, Environment.ProcessorCount);
                            }
                        }
                        previousProcessCpu = processCpu;
                        previousProcessSample = now.Ticks;
                        result.WorkingSetMegabytes =
                            process.WorkingSet64 / 1048576.0;
                        if (processStartedAt == DateTime.MinValue)
                        {
                            processStartedAt =
                                process.StartTime.ToUniversalTime();
                        }
                        result.Uptime = now - processStartedAt;
                    }
                }
                catch
                {
                }

                MemoryStatus memory = new MemoryStatus();
                memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
                if (GlobalMemoryStatusEx(ref memory))
                {
                    result.SystemMemoryPercent = memory.MemoryLoad;
                }

                if (now >= nextGpuSample)
                {
                    nextGpuSample = now.AddSeconds(5.0);
                    QueueGpuSample();
                }
                result.GpuUsagePercent = cachedGpuUsage;

                IntPtr screenDc = GetDC(IntPtr.Zero);
                if (screenDc != IntPtr.Zero)
                {
                    result.RefreshRateHz = GetDeviceCaps(screenDc, 116);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                result.ScreenWidth = bounds.Width;
                result.ScreenHeight = bounds.Height;
                previousWall = now;
            }
            result.SystemCpuPercent = Math.Max(
                0.0,
                Math.Min(100.0, result.SystemCpuPercent));
            result.ProcessCpuPercent = Math.Max(
                0.0,
                Math.Min(100.0, result.ProcessCpuPercent));
            return result;
        }

        private static void QueueGpuSample()
        {
            if (Interlocked.CompareExchange(
                ref gpuSamplePending,
                1,
                0) != 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    int processId = Process.GetCurrentProcess().Id;
                    string pidToken = "pid_" + processId + "_";
                    double totalGpu = 0.0;
                    using (ManagementObjectSearcher searcher =
                        new ManagementObjectSearcher(
                            "root\\CIMV2",
                            "SELECT Name,UtilizationPercentage " +
                            "FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))
                    using (ManagementObjectCollection results =
                        searcher.Get())
                    {
                        foreach (ManagementObject item in results)
                        {
                            string name = Convert.ToString(item["Name"]);
                            if (!string.IsNullOrEmpty(name) &&
                                name.IndexOf(
                                    pidToken,
                                    StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                object value = item["UtilizationPercentage"];
                                if (value != null)
                                {
                                    totalGpu += Convert.ToDouble(value);
                                }
                            }
                            item.Dispose();
                        }
                    }
                    cachedGpuUsage = Math.Max(
                        0.0,
                        Math.Min(100.0, totalGpu));
                }
                catch
                {
                    cachedGpuUsage = -1.0;
                }
                finally
                {
                    Interlocked.Exchange(ref gpuSamplePending, 0);
                }
            });
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(
            ref MemoryStatus memoryStatus);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr dc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr dc, int index);
    }
}
