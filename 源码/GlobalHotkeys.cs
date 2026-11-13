using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartWindow
{
    internal sealed class GlobalHotkeys : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int WmKeyUp = 0x0101;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;
        private const int VkMenu = 0x12;
        private const int VkLWin = 0x5B;
        private const int VkRWin = 0x5C;
        private const int VkM = 0x4D;
        private const int VkL = 0x4C;

        private readonly Action toggleMusicMode;
        private readonly Action toggleMusicPersistent;
        private readonly LowLevelKeyboardProc hookCallback;
        private readonly System.Windows.Forms.Timer sequenceTimer;
        private IntPtr hookHandle;
        private bool mKeyDown;
        private bool sequenceArmed;
        private DateTime sequenceStarted;

        public GlobalHotkeys(
            Action toggleMusicMode,
            Action toggleMusicPersistent)
        {
            this.toggleMusicMode = toggleMusicMode;
            this.toggleMusicPersistent = toggleMusicPersistent;
            hookCallback = KeyboardHook;

            sequenceTimer = new System.Windows.Forms.Timer();
            sequenceTimer.Interval = 550;
            sequenceTimer.Tick += delegate
            {
                sequenceTimer.Stop();
                if (sequenceArmed)
                {
                    sequenceArmed = false;
                    toggleMusicMode();
                }
            };

            using (System.Diagnostics.Process process =
                System.Diagnostics.Process.GetCurrentProcess())
            using (System.Diagnostics.ProcessModule module =
                process.MainModule)
            {
                hookHandle = SetWindowsHookEx(
                    WhKeyboardLl,
                    hookCallback,
                    GetModuleHandle(module.ModuleName),
                    0);
            }
            if (hookHandle == IntPtr.Zero)
            {
                Logger.Write(
                    "SetWindowsHookEx failed: " +
                    Marshal.GetLastWin32Error());
            }
        }

        private IntPtr KeyboardHook(
            int code,
            IntPtr wordParameter,
            IntPtr longParameter)
        {
            if (code >= 0)
            {
                int message = wordParameter.ToInt32();
                KeyboardHookData data = (KeyboardHookData)Marshal.PtrToStructure(
                    longParameter,
                    typeof(KeyboardHookData));

                if (message == WmKeyUp && data.KeyCode == VkM)
                {
                    mKeyDown = false;
                }
                else if (message == WmKeyDown || message == WmSysKeyDown)
                {
                    bool controlDown =
                        (GetAsyncKeyState(VkControl) & 0x8000) != 0;
                    bool competingModifier =
                        (GetAsyncKeyState(VkShift) & 0x8000) != 0 ||
                        (GetAsyncKeyState(VkMenu) & 0x8000) != 0 ||
                        (GetAsyncKeyState(VkLWin) & 0x8000) != 0 ||
                        (GetAsyncKeyState(VkRWin) & 0x8000) != 0;

                    if (controlDown && !competingModifier &&
                        data.KeyCode == VkM &&
                        !mKeyDown)
                    {
                        mKeyDown = true;
                        sequenceArmed = true;
                        sequenceStarted = DateTime.UtcNow;
                        sequenceTimer.Stop();
                        sequenceTimer.Start();
                        return new IntPtr(1);
                    }

                    if (controlDown && !competingModifier &&
                        data.KeyCode == VkL &&
                        sequenceArmed &&
                        (DateTime.UtcNow - sequenceStarted).TotalMilliseconds <= 700)
                    {
                        sequenceTimer.Stop();
                        sequenceArmed = false;
                        toggleMusicPersistent();
                        return new IntPtr(1);
                    }
                }
            }
            return CallNextHookEx(hookHandle, code, wordParameter, longParameter);
        }

        public void Dispose()
        {
            sequenceTimer.Stop();
            sequenceTimer.Dispose();
            if (hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(
            int code,
            IntPtr wordParameter,
            IntPtr longParameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardHookData
        {
            public int KeyCode;
            public int ScanCode;
            public int Flags;
            public int Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int hookType,
            LowLevelKeyboardProc callback,
            IntPtr module,
            uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(
            IntPtr hook,
            int code,
            IntPtr wordParameter,
            IntPtr longParameter);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
