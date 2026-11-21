using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class SmartWindowSetup
{
    private const string ProductName = "智慧窗";
    private const string ProductVersion = "1.3.0";
    private const string Publisher =
        "Mr.Dove (Sendyun Technology Unit)";
    private const string UninstallKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SmartWindow";

    [STAThread]
    private static int Main(string[] args)
    {
        bool createdNew;
        using (Mutex setupMutex = new Mutex(
            true,
            @"Local\SmartWindow.Setup",
            out createdNew))
        {
            if (!createdNew)
            {
                return 1618;
            }
            return Run(args);
        }
    }

    private static int Run(string[] args)
    {
        bool silent = HasArgument(args, "/S") ||
            HasArgument(args, "/s") ||
            HasArgument(args, "--silent") ||
            HasArgument(args, "/quiet");
        bool uninstall =
            HasArgument(args, "/uninstall") ||
            HasArgument(args, "--uninstall");

        string targetDirectory = GetTargetDirectory(args);
        if (uninstall)
        {
            return Uninstall(targetDirectory, silent);
        }
        if (!IsSupportedOperatingSystem())
        {
            if (!silent)
            {
                MessageBox.Show(
                    "智慧窗需要 Windows 10 1809（Build 17763）或更高版本。",
                    ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return 1633;
        }

        if (!silent)
        {
            DialogResult confirmation = MessageBox.Show(
                "将安装 " + ProductName + " Ver " + ProductVersion +
                "。\r\n\r\n安装位置：\r\n" + targetDirectory,
                ProductName + " 安装程序",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (confirmation != DialogResult.OK)
            {
                return 1602;
            }
        }

        try
        {
            Install(targetDirectory);
            if (!silent)
            {
                MessageBox.Show(
                    ProductName + " 已安装完成。",
                    ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return 0;
        }
        catch (Exception exception)
        {
            int exitCode = IsDiskFull(exception) ? 112 : 1603;
            if (!silent)
            {
                MessageBox.Show(
                    "安装失败：\r\n" + exception.Message,
                    ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return exitCode;
        }
    }

    private static bool IsDiskFull(Exception exception)
    {
        for (Exception current = exception;
            current != null;
            current = current.InnerException)
        {
            int code = current.HResult & 0xFFFF;
            if (code == 39 || code == 112)
            {
                return true;
            }
        }
        return false;
    }

    private static void Install(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        string installedExe = Path.Combine(
            targetDirectory,
            "SmartWindow.exe");
        StopInstalledApplication(installedExe);

        string temporaryExe = installedExe + ".new";
        ExtractResource(
            "SmartWindowInstaller.Payload.exe",
            temporaryExe);
        if (File.Exists(installedExe))
        {
            File.Delete(installedExe);
        }
        File.Move(temporaryExe, installedExe);

        string licensePath = Path.Combine(
            targetDirectory,
            "LICENSE.md");
        ExtractResource(
            "SmartWindowInstaller.LICENSE.md",
            licensePath);

        string uninstaller = Path.Combine(
            targetDirectory,
            "Uninstall SmartWindow.exe");
        string currentProcess = Process.GetCurrentProcess()
            .MainModule.FileName;
        if (!string.Equals(
            Path.GetFullPath(currentProcess),
            Path.GetFullPath(uninstaller),
            StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(currentProcess, uninstaller, true);
        }

        CreateShortcut(installedExe);
        WriteUninstallEntry(targetDirectory, uninstaller);
    }

    private static int Uninstall(
        string targetDirectory,
        bool silent)
    {
        string installedExe = Path.Combine(
            targetDirectory,
            "SmartWindow.exe");
        StopInstalledApplication(installedExe);
        RestoreNotificationBanners(installedExe);

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                UninstallKey,
                false);
        }
        catch
        {
        }

        try
        {
            string shortcut = GetStartMenuShortcutPath();
            if (File.Exists(shortcut))
            {
                File.Delete(shortcut);
            }
        }
        catch
        {
        }

        if (!silent)
        {
            MessageBox.Show(
                ProductName + " 已卸载。",
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        ScheduleDirectoryCleanup(targetDirectory);
        return 0;
    }

    private static void StopInstalledApplication(string installedExe)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(installedExe);
        }
        catch
        {
            return;
        }

        foreach (Process process in Process.GetProcessesByName("SmartWindow"))
        {
            try
            {
                string processPath = process.MainModule.FileName;
                if (!string.Equals(
                    Path.GetFullPath(processPath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                process.CloseMainWindow();
                if (!process.WaitForExit(1800))
                {
                    process.Kill();
                    process.WaitForExit(2500);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void RestoreNotificationBanners(string installedExe)
    {
        if (!File.Exists(installedExe))
        {
            return;
        }
        try
        {
            using (Process process = Process.Start(new ProcessStartInfo
            {
                FileName = installedExe,
                Arguments = "--restore",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                if (process != null)
                {
                    process.WaitForExit(2500);
                }
            }
        }
        catch
        {
        }
    }

    private static void WriteUninstallEntry(
        string targetDirectory,
        string uninstaller)
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
            UninstallKey))
        {
            if (key == null)
            {
                return;
            }
            key.SetValue("DisplayName", ProductName);
            key.SetValue("DisplayVersion", ProductVersion);
            key.SetValue("Publisher", Publisher);
            key.SetValue(
                "DisplayIcon",
                Path.Combine(targetDirectory, "SmartWindow.exe"));
            key.SetValue("InstallLocation", targetDirectory);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue(
                "UninstallString",
                "\"" + uninstaller + "\" /uninstall");
            key.SetValue(
                "QuietUninstallString",
                "\"" + uninstaller + "\" /uninstall /S");
        }
    }

    private static void CreateShortcut(string installedExe)
    {
        try
        {
            string shortcutPath = GetStartMenuShortcutPath();
            Directory.CreateDirectory(
                Path.GetDirectoryName(shortcutPath));
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                return;
            }
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember(
                "TargetPath",
                BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { installedExe });
            shortcutType.InvokeMember(
                "WorkingDirectory",
                BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { Path.GetDirectoryName(installedExe) });
            shortcutType.InvokeMember(
                "Description",
                BindingFlags.SetProperty,
                null,
                shortcut,
                new object[] { ProductName });
            shortcutType.InvokeMember(
                "Save",
                BindingFlags.InvokeMethod,
                null,
                shortcut,
                null);
        }
        catch
        {
        }
    }

    private static string GetStartMenuShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.Programs),
            ProductName + ".lnk");
    }

    private static string GetTargetDirectory(string[] args)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index] ?? "";
            if (argument.StartsWith(
                "/D=",
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(
                    argument.Substring(3).Trim('"'));
            }
        }
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "SmartWindow");
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

    private static bool IsSupportedOperatingSystem()
    {
        Version version = Environment.OSVersion.Version;
        return version.Major >= 10 && version.Build >= 17763;
    }

    private static void ExtractResource(
        string resourceName,
        string destination)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream resource =
            assembly.GetManifestResourceStream(resourceName))
        {
            if (resource == null)
            {
                throw new InvalidOperationException(
                    "Missing embedded resource: " + resourceName);
            }
            using (FileStream output = new FileStream(
                destination,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None))
            {
                resource.CopyTo(output);
            }
        }
    }

    private static void ScheduleDirectoryCleanup(string directory)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "cmd.exe");
            startInfo.Arguments =
                "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" +
                directory + "\"";
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(startInfo);
        }
        catch
        {
        }
    }
}

