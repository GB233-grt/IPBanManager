using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IPBanManager.Models;

namespace IPBanManager.Services
{
    public class HostsApplyResult
    {
        public HostsApplyResult()
        {
            Conflicts = new List<string>();
        }

        public bool Success { get; set; }
        public bool RequiresElevation { get; set; }
        public bool DnsSuccess { get; set; }
        public string Message { get; set; }
        public string Error { get; set; }
        public string BackupPath { get; set; }
        public string DnsDetail { get; set; }
        public List<string> Conflicts { get; set; }
        public int ActiveRuleCount { get; set; }
        public int DisabledRuleCount { get; set; }
        public DateTime AppliedAt { get; set; }
    }

    /// <summary>
    /// HOSTS 读取、托管块生成、去重、备份、原子写入和 DNS 刷新。
    ///
    /// 核心约定：只维护“IPBan Manager 托管区域”标记块内的内容，块外的用户自有记录永不改写；
    /// 每次保存前必定先备份；先写临时文件再覆盖，尽量降低写坏系统文件的风险。
    /// </summary>
    public static class HostsManager
    {
        public const string StartMarker = "# ==== IPBan Manager 托管区域 开始 ====";
        public const string EndMarker = "# ==== IPBan Manager 托管区域 结束 ====";

        public static HostsApplyResult Apply(AppSettings settings, IList<BanRule> rules, IList<BanGroup> groups)
        {
            HostsApplyResult result = new HostsApplyResult();
            if (settings == null) settings = AppSettings.CreateDefault();
            settings.Normalize();
            if (rules == null) rules = new List<BanRule>();
            if (groups == null) groups = new List<BanGroup>();

            string hostsPath = settings.HostsPath;
            string existing;

            try
            {
                existing = ReadHosts(hostsPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Fail(result, "读取 HOSTS 失败：" + ex.Message, true);
            }
            catch (Exception ex)
            {
                LogService.Error("读取 HOSTS 失败", ex);
                return Fail(result, "读取 HOSTS 失败：" + ex.Message, false);
            }

            int activeCount;
            int disabledCount;
            string block = BuildManagedBlock(rules, groups, settings, out activeCount, out disabledCount);
            result.ActiveRuleCount = activeCount;
            result.DisabledRuleCount = disabledCount;

            HashSet<string> managedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BanRule rule in rules)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.Domain)) managedDomains.Add(rule.Domain);
            }
            result.Conflicts = FindUnmanagedConflicts(existing, managedDomains);

            BackupItem backup = null;
            if (File.Exists(hostsPath))
            {
                try
                {
                    backup = BackupManager.CreateBackup(hostsPath, settings.BackupDirectory, "应用规则前自动备份");
                    if (backup != null) result.BackupPath = backup.FilePath;
                }
                catch (UnauthorizedAccessException ex)
                {
                    LogService.Error("备份 HOSTS 失败（权限）", ex);
                    return Fail(result, "备份 HOSTS 失败，已中止修改：" + ex.Message, true);
                }
                catch (Exception ex)
                {
                    LogService.Error("备份 HOSTS 失败", ex);
                    return Fail(result, "备份 HOSTS 失败，已中止修改：" + ex.Message, false);
                }
            }

            try
            {
                string merged = MergeHosts(existing, block);
                WriteHosts(hostsPath, merged, settings.WriteUtf8Bom);
                result.Success = true;
                result.AppliedAt = DateTime.Now;
                LogService.Info("HOSTS 写入成功：" + hostsPath + "，启用 " + activeCount + " 条，禁用 " + disabledCount + " 条");
            }
            catch (UnauthorizedAccessException ex)
            {
                LogService.Error("写入 HOSTS 失败（权限）", ex);
                return Fail(result, "写入 HOSTS 失败，请以管理员身份重新运行本程序：" + ex.Message, true);
            }
            catch (Exception ex)
            {
                LogService.Error("写入 HOSTS 失败", ex);
                return Fail(result, "写入 HOSTS 失败：" + ex.Message, false);
            }

            try
            {
                BackupManager.Prune(settings.BackupDirectory, settings.BackupRetention);
            }
            catch (Exception ex)
            {
                LogService.Warn("清理旧备份失败：" + ex.Message);
            }

            DnsFlushResult dns = DnsService.Flush(settings.DnsFlushMode);
            result.DnsSuccess = dns.Success;
            result.DnsDetail = dns.Detail;
            if (dns.Success)
            {
                result.Message = "HOSTS 已更新，DNS 缓存已刷新";
            }
            else
            {
                result.Message = "HOSTS 已保存，但 DNS 刷新失败，请手动刷新";
            }

            if (result.Conflicts.Count > 0)
            {
                result.DnsDetail += Environment.NewLine +
                    "检测到 HOSTS 块外已有同域名记录：" + string.Join("、", result.Conflicts.ToArray());
            }
            return result;
        }

        public static string BuildManagedBlock(
            IList<BanRule> rules,
            IList<BanGroup> groups,
            AppSettings settings,
            out int activeCount,
            out int disabledCount)
        {
            activeCount = 0;
            disabledCount = 0;
            if (settings == null) settings = AppSettings.CreateDefault();
            settings.Normalize();
            if (rules == null) rules = new List<BanRule>();
            if (groups == null) groups = new List<BanGroup>();

            if (rules.Count == 0) return "";

            Dictionary<string, BanGroup> groupMap = new Dictionary<string, BanGroup>(StringComparer.OrdinalIgnoreCase);
            foreach (BanGroup group in groups)
            {
                if (group != null && !string.IsNullOrEmpty(group.Id)) groupMap[group.Id] = group;
            }

            List<BanRule> sorted = new List<BanRule>();
            foreach (BanRule rule in rules)
            {
                if (rule != null) sorted.Add(rule);
            }
            sorted.Sort(delegate(BanRule a, BanRule b)
            {
                BanGroup ga;
                BanGroup gb;
                groupMap.TryGetValue(a.GroupId ?? "", out ga);
                groupMap.TryGetValue(b.GroupId ?? "", out gb);
                int orderA = ga == null ? 999 : ga.SortOrder;
                int orderB = gb == null ? 999 : gb.SortOrder;
                if (orderA != orderB) return orderA.CompareTo(orderB);
                return string.Compare(a.Domain, b.Domain, StringComparison.OrdinalIgnoreCase);
            });

            List<string> lines = new List<string>();
            lines.Add(StartMarker);
            lines.Add("# 由 IPBan Manager 自动生成，请勿手动修改此区域");
            lines.Add("# 生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            lines.Add("");

            HashSet<string> writtenDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> writtenLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (BanRule rule in sorted)
            {
                string domain;
                string error;
                if (!DomainUtil.TryNormalize(rule.Domain, out domain, out error))
                {
                    lines.Add("# [无效域名] " + (rule.Domain ?? "") + "：" + error);
                    continue;
                }
                if (writtenDomains.Contains(domain)) continue;
                writtenDomains.Add(domain);

                BanGroup group;
                groupMap.TryGetValue(rule.GroupId ?? "", out group);
                string groupName = group == null ? "自定义" : group.Name;
                bool effective = rule.Enabled && (group == null || group.Enabled);
                string remark = SafeComment(rule.Remark);
                string title = string.IsNullOrEmpty(remark) ? domain : remark;

                List<string> mappingLines = BuildMappingLines(rule, domain, settings);
                if (effective)
                {
                    activeCount++;
                    lines.Add("# 封禁：" + title + " [" + groupName + "]");
                    for (int i = 0; i < mappingLines.Count; i++)
                    {
                        if (writtenLines.Add(mappingLines[i])) lines.Add(mappingLines[i]);
                    }
                    lines.Add("");
                }
                else
                {
                    disabledCount++;
                    lines.Add("# [已禁用] " + title + " [" + groupName + "]");
                    for (int i = 0; i < mappingLines.Count; i++)
                    {
                        lines.Add("# " + mappingLines[i]);
                    }
                    lines.Add("");
                }
            }

            lines.Add(EndMarker);
            return string.Join("\r\n", lines.ToArray());
        }

        /// <summary>把托管块合并进现有 HOSTS，保留块外内容，并移除上一次的托管块。</summary>
        public static string MergeHosts(string existing, string managedBlock)
        {
            string text = existing ?? "";
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = text.Split('\n');
            List<string> outside = new List<string>();
            bool inManaged = false;
            bool foundStart = false;
            bool foundEnd = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (!inManaged && string.Equals(trimmed, StartMarker, StringComparison.Ordinal))
                {
                    inManaged = true;
                    foundStart = true;
                    continue;
                }
                if (inManaged && string.Equals(trimmed, EndMarker, StringComparison.Ordinal))
                {
                    inManaged = false;
                    foundEnd = true;
                    continue;
                }
                if (inManaged) continue;
                outside.Add(lines[i]);
            }

            // 开始标记存在但结束标记缺失：保守地删除从开始标记到文件末尾的内容。
            // 上面的循环已经自然完成该行为。foundEnd 仅用于日志。
            if (foundStart && !foundEnd)
            {
                LogService.Warn("HOSTS 中的托管区域缺少结束标记，已按文件末尾处理");
            }

            while (outside.Count > 0 && outside[outside.Count - 1].Trim().Length == 0)
            {
                outside.RemoveAt(outside.Count - 1);
            }

            string outsideText = string.Join("\r\n", outside.ToArray());
            if (string.IsNullOrEmpty(managedBlock))
            {
                return outsideText.Length == 0 ? "" : outsideText + "\r\n";
            }

            if (outsideText.Length == 0) return managedBlock + "\r\n";
            return outsideText + "\r\n\r\n" + managedBlock + "\r\n";
        }

        public static string ReadHosts(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("HOSTS 路径为空");
            if (!File.Exists(path)) return "";
            byte[] bytes = File.ReadAllBytes(path);
            return JsonHelper.DecodeText(bytes);
        }

        public static void WriteHosts(string path, string content, bool useBom)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("HOSTS 路径为空");
            if (File.Exists(path))
            {
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    throw new IOException("HOSTS 文件是只读属性，请先取消只读后再试");
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string temp = path + ".ipban-" + Guid.NewGuid().ToString("N") + ".tmp";
            UTF8Encoding encoding = new UTF8Encoding(useBom);
            FileAttributes originalAttributes = FileAttributes.Normal;
            if (File.Exists(path)) originalAttributes = File.GetAttributes(path);
            try
            {
                File.WriteAllText(temp, content, encoding);

                if (File.Exists(path))
                {
                    Exception replaceException = null;
                    try
                    {
                        // 同目录 File.Replace 在 NTFS 上是原子替换，避免出现半截 HOSTS。
                        File.Replace(temp, path, null);
                    }
                    catch (Exception ex)
                    {
                        // 文件系统不支持、共享冲突或 ACL 限制时退回到覆盖复制；
                        // 如果复制也失败，则抛出 Replace 的原始异常（多半是权限问题）。
                        replaceException = ex;
                        try
                        {
                            File.Copy(temp, path, true);
                        }
                        catch
                        {
                            throw replaceException;
                        }
                    }

                    try
                    {
                        File.SetAttributes(path, originalAttributes & ~FileAttributes.ReadOnly);
                    }
                    catch
                    {
                        // 属性恢复失败不影响内容。
                    }
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch
                {
                    // 临时文件清理失败不改变写入结果。
                }
            }
        }

        public static List<string> FindUnmanagedConflicts(string existingHosts, HashSet<string> domains)
        {
            List<string> conflicts = new List<string>();
            if (string.IsNullOrEmpty(existingHosts) || domains == null || domains.Count == 0) return conflicts;

            string text = existingHosts.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = text.Split('\n');
            bool inManaged = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (string.Equals(trimmed, StartMarker, StringComparison.Ordinal))
                {
                    inManaged = true;
                    continue;
                }
                if (string.Equals(trimmed, EndMarker, StringComparison.Ordinal))
                {
                    inManaged = false;
                    continue;
                }
                if (inManaged || trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

                int hash = trimmed.IndexOf('#');
                if (hash >= 0) trimmed = trimmed.Substring(0, hash).Trim();
                string[] parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                for (int p = 1; p < parts.Length; p++)
                {
                    if (domains.Contains(parts[p]) && !DomainUtil.ContainsIgnoreCase(conflicts, parts[p]))
                        conflicts.Add(parts[p]);
                }
            }
            return conflicts;
        }

        public static bool IsEffectivelyEnabled(BanRule rule, IList<BanGroup> groups)
        {
            if (rule == null || !rule.Enabled) return false;
            if (groups != null)
            {
                foreach (BanGroup group in groups)
                {
                    if (group != null && string.Equals(group.Id, rule.GroupId, StringComparison.OrdinalIgnoreCase))
                        return group.Enabled;
                }
            }
            return true;
        }

        private static List<string> BuildMappingLines(BanRule rule, string domain, AppSettings settings)
        {
            List<string> result = new List<string>();
            if (string.Equals(rule.IpMode, "custom", StringComparison.OrdinalIgnoreCase))
            {
                string ip;
                string error;
                if (DomainUtil.TryNormalizeIp(rule.CustomIp, out ip, out error))
                {
                    result.Add(ip + " " + domain);
                }
                else
                {
                    result.Add("0.0.0.0 " + domain);
                }
            }
            else
            {
                string blockLine = RenderTemplate(settings.BlockTemplate, "0.0.0.0", domain);
                string redirectLine = RenderTemplate(settings.RedirectTemplate, "127.0.0.1", domain);
                if (!string.Equals(blockLine, redirectLine, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(blockLine);
                    result.Add(redirectLine);
                }
                else
                {
                    result.Add(blockLine);
                }
            }
            return result;
        }

        private static string RenderTemplate(string template, string ip, string domain)
        {
            string text = string.IsNullOrWhiteSpace(template) ? ip + " {domain}" : template;
            text = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            text = text.Replace("{domain}", domain).Replace("{ip}", ip);
            if (text.IndexOf(domain, StringComparison.OrdinalIgnoreCase) < 0) text += " " + domain;
            return text.Trim();
        }

        private static string SafeComment(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Replace('\r', ' ').Replace('\n', ' ').Replace("#", "＃").Trim();
        }

        private static HostsApplyResult Fail(HostsApplyResult result, string message, bool requiresElevation)
        {
            result.Success = false;
            result.RequiresElevation = requiresElevation;
            result.Error = message;
            result.Message = message;
            return result;
        }
    }
}
