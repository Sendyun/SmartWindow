using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

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

        public static Bitmap LoadLogoLight()
        {
            return LoadBitmapResource(
                "SmartWindow.Resources.logo-light.png");
        }

        public static Bitmap LoadLogoDark()
        {
            return LoadBitmapResource(
                "SmartWindow.Resources.logo-dark.png");
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

        public static Bitmap LoadDonationAlipay()
        {
            return LoadBitmapResource(
                "SmartWindow.Resources.donation-alipay.jpg");
        }

        public static Bitmap LoadDonationWechat()
        {
            return LoadBitmapResource(
                "SmartWindow.Resources.donation-wechat.jpg");
        }

        private static Bitmap LoadBitmapResource(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream =
                assembly.GetManifestResourceStream(resourceName))
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

                if (File.Exists(destination) &&
                    resource.Length == new FileInfo(destination).Length &&
                    ResourceHashEquals(resource, destination))
                {
                    return;
                }

                string temporary = destination + ".tmp";
                using (FileStream output = new FileStream(
                    temporary,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    resource.CopyTo(output);
                }
                File.Copy(temporary, destination, true);
                File.Delete(temporary);
            }
        }

        private static bool ResourceHashEquals(
            Stream resource,
            string destination)
        {
            try
            {
                byte[] expected;
                using (SHA256 algorithm = SHA256.Create())
                {
                    expected = algorithm.ComputeHash(resource);
                }
                byte[] actual;
                using (FileStream file = new FileStream(
                    destination,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (SHA256 algorithm = SHA256.Create())
                {
                    actual = algorithm.ComputeHash(file);
                }
                if (expected.Length != actual.Length)
                {
                    return false;
                }
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] != actual[index])
                    {
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                resource.Position = 0;
            }
        }
    }
}
