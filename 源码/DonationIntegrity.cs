using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace SmartWindow
{
    internal static class DonationIntegrityGuard
    {
        private const string ExpectedSha256 =
            "9B1CFF2938EB32922299A72E09D935199148AC5670CF6EFF7FABE6001F964506";
        private static readonly string StateDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "SmartWindow");
        private static readonly string DamageMarker = Path.Combine(
            StateDirectory,
            "integrity-damaged.dat");

        public static string Url
        {
            get
            {
                return string.Concat(
                    "https://",
                    "cnsendyun",
                    ".com",
                    "/donate/");
            }
        }

        public static bool IsValid()
        {
            return IsCanonicalUrl(Url);
        }

        internal static bool IsCanonicalUrl(string candidate)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }
            string actual;
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(candidate));
                actual = BitConverter
                    .ToString(digest)
                    .Replace("-", "");
            }
            return ConstantTimeEquals(
                actual,
                ExpectedSha256);
        }

        public static bool EnforceOrShutdown()
        {
            string executableHash = GetExecutableHash();
            if (HasDamagedMarker(executableHash))
            {
                ShowDamagedMessage();
                return false;
            }

            if (!IsValid())
            {
                MarkDamaged(executableHash);
                ShowDamagedMessage();
                return false;
            }
            return true;
        }

        private static void ShowDamagedMessage()
        {
            MessageBox.Show(
                "检测到赞助网址被修改，关键文件完整性已受损。\n\n" +
                "当前程序已被标记为不可用，请从 Sendyun Technology Unit " +
                "官方渠道重新下载完整版本。\n\n" +
                "Critical application data has been damaged. " +
                "Please download a clean official build.",
                "智慧窗 · 关键文件受损",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static string GetExecutableHash()
        {
            try
            {
                string path =
                    System.Reflection.Assembly
                        .GetExecutingAssembly()
                        .Location;
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (SHA256 algorithm = SHA256.Create())
                {
                    return BitConverter
                        .ToString(algorithm.ComputeHash(stream))
                        .Replace("-", "");
                }
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        private static bool HasDamagedMarker(string executableHash)
        {
            try
            {
                if (!File.Exists(DamageMarker))
                {
                    return false;
                }
                string storedHash = File.ReadAllText(
                    DamageMarker,
                    Encoding.UTF8).Trim();
                if (string.Equals(
                    storedHash,
                    executableHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // A different executable hash means the damaged file was
                // replaced by a newly downloaded build.
                File.Delete(DamageMarker);
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void MarkDamaged(string executableHash)
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                File.WriteAllText(
                    DamageMarker,
                    executableHash,
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static bool ConstantTimeEquals(
            string left,
            string right)
        {
            if (left == null || right == null)
            {
                return false;
            }
            int difference = left.Length ^ right.Length;
            int length = Math.Min(left.Length, right.Length);
            for (int index = 0; index < length; index++)
            {
                difference |=
                    char.ToUpperInvariant(left[index]) ^
                    char.ToUpperInvariant(right[index]);
            }
            return difference == 0;
        }
    }
}
