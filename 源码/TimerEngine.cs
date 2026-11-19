using System;
using System.Collections.Generic;
using System.Drawing;

namespace SmartWindow
{
    internal enum SmartTimerMode
    {
        Countdown,
        Stopwatch
    }

    internal sealed class TimerStartRequest
    {
        public SmartTimerMode Mode;
        public TimeSpan Duration;
    }

    internal sealed class TimerPreset
    {
        public string Name;
        public int DurationSeconds;

        public TimerPreset()
        {
        }

        public TimerPreset(string name, int durationSeconds)
        {
            Name = name;
            DurationSeconds = Math.Max(1, durationSeconds);
        }
    }

    internal sealed class TimerSnapshot
    {
        public SmartTimerMode Mode;
        public TimeSpan DisplayTime;
        public bool IsActive;
        public bool IsRunning;
        public bool IsPaused;
        public bool IsCompleted;
        public bool CountdownActive;
        public bool CountdownRunning;
        public bool CountdownPaused;
        public bool CountdownCompleted;
        public TimeSpan CountdownTime;
        public string CountdownFormattedTime;
        public bool StopwatchActive;
        public bool StopwatchRunning;
        public bool StopwatchPaused;
        public TimeSpan StopwatchTime;
        public string StopwatchFormattedTime;
        public int LapCount;
        public Color AccentColor;
        public string FormattedTime;
        public TimeSpan[] Laps;

        public bool IsModeActive(SmartTimerMode mode)
        {
            return mode == SmartTimerMode.Countdown
                ? CountdownActive
                : StopwatchActive;
        }

        public string FormatForMode(SmartTimerMode mode)
        {
            if (mode == SmartTimerMode.Countdown)
            {
                return CountdownActive
                    ? CountdownFormattedTime
                    : "00:00";
            }
            return StopwatchActive
                ? StopwatchFormattedTime
                : "00:00";
        }
    }

    internal sealed class SmartTimerSession
    {
        private sealed class Clock
        {
            public bool Active;
            public bool Running;
            public bool Paused;
            public bool Completed;
            public TimeSpan Duration;
            public TimeSpan Accumulated;
            public DateTime StartedAt;
            public readonly List<TimeSpan> Laps =
                new List<TimeSpan>();
            public TimeSpan LastLapElapsed;

            public TimeSpan Elapsed
            {
                get
                {
                    TimeSpan value = Accumulated;
                    if (Running)
                    {
                        value += DateTime.UtcNow - StartedAt;
                    }
                    return value;
                }
            }

            public TimeSpan Remaining
            {
                get
                {
                    TimeSpan elapsed = Elapsed;
                    return elapsed >= Duration
                        ? TimeSpan.Zero
                        : Duration - elapsed;
                }
            }
        }

        private readonly Clock countdown = new Clock();
        private readonly Clock stopwatch = new Clock();
        private Color accentColor = Color.FromArgb(100, 181, 246);

        public bool IsActive
        {
            get { return countdown.Active || stopwatch.Active; }
        }

        public bool CountdownActive
        {
            get { return countdown.Active; }
        }

        public bool StopwatchActive
        {
            get { return stopwatch.Active; }
        }

        public bool IsCompleted
        {
            get { return countdown.Active && countdown.Completed; }
        }

        public Color AccentColor
        {
            get { return accentColor; }
        }

        public void StartCountdown(TimeSpan requestedDuration)
        {
            countdown.Active = true;
            countdown.Running = true;
            countdown.Paused = false;
            countdown.Completed = false;
            countdown.Duration =
                requestedDuration < TimeSpan.FromSeconds(1)
                    ? TimeSpan.FromMinutes(1)
                    : requestedDuration;
            countdown.Accumulated = TimeSpan.Zero;
            countdown.StartedAt = DateTime.UtcNow;
            countdown.LastLapElapsed = TimeSpan.Zero;
            countdown.Laps.Clear();
        }

        public void StartStopwatch()
        {
            stopwatch.Active = true;
            stopwatch.Running = true;
            stopwatch.Paused = false;
            stopwatch.Completed = false;
            stopwatch.Duration = TimeSpan.Zero;
            stopwatch.Accumulated = TimeSpan.Zero;
            stopwatch.StartedAt = DateTime.UtcNow;
            stopwatch.LastLapElapsed = TimeSpan.Zero;
            stopwatch.Laps.Clear();
        }

