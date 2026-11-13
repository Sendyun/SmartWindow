using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartWindow
{
    internal sealed class GlobalMouseHook : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WmLeftButtonDown = 0x0201;

        private readonly Action<int, int> leftButtonDown;
        private readonly MouseHookProc hookCallback;
        private IntPtr hookHandle;

        public GlobalMouseHook(Action<int, int> leftButtonDown)
        {
            this.leftButtonDown = leftButtonDown;
            hookCallback = MouseHook;

            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule module = process.MainModule)
            {
                hookHandle = SetWindowsHookEx(
                    WhMouseLl,
                    hookCallback,
                    GetModuleHandle(module.ModuleName),
                    0);
            }
            if (hookHandle == IntPtr.Zero)
            {
                Logger.Write(
                    "Mouse hook failed: " +
                    Marshal.GetLastWin32Error());
            }
        }

        private IntPtr MouseHook(
            int code,
            IntPtr wordParameter,
            IntPtr longParameter)
        {
            if (code >= 0 &&
                wordParameter.ToInt32() == WmLeftButtonDown)
            {
                MouseHookData data = (MouseHookData)Marshal.PtrToStructure(
                    longParameter,
                    typeof(MouseHookData));
                Action<int, int> handler = leftButtonDown;
                if (handler != null)
                {
                    handler(data.Point.X, data.Point.Y);
                }
            }
            return CallNextHookEx(
                hookHandle,
                code,
                wordParameter,
                longParameter);
        }

        public void Dispose()
        {
            if (hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private delegate IntPtr MouseHookProc(
            int code,
            IntPtr wordParameter,
            IntPtr longParameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseHookData
        {
            public NativePoint Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int hookType,
            MouseHookProc callback,
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

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);
    }
}
