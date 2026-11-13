using System;
using System.IO;

namespace IPBanManager.Services
{
    /// <summary>
    /// 应用数据目录。默认 %APPDATA%\IPBanManager，可用 --data-dir 覆盖，便携使用也方便。
    /// </summary>
    public static class StoragePaths
    {
        private static string _root;

        public static string Root
        {
            get
            {
                EnsureInitialized();
                return _root;
            }
        }

        public static string DataFile { get { return Path.Combine(Root, "data.json"); } }
        public static string SettingsFile { get { return Path.Combine(Root, "settings.json"); } }
        public static string ThemeFile { get { return Path.Combine(Root, "theme.json"); } }
        public static string LogsDirectory { get { return Path.Combine(Root, "logs"); } }
        public static string BackupsDirectory { get { return Path.Combine(Root, "backups"); } }
        public static string AssetsDirectory { get { return Path.Combine(Root, "assets"); } }

        public static void Initialize(string customRoot)
        {
            if (!string.IsNullOrWhiteSpace(customRoot))
            {
                _root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(customRoot));
            }
            else
            {
                _root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IPBanManager");
            }

            Directory.CreateDirectory(_root);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
            Directory.CreateDirectory(AssetsDirectory);
        }

        public static string EnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return Root;
            string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            Directory.CreateDirectory(full);
            return full;
        }

        private static void EnsureInitialized()
        {
            if (string.IsNullOrEmpty(_root)) Initialize(null);
        }
    }
}
