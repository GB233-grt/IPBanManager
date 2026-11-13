using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using IPBanManager.Models;

namespace IPBanManager.Services
{
    /// <summary>
    /// 业务数据、设置、主题的加载与修复。
    /// </summary>
    public static class DataService
    {
        public static AppData LoadData(out string warning)
        {
            warning = "";
            AppData data = null;
            bool isNewFile = !File.Exists(StoragePaths.DataFile);

            if (File.Exists(StoragePaths.DataFile))
            {
                try
                {
                    data = JsonHelper.Load<AppData>(StoragePaths.DataFile);
                }
                catch (Exception ex)
                {
                    warning = "数据文件损坏，已重置并保留原文件：" + ex.Message;
                    LogService.Error("读取 data.json 失败", ex);
                    TryPreserveCorruptFile(StoragePaths.DataFile);
                }
            }

            if (data == null) data = new AppData();
            if (data.Rules == null) data.Rules = new ObservableCollectionCompat<BanRule>();
            if (data.Groups == null) data.Groups = new ObservableCollectionCompat<BanGroup>();
            if (data.Operations == null) data.Operations = new ObservableCollectionCompat<OperationRecord>();

            int removedNulls = RemoveNullItems(data.Rules) + RemoveNullItems(data.Groups) + RemoveNullItems(data.Operations);
            int repairedGroups = EnsureGroups(data);
            int repairedRules = NormalizeRules(data);
            int trimmed = TrimOperations(data, 500);

            if (!isNewFile && (repairedGroups > 0 || repairedRules > 0 || trimmed > 0 || removedNulls > 0))
            {
                if (warning.Length > 0) warning += "；";
                warning += "已自动修复数据：分组 " + repairedGroups + "，规则 " + repairedRules + "，清理日志 " + trimmed;
                if (removedNulls > 0) warning += "，移除空记录 " + removedNulls;
            }

            return data;
        }

