using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace SmartWindow
{
    internal static class RuntimeAssets
    {
        private static bool initialized;
        private static string scriptDirectory;
        private static PrivateFontCollection embeddedFonts;
        private static FontFamily embeddedFamily;

        public static string MediaMonitorScriptPath
        {
            get { return Path.Combine(scriptDirectory, "MediaMonitor.ps1"); }
        }

        public static string MediaCommandScriptPath
        {
            get { return Path.Combine(scriptDirectory, "MediaCommand.ps1"); }
        }

        public static string UiFontPath
        {
            get { return Path.Combine(scriptDirectory, "HYWenHei-85W.otf"); }
        }

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            scriptDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "SmartWindow",
                "runtime");
            Directory.CreateDirectory(scriptDirectory);
            ExtractResource(
                "SmartWindow.Resources.MediaMonitor.ps1",
                MediaMonitorScriptPath);
            ExtractResource(
                "SmartWindow.Resources.MediaCommand.ps1",
                MediaCommandScriptPath);
            ExtractResource(
                "SmartWindow.Resources.HYWenHei-85W.otf",
                UiFontPath);
            try
            {
                embeddedFonts = new PrivateFontCollection();
                embeddedFonts.AddFontFile(UiFontPath);
                if (embeddedFonts.Families.Length > 0)
                {
                    embeddedFamily = embeddedFonts.Families[0];
                }
            }
            catch (Exception exception)
            {
                Logger.Write("Embedded font: " + exception);
            }
            initialized = true;
        }

        public static Font CreateUiFont(
            float pixelSize,
            FontStyle style)
        {
            if (Localization.Current == UiLanguage.Chinese &&
                embeddedFamily != null)
            {
                try
                {
                    return new Font(
                        embeddedFamily,
                        pixelSize,
                        style,
                        GraphicsUnit.Pixel);
                }
                catch
                {
                }
            }
            return new Font(
                Localization.PreferredFontFamily,
                pixelSize,
                style,
                GraphicsUnit.Pixel);
        }

        public static Bitmap LoadLogo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(
                "SmartWindow.Resources.logo.png"))
            {
                if (stream == null)
                {
                    return null;
                }
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
        }

        public static Bitmap LoadGithubLogo()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(
                "SmartWindow.Resources.github.png"))
            {
                if (stream == null)
                {
                    return null;
                }
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
        }

        private static void ExtractResource(
            string resourceName,
            string destination)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream resource = assembly.GetManifestResourceStream(resourceName))
            {
                if (resource == null)
                {
                    throw new InvalidOperationException(
                        "Embedded resource is missing: " + resourceName);
                }
                using (FileStream output = new FileStream(
                    destination,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                {
                    resource.CopyTo(output);
                }
            }
        }
    }
}
