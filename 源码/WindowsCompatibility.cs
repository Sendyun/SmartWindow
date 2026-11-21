using System;
using System.Runtime.InteropServices;

namespace SmartWindow
{
    internal static class WindowsCompatibility
    {
        public const int MinimumSupportedBuild = 17763;
        private const int Windows11Build = 22000;
        private static bool initialized;
        private static int buildNumber;
        private static int majorVersion;
        private static int minorVersion;
        private static string productName = "";

        public static int BuildNumber
        {
            get
            {
                EnsureInitialized();
                return buildNumber;
            }
        }

        public static bool IsWindows10
        {
            get
            {
                EnsureInitialized();
                return majorVersion == 10 &&
                    buildNumber >= MinimumSupportedBuild &&
                    buildNumber < Windows11Build;
            }
        }

        public static bool IsWindows11
        {
            get
            {
                EnsureInitialized();
                return majorVersion == 10 &&
                    buildNumber >= Windows11Build;
            }
        }

        public static bool IsSupported
        {
            get
            {
                EnsureInitialized();
                return majorVersion == 10 &&
                    buildNumber >= MinimumSupportedBuild;
            }
        }

        public static bool SupportsMediaSessions
        {
            get { return IsSupported; }
        }

        public static string ProductName
        {
            get
            {
                EnsureInitialized();
                return productName;
            }
        }

        public static string DisplayName
        {
            get
            {
                EnsureInitialized();
                string family = IsWindows11
                    ? "Windows 11"
                    : (IsWindows10 ? "Windows 10" : "Windows");
                return family + " Build " + buildNumber.ToString();
            }
        }

        public static string MinimumRequirementText
        {
            get
            {
                return "Windows 10 1809（Build 17763）或更高版本";
            }
        }

        public static void Initialize()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            OsVersionInfoEx info = new OsVersionInfoEx();
            info.Size = Marshal.SizeOf(typeof(OsVersionInfoEx));
            try
            {
                int status = RtlGetVersion(ref info);
                if (status != 0 || info.MajorVersion <= 0)
                {
                    throw new InvalidOperationException(
                        "RtlGetVersion returned " + status.ToString());
                }
                majorVersion = info.MajorVersion;
                minorVersion = info.MinorVersion;
                buildNumber = info.BuildNumber;
            }
            catch (Exception exception)
            {
                Version fallback = Environment.OSVersion.Version;
                majorVersion = fallback.Major;
                minorVersion = fallback.Minor;
                buildNumber = fallback.Build;
                Logger.Write(
                    "WindowsCompatibility fallback: " + exception);
            }

            productName = majorVersion >= 10
                ? "Windows"
                : "Unsupported Windows";
            initialized = true;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OsVersionInfoEx
        {
            public int Size;
            public int MajorVersion;
            public int MinorVersion;
            public int BuildNumber;
            public int PlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;

            public ushort SuiteMask;
            public byte ProductType;
            public byte Reserved;
        }

        [DllImport("ntdll.dll", EntryPoint = "RtlGetVersion")]
        private static extern int RtlGetVersion(
            ref OsVersionInfoEx versionInfo);
    }
}