        public void TogglePause(SmartTimerMode mode)
        {
            Clock clock = mode == SmartTimerMode.Countdown
                ? countdown
                : stopwatch;
            if (!clock.Active || clock.Completed)
            {
                return;
            }
            if (clock.Running)
            {
                clock.Accumulated = clock.Elapsed;
                clock.Running = false;
                clock.Paused = true;
            }
            else
            {
                clock.StartedAt = DateTime.UtcNow;
                clock.Running = true;
                clock.Paused = false;
            }
        }

        public void Cancel(SmartTimerMode mode)
        {
            Clock clock = mode == SmartTimerMode.Countdown
                ? countdown
                : stopwatch;
            clock.Active = false;
            clock.Running = false;
            clock.Paused = false;
            clock.Completed = false;
            clock.Accumulated = TimeSpan.Zero;
            clock.Duration = TimeSpan.Zero;
            clock.LastLapElapsed = TimeSpan.Zero;
            clock.Laps.Clear();
        }

        public void CancelAll()
        {
            Cancel(SmartTimerMode.Countdown);
            Cancel(SmartTimerMode.Stopwatch);
        }

        public bool AddLap(SmartTimerMode mode)
        {
            if (mode != SmartTimerMode.Stopwatch ||
                !stopwatch.Active ||
                stopwatch.Completed)
            {
                return false;
            }
            TimeSpan elapsed = stopwatch.Elapsed;
            TimeSpan lap = elapsed - stopwatch.LastLapElapsed;
            stopwatch.LastLapElapsed = elapsed;
            stopwatch.Laps.Insert(0, lap);
            if (stopwatch.Laps.Count > 24)
            {
                stopwatch.Laps.RemoveAt(stopwatch.Laps.Count - 1);
            }
            return true;
        }

        public void SetAccentColor(Color color)
        {
            accentColor = Color.FromArgb(255, color);
        }

        public void Tick()
        {
            if (countdown.Active &&
                countdown.Running &&
                countdown.Remaining <= TimeSpan.Zero)
            {
                countdown.Accumulated = countdown.Duration;
                countdown.Running = false;
                countdown.Paused = false;
                countdown.Completed = true;
            }
        }

        public TimerSnapshot CreateSnapshot(SmartTimerMode preferredMode)
        {
            SmartTimerMode visibleMode = countdown.Active
                ? SmartTimerMode.Countdown
                : (stopwatch.Active
                    ? SmartTimerMode.Stopwatch
                    : preferredMode);
            Clock visible = visibleMode == SmartTimerMode.Countdown
                ? countdown
                : stopwatch;
            TimeSpan display = visibleMode == SmartTimerMode.Countdown
                ? visible.Remaining
                : visible.Elapsed;
            TimerSnapshot snapshot = new TimerSnapshot();
            snapshot.Mode = visibleMode;
            snapshot.DisplayTime = display;
            snapshot.IsActive = visible.Active;
            snapshot.IsRunning = visible.Running;
            snapshot.IsPaused = visible.Paused;
            snapshot.IsCompleted = visible.Completed;
            snapshot.CountdownActive = countdown.Active;
            snapshot.CountdownRunning = countdown.Running;
            snapshot.CountdownPaused = countdown.Paused;
            snapshot.CountdownCompleted = countdown.Completed;
            snapshot.CountdownTime = countdown.Remaining;
            snapshot.CountdownFormattedTime =
                FormatTime(countdown.Remaining, true);
            snapshot.StopwatchActive = stopwatch.Active;
            snapshot.StopwatchRunning = stopwatch.Running;
            snapshot.StopwatchPaused = stopwatch.Paused;
            snapshot.StopwatchTime = stopwatch.Elapsed;
            snapshot.StopwatchFormattedTime =
                FormatTime(stopwatch.Elapsed, false);
            snapshot.LapCount = stopwatch.Laps.Count;
            snapshot.AccentColor = accentColor;
            snapshot.FormattedTime = FormatTime(
                display,
                visibleMode == SmartTimerMode.Countdown);
            snapshot.Laps = stopwatch.Laps.ToArray();
            return snapshot;
        }

        private static string FormatTime(TimeSpan value, bool roundUp)
        {
            double secondsValue = value.TotalSeconds;
            long totalSeconds = roundUp
                ? (long)Math.Ceiling(Math.Max(0.0, secondsValue))
                : (long)Math.Floor(Math.Max(0.0, secondsValue));
            long hours = totalSeconds / 3600;
            long minutes = (totalSeconds % 3600) / 60;
            long seconds = totalSeconds % 60;
            if (hours > 0)
            {
                return hours.ToString("00") +
                    ":" +
                    minutes.ToString("00") +
                    ":" +
                    seconds.ToString("00");
            }
            return minutes.ToString("00") +
                ":" +
                seconds.ToString("00");
        }
    }
}