        private static int RemoveNullItems<T>(ObservableCollection<T> items) where T : class
        {
            if (items == null) return 0;
            List<T> nulls = new List<T>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == null) nulls.Add(items[i]);
            }
            foreach (T item in nulls) items.Remove(item);
            return nulls.Count;
        }

        public static AppSettings LoadSettings()
        {
            AppSettings settings = null;
            if (File.Exists(StoragePaths.SettingsFile))
            {
                try
                {
                    settings = JsonHelper.Load<AppSettings>(StoragePaths.SettingsFile);
                }
                catch (Exception ex)
                {
                    LogService.Error("读取 settings.json 失败，使用默认设置", ex);
                }
            }
            if (settings == null) settings = AppSettings.CreateDefault();
            settings.Normalize();
            try
            {
                StoragePaths.EnsureDirectory(settings.BackupDirectory);
            }
            catch (Exception ex)
            {
                LogService.Error("创建备份目录失败，回退到默认目录", ex);
                settings.BackupDirectory = StoragePaths.BackupsDirectory;
            }
            return settings;
        }

        public static ThemeSettings LoadTheme()
        {
            ThemeSettings theme = null;
            if (File.Exists(StoragePaths.ThemeFile))
            {
                try
                {
                    theme = JsonHelper.Load<ThemeSettings>(StoragePaths.ThemeFile);
                }
                catch (Exception ex)
                {
                    LogService.Error("读取 theme.json 失败，使用默认主题", ex);
                }
            }
            if (theme == null) theme = ThemeSettings.CreateDefault();
            theme.Normalize();
            return theme;
        }

        public static void SaveData(AppData data)
        {
            JsonHelper.Save(StoragePaths.DataFile, data);
        }

        public static void SaveSettings(AppSettings settings)
        {
            settings.Normalize();
            JsonHelper.Save(StoragePaths.SettingsFile, settings);
        }

        public static void SaveTheme(ThemeSettings theme)
        {
            theme.Normalize();
            JsonHelper.Save(StoragePaths.ThemeFile, theme);
        }

        public static void ExportTheme(string path, ThemeSettings theme)
        {
            JsonHelper.Save(path, theme);
        }

        public static ThemeSettings ImportTheme(string path)
        {
            ThemeSettings theme = JsonHelper.Load<ThemeSettings>(path);
            if (theme == null) throw new InvalidDataException("主题文件内容为空或格式不正确");
            theme.Normalize();
            return theme;
        }

        private static int EnsureGroups(AppData data)
        {
            List<BanGroup> builtIns = BanGroup.CreateBuiltIns();
            int added = 0;
            foreach (BanGroup builtIn in builtIns)
            {
                bool exists = false;
                foreach (BanGroup group in data.Groups)
                {
                    if (string.Equals(group.Id, builtIn.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    data.Groups.Add(builtIn);
                    added++;
                }
            }

            foreach (BanGroup group in data.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.Color)) group.Color = "#4C8DFF";
                if (string.IsNullOrWhiteSpace(group.Name)) group.Name = "未命名分组";
            }
            return added;
        }

        private static int NormalizeRules(AppData data)
        {
            int repaired = 0;
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<BanRule> duplicates = new List<BanRule>();
            HashSet<string> groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BanGroup group in data.Groups) groupIds.Add(group.Id);

            foreach (BanRule rule in data.Rules)
            {
                if (rule == null) continue;
                if (string.IsNullOrEmpty(rule.Id)) rule.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(rule.Domain))
                {
                    rule.Enabled = false;
                    repaired++;
                }
                else
                {
                    string domain;
                    string error;
                    if (DomainUtil.TryNormalize(rule.Domain, out domain, out error))
                    {
                        if (rule.Domain != domain)
                        {
                            rule.Domain = domain;
                            repaired++;
                        }
                    }
                    else
                    {
                        rule.Enabled = false;
                        repaired++;
                    }
                }

                if (!string.Equals(rule.IpMode, "custom", StringComparison.OrdinalIgnoreCase))
                {
                    if (rule.IpMode != "auto")
                    {
                        rule.IpMode = "auto";
                        repaired++;
                    }
                    rule.CustomIp = "";
                }
                else
                {
                    string ip;
                    string ipError;
                    if (!DomainUtil.TryNormalizeIp(rule.CustomIp, out ip, out ipError))
                    {
                        rule.IpMode = "auto";
                        rule.CustomIp = "";
                        repaired++;
                    }
                    else if (rule.CustomIp != ip)
                    {
                        rule.CustomIp = ip;
                        repaired++;
                    }
                }

                if (string.IsNullOrEmpty(rule.GroupId) || !groupIds.Contains(rule.GroupId))
                {
                    rule.GroupId = "custom";
                    repaired++;
                }
                if (rule.Remark == null) rule.Remark = "";
                if (rule.CreatedAt == DateTime.MinValue) rule.CreatedAt = DateTime.Now;

                if (!string.IsNullOrEmpty(rule.Domain))
                {
                    if (seen.Contains(rule.Domain))
                    {
                        duplicates.Add(rule);
                        repaired++;
                        continue;
                    }
                    seen.Add(rule.Domain);
                }
            }

            foreach (BanRule duplicate in duplicates)
            {
                data.Rules.Remove(duplicate);
                LogService.Warn("已移除重复域名规则：" + duplicate.Domain);
            }
            return repaired;
        }

        private static int TrimOperations(AppData data, int max)
        {
            int removed = 0;
            while (data.Operations.Count > max)
            {
                data.Operations.RemoveAt(data.Operations.Count - 1);
                removed++;
            }
            return removed;
        }

        private static void TryPreserveCorruptFile(string path)
        {
            try
            {
                string target = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                File.Copy(path, target, true);
            }
            catch (Exception ex)
            {
                LogService.Error("保留损坏数据文件失败", ex);
            }
        }

        /// <summary>
        /// 兼容反序列化时可能出现的 null 集合。ObservableCollection 本身由 JavaScriptSerializer 创建，
        /// 这里使用一个可实例化的别名，避免重复写泛型。
        /// </summary>
        private class ObservableCollectionCompat<T> : System.Collections.ObjectModel.ObservableCollection<T>
        {
        }
    }
}
