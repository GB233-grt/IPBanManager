using System;
using System.IO;
using System.Text;
using IPBanManager.Services;

namespace IPBanManager.Models
{
    /// <summary>
    /// 应用设置，持久化到 %APPDATA%\IPBanManager\settings.json。
    /// </summary>
    public class AppSettings
    {
        public AppSettings()
        {
            HostsPath = "";
            BackupDirectory = "";
            DnsFlushMode = "auto";
            BlockTemplate = "0.0.0.0 {domain}";
            RedirectTemplate = "127.0.0.1 {domain}";
            AutoExpandSubdomains = false;
            ExpansionPrefixes = "www,m,api";
            AutoApplyHosts = true;
            WriteUtf8Bom = false;
            BackupRetention = 50;
            TrayEnabled = true;
            MinimizeToTray = true;
            CloseToTray = true;
            StartWithWindows = false;
            StartMinimized = false;
            MaxLogRecords = 500;
        }

        public string HostsPath { get; set; }
        public string BackupDirectory { get; set; }
        public string DnsFlushMode { get; set; }
        public string BlockTemplate { get; set; }
        public string RedirectTemplate { get; set; }
        public bool AutoExpandSubdomains { get; set; }
        public string ExpansionPrefixes { get; set; }
        public bool AutoApplyHosts { get; set; }
        public bool WriteUtf8Bom { get; set; }
        public int BackupRetention { get; set; }
        public bool TrayEnabled { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool CloseToTray { get; set; }
        public bool StartWithWindows { get; set; }
        public bool StartMinimized { get; set; }
        public int MaxLogRecords { get; set; }

        /// <summary>系统 HOSTS 的默认位置（x64 进程）。</summary>
        public static string DefaultHostsPath
        {
            get
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                return Path.Combine(windows, "System32", "drivers", "etc", "hosts");
            }
        }

        public static AppSettings CreateDefault()
        {
            AppSettings settings = new AppSettings();
            settings.HostsPath = DefaultHostsPath;
            settings.BackupDirectory = "";
            return settings;
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(HostsPath)) HostsPath = DefaultHostsPath;
            HostsPath = Environment.ExpandEnvironmentVariables(HostsPath.Trim());

            if (string.IsNullOrWhiteSpace(BackupDirectory))
                BackupDirectory = StoragePaths.BackupsDirectory;
            else
                BackupDirectory = Environment.ExpandEnvironmentVariables(BackupDirectory.Trim());

            if (string.IsNullOrWhiteSpace(BlockTemplate)) BlockTemplate = "0.0.0.0 {domain}";
            if (string.IsNullOrWhiteSpace(RedirectTemplate)) RedirectTemplate = "127.0.0.1 {domain}";
            BlockTemplate = SanitizeTemplate(BlockTemplate);
            RedirectTemplate = SanitizeTemplate(RedirectTemplate);
            if (!BlockTemplate.Contains("{domain}")) BlockTemplate = BlockTemplate + " {domain}";
            if (!RedirectTemplate.Contains("{domain}")) RedirectTemplate = RedirectTemplate + " {domain}";

            if (string.Equals(DnsFlushMode, "api", StringComparison.OrdinalIgnoreCase)) DnsFlushMode = "api";
            else if (string.Equals(DnsFlushMode, "ipconfig", StringComparison.OrdinalIgnoreCase)) DnsFlushMode = "ipconfig";
            else DnsFlushMode = "auto";

            if (string.IsNullOrWhiteSpace(ExpansionPrefixes)) ExpansionPrefixes = "www,m,api";
            if (BackupRetention < 1) BackupRetention = 50;
            if (BackupRetention > 500) BackupRetention = 500;
            if (MaxLogRecords < 50) MaxLogRecords = 500;
            if (MaxLogRecords > 2000) MaxLogRecords = 2000;
        }

        public string DnsModeDisplay
        {
            get
            {
                if (string.Equals(DnsFlushMode, "api", StringComparison.OrdinalIgnoreCase)) return "Windows API";
                if (string.Equals(DnsFlushMode, "ipconfig", StringComparison.OrdinalIgnoreCase)) return "ipconfig /flushdns";
                return "自动（API + ipconfig）";
            }
        }

        private static string SanitizeTemplate(string value)
        {
            if (value == null) return "";
            // HOSTS 模板必须保持单行，防止导入的 JSON 通过换行注入额外 HOSTS 行。
            string text = value.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
            while (text.Contains("  ")) text = text.Replace("  ", " ");
            return text;
        }
    }
}
