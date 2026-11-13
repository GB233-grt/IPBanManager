using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager
{
    public class MutationResult
    {
        public MutationResult()
        {
            Upsert = new UpsertResult();
        }

        public UpsertResult Upsert { get; set; }
        public HostsApplyResult Hosts { get; set; }
        public string Error { get; set; }
        public bool Success { get { return string.IsNullOrEmpty(Error); } }
    }

    /// <summary>
    /// 应用状态中枢：规则、分组、操作记录、设置、主题，以及所有会落盘/写 HOSTS 的动作。
    /// UI 只调用这里的方法，保证“数据持久化 → 可选自动应用 → 通知界面”的顺序一致。
    /// </summary>
    public class AppState : INotifyPropertyChanged
    {
        private readonly string _loadWarning;

        public AppState(AppData data, AppSettings settings, ThemeSettings theme, string loadWarning)
        {
            Data = data ?? new AppData();
            Settings = settings ?? AppSettings.CreateDefault();
            Theme = theme ?? ThemeSettings.CreateDefault();
            _loadWarning = loadWarning ?? "";

            SortOperations();
            RecentOperations = new ObservableCollection<OperationRecord>();
            RebuildRecentOperations();
        }

        public AppData Data { get; private set; }
        public AppSettings Settings { get; set; }
        public ThemeSettings Theme { get; set; }

        public ObservableCollection<BanRule> Rules { get { return Data.Rules; } }
        public ObservableCollection<BanGroup> Groups { get { return Data.Groups; } }
        public ObservableCollection<OperationRecord> Operations { get { return Data.Operations; } }
        public ObservableCollection<OperationRecord> RecentOperations { get; private set; }

        public string LoadWarning { get { return _loadWarning; } }

        public event EventHandler DataChanged;
        public event EventHandler<string> PersistenceFailed;
        public event PropertyChangedEventHandler PropertyChanged;

        public int RuleCount { get { return Data.Rules.Count; } }

        public int EnabledRuleCount
        {
            get
            {
                int count = 0;
                foreach (BanRule rule in Data.Rules)
                {
                    if (HostsManager.IsEffectivelyEnabled(rule, Data.Groups)) count++;
                }
                return count;
            }
        }

        public int DisabledRuleCount { get { return RuleCount - EnabledRuleCount; } }

        public int GroupCount { get { return Data.Groups.Count; } }

        public int EnabledGroupCount
        {
            get
            {
                int count = 0;
                foreach (BanGroup group in Data.Groups)
                {
                    if (group.Enabled) count++;
                }
                return count;
            }
        }

        public int PausedGroupCount { get { return GroupCount - EnabledGroupCount; } }
        public bool HasRules { get { return RuleCount > 0; } }
        public bool HasOperations { get { return Data.Operations.Count > 0; } }
        public bool IsAdmin { get { return App.IsAdmin; } }
        public bool IsPreview { get { return App.IsPreview; } }

        public string AdminStatusText
        {
            get
            {
                if (App.IsPreview) return "预览模式";
                return App.IsAdmin ? "管理员权限" : "普通权限";
            }
        }

        public string AdminStatusDetail
        {
            get
            {
                if (App.IsPreview) return "当前为界面预览模式，HOSTS 指向临时文件";
                return App.IsAdmin ? "已获得写入 HOSTS 所需权限" : "需要管理员权限才能写入系统 HOSTS";
            }
        }

        public string HostsPathDisplay
        {
            get { return Settings == null ? "" : Settings.HostsPath; }
        }

        public string BackupDirectoryDisplay
        {
            get { return Settings == null ? "" : Settings.BackupDirectory; }
        }

        public string LastApplyText
        {
            get
            {
                OperationRecord record = FindLastOperation("apply");
                if (record == null) return "尚未应用 HOSTS";
                return record.Success ? FormatRelativeTime(record.Time) : "上次应用失败";
            }
        }

        public string LastApplyDetail
        {
            get
            {
                OperationRecord record = FindLastOperation("apply");
                if (record == null) return "添加规则后会自动写入 HOSTS";
                return record.Time.ToString("MM-dd HH:mm:ss") + " · " + record.Detail;
            }
        }

        public string LastDnsText
        {
            get
            {
                OperationRecord record = FindLastOperation("dns");
                if (record == null)
                {
                    OperationRecord apply = FindLastOperation("apply");
                    if (apply != null && apply.Success && apply.Detail.Contains("DNS"))
                        return "随 HOSTS 应用刷新";
                    return "尚未刷新";
                }
                return record.Success ? "正常" : "失败";
            }
        }

        public string LastBackupText
        {
            get
            {
                OperationRecord record = FindLastOperation("backup");
                if (record == null)
                {
                    OperationRecord apply = FindLastOperation("apply");
                    if (apply == null) return "暂无备份";
                    return "随 HOSTS 保存自动备份";
                }
                return FormatRelativeTime(record.Time);
            }
        }

        public string BehaviorSummary
        {
            get
            {
                List<string> parts = new List<string>();
                if (Settings.AutoApplyHosts) parts.Add("自动应用");
                else parts.Add("手动应用");
                if (Settings.TrayEnabled) parts.Add("托盘");
                if (Settings.StartWithWindows) parts.Add("自启");
                return string.Join(" · ", parts.ToArray());
            }
        }

        public void RaiseAll()
        {
            OnPropertyChanged("RuleCount");
            OnPropertyChanged("EnabledRuleCount");
            OnPropertyChanged("DisabledRuleCount");
            OnPropertyChanged("GroupCount");
            OnPropertyChanged("EnabledGroupCount");
            OnPropertyChanged("PausedGroupCount");
            OnPropertyChanged("HasRules");
            OnPropertyChanged("HasOperations");
            OnPropertyChanged("AdminStatusText");
            OnPropertyChanged("AdminStatusDetail");
            OnPropertyChanged("HostsPathDisplay");
            OnPropertyChanged("BackupDirectoryDisplay");
            OnPropertyChanged("LastApplyText");
            OnPropertyChanged("LastApplyDetail");
            OnPropertyChanged("LastDnsText");
            OnPropertyChanged("LastBackupText");
            OnPropertyChanged("BehaviorSummary");
            OnPropertyChanged("IsAdmin");
            OnPropertyChanged("IsPreview");
        }

        public void NotifyDataChanged()
        {
            RaiseAll();
            EventHandler handler = DataChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public BanGroup GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return GetGroup("custom");
            foreach (BanGroup group in Data.Groups)
            {
                if (string.Equals(group.Id, groupId, StringComparison.OrdinalIgnoreCase)) return group;
            }
            return null;
        }

        public string GetGroupName(string groupId)
        {
            BanGroup group = GetGroup(groupId);
            return group == null ? "自定义" : group.Name;
        }

        public string GetGroupNameWithState(string groupId)
        {
            BanGroup group = GetGroup(groupId);
            if (group == null) return "自定义";
            return group.Enabled ? group.Name : group.Name + "（已暂停）";
        }

        public bool GetEffectiveEnabled(BanRule rule)
        {
            return HostsManager.IsEffectivelyEnabled(rule, Data.Groups);
        }

        public MutationResult AddRules(
            IEnumerable<string> domains,
            string ipMode,
            string customIp,
            string groupId,
            string remark,
            bool enabled)
        {
            MutationResult mutation = new MutationResult();
            UpsertResult result = RuleService.Upsert(Data.Rules, domains, ipMode, customIp, groupId, remark, enabled);
            mutation.Upsert = result;

            if (result.Added + result.Updated > 0)
            {
                string preview = string.Join("、", result.Domains.Take(4).ToArray());
                RecordOperation("添加封禁规则", result.Summary + " · " + preview, true, "add");
                PersistData();
                if (Settings.AutoApplyHosts) mutation.Hosts = ApplyHosts();
                NotifyDataChanged();
            }
            else
            {
                mutation.Error = result.Errors.Count > 0
                    ? string.Join("；", result.Errors.Take(3).ToArray())
                    : "没有可添加的域名";
            }
            return mutation;
        }

        public HostsApplyResult EditRule(BanRule rule, RuleUpdateResult update)
        {
            string error = RuleService.UpdateSingle(
                Data.Rules, rule, update.Domain, update.IpMode, update.CustomIp,
                update.GroupId, update.Remark, update.Enabled);
            if (error != null)
            {
                LastOperationError = error;
                return null;
            }

            RecordOperation("编辑规则", rule.Domain + " · " + GetGroupName(rule.GroupId), true, "edit");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public string LastOperationError { get; private set; }

        public void ClearLastOperationError()
        {
            LastOperationError = "";
        }

        public HostsApplyResult DeleteRules(IEnumerable<BanRule> rules)
        {
            List<BanRule> list = new List<BanRule>(rules);
            if (list.Count == 0) return null;
            foreach (BanRule rule in list) Data.Rules.Remove(rule);
            RecordOperation("删除规则", "删除 " + list.Count + " 条：" +
                string.Join("、", list.Take(3).Select(delegate(BanRule r) { return r.Domain; }).ToArray()), true, "delete");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public HostsApplyResult SetRuleEnabled(BanRule rule, bool enabled)
        {
            if (rule == null) return null;
            rule.Enabled = enabled;
            rule.UpdatedAt = DateTime.Now;
            RecordOperation(enabled ? "启用规则" : "禁用规则", rule.Domain, true, "toggle");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public HostsApplyResult SetAllRulesEnabled(IEnumerable<BanRule> rules, bool enabled)
        {
            int count = 0;
            foreach (BanRule rule in rules)
            {
                if (rule.Enabled != enabled)
                {
                    rule.Enabled = enabled;
                    rule.UpdatedAt = DateTime.Now;
                    count++;
                }
            }
            if (count == 0) return null;
            RecordOperation(enabled ? "批量启用规则" : "批量禁用规则", "更新 " + count + " 条规则", true, "toggle");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public HostsApplyResult ApplyHosts()
        {
            HostsApplyResult result;
            try
            {
                result = HostsManager.Apply(Settings, Data.Rules.ToList(), Data.Groups.ToList());
            }
            catch (Exception ex)
            {
                LogService.Error("应用 HOSTS 时发生未处理异常", ex);
                result = new HostsApplyResult
                {
                    Success = false,
                    RequiresElevation = ex is UnauthorizedAccessException,
                    Message = "应用 HOSTS 失败：" + ex.Message,
                    Error = ex.Message
                };
            }

            string detail = result.Success ? result.Message : (result.Error ?? result.Message);
            if (!string.IsNullOrEmpty(result.BackupPath))
                detail += " · 备份 " + System.IO.Path.GetFileName(result.BackupPath);
            if (result.Conflicts != null && result.Conflicts.Count > 0)
                detail += " · 块外同域名：" + string.Join("、", result.Conflicts.ToArray());

            RecordOperation("应用 HOSTS", detail, result.Success, "apply");
            PersistData();
            SaveSettingsFile();
            RaiseAll();
            return result;
        }

        public DnsFlushResult RefreshDnsOnly()
        {
            DnsFlushResult result = DnsService.Flush(Settings.DnsFlushMode);
            RecordOperation("刷新 DNS", result.Message, result.Success, "dns");
            PersistData();
            NotifyDataChanged();
            return result;
        }

        public BackupItem BackupNow()
        {
            try
            {
                BackupItem item = BackupManager.CreateBackup(Settings.HostsPath, Settings.BackupDirectory, "手动备份");
                if (item == null)
                {
                    LastOperationError = "HOSTS 文件不存在，无法备份";
                    return null;
                }
                BackupManager.Prune(Settings.BackupDirectory, Settings.BackupRetention);
                RecordOperation("手动备份 HOSTS", item.FileName, true, "backup");
                PersistData();
                NotifyDataChanged();
                return item;
            }
            catch (Exception ex)
            {
                LastOperationError = "备份失败：" + ex.Message;
                LogService.Error("手动备份失败", ex);
                return null;
            }
        }

        public HostsApplyResult RestoreBackup(string backupPath)
        {
            HostsApplyResult result = new HostsApplyResult();
            try
            {
                BackupManager.RestoreBackup(backupPath, Settings.HostsPath, Settings.BackupDirectory,
                    "恢复 " + System.IO.Path.GetFileName(backupPath));
                result.Success = true;
                result.AppliedAt = DateTime.Now;
                DnsFlushResult dns = DnsService.Flush(Settings.DnsFlushMode);
                result.DnsSuccess = dns.Success;
                result.Message = dns.Success
                    ? "备份已恢复，DNS 缓存已刷新"
                    : "备份已恢复，但 DNS 刷新失败，请手动刷新";
                result.BackupPath = backupPath;
                RecordOperation("恢复 HOSTS 备份", System.IO.Path.GetFileName(backupPath) + " · " + result.Message, true, "restore");
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Success = false;
                result.RequiresElevation = true;
                result.Message = "恢复备份失败，请以管理员身份运行：" + ex.Message;
                result.Error = result.Message;
                RecordOperation("恢复 HOSTS 备份", result.Message, false, "restore");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "恢复备份失败：" + ex.Message;
                result.Error = result.Message;
                LogService.Error("恢复备份失败", ex);
                RecordOperation("恢复 HOSTS 备份", result.Message, false, "restore");
            }
            PersistData();
            RaiseAll();
            return result;
        }

        public List<BackupItem> LoadBackups()
        {
            return BackupManager.ListBackups(Settings.BackupDirectory);
        }

        public BanGroup AddGroup(string name, string color)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            foreach (BanGroup existing in Data.Groups)
            {
                if (string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)) return null;
            }

            int maxOrder = 0;
            foreach (BanGroup group in Data.Groups) maxOrder = Math.Max(maxOrder, group.SortOrder);
            BanGroup created = new BanGroup
            {
                Name = name,
                Color = string.IsNullOrWhiteSpace(color) ? "#4C8DFF" : color,
                IsSystem = false,
                SortOrder = maxOrder + 1,
                Enabled = true
            };
            Data.Groups.Add(created);
            RecordOperation("新建分组", name, true, "group");
            PersistData();
            if (Settings.AutoApplyHosts) ApplyHosts();
            NotifyDataChanged();
            return created;
        }

        public string RenameGroup(BanGroup group, string newName, string color)
        {
            if (group == null) return "分组不存在";
            if (string.IsNullOrWhiteSpace(newName)) return "分组名称不能为空";
            newName = newName.Trim();
            foreach (BanGroup other in Data.Groups)
            {
                if (!ReferenceEquals(other, group) &&
                    string.Equals(other.Name, newName, StringComparison.OrdinalIgnoreCase))
                    return "已存在同名分组";
            }
            group.Name = newName;
            if (!string.IsNullOrWhiteSpace(color)) group.Color = color;
            RecordOperation("重命名分组", newName, true, "group");
            PersistData();
            if (Settings.AutoApplyHosts) ApplyHosts();
            NotifyDataChanged();
            return null;
        }

        public HostsApplyResult DeleteGroup(BanGroup group)
        {
            if (group == null) return null;
            if (group.IsSystem)
            {
                LastOperationError = "系统分组不能删除";
                return null;
            }
            BanGroup custom = GetGroup("custom");
            foreach (BanRule rule in Data.Rules)
            {
                if (string.Equals(rule.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                    rule.GroupId = custom == null ? "custom" : custom.Id;
            }
            Data.Groups.Remove(group);
            RecordOperation("删除分组", group.Name + "，组内规则已移至自定义", true, "group");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public HostsApplyResult SetGroupEnabled(BanGroup group, bool enabled)
        {
            if (group == null) return null;
            group.Enabled = enabled;
            RecordOperation(enabled ? "恢复分组" : "暂停分组", group.Name, true, "group");
            PersistData();
            HostsApplyResult hosts = Settings.AutoApplyHosts ? ApplyHosts() : null;
            NotifyDataChanged();
            return hosts;
        }

        public AutoStartResult SaveSettings()
        {
            Settings.Normalize();
            AutoStartResult autoStart = new AutoStartResult { Success = true, Message = "" };
            string backupNote = "";
            try
            {
                StoragePaths.EnsureDirectory(Settings.BackupDirectory);
            }
            catch (Exception ex)
            {
                LogService.Warn("备份目录不可用，回退默认目录：" + ex.Message);
                Settings.BackupDirectory = StoragePaths.BackupsDirectory;
                backupNote = "；备份目录不可用，已回退默认目录";
                try
                {
                    StoragePaths.EnsureDirectory(Settings.BackupDirectory);
                }
                catch (Exception fallbackEx)
                {
                    LogService.Error("默认备份目录也不可用", fallbackEx);
                }
            }

            bool currentlyEnabled = AutoStartService.IsEnabled();
            if (Settings.StartWithWindows || currentlyEnabled)
            {
                autoStart = AutoStartService.SetEnabled(Settings.StartWithWindows, Settings.StartMinimized);
                if (!autoStart.Success && Settings.StartWithWindows)
                    Settings.StartWithWindows = false;
            }

            SaveSettingsFile();
            RecordOperation("保存设置", "HOSTS：" + Settings.HostsPath + backupNote, true, "settings");
            PersistData();
            NotifyDataChanged();
            return autoStart;
        }

        public void SaveTheme(ThemeSettings theme, bool record)
        {
            Theme = theme;
            Theme.Normalize();
            ThemeManager.Apply(Theme);
            try
            {
                DataService.SaveTheme(Theme);
            }
            catch (Exception ex)
            {
                LastOperationError = "保存主题失败：" + ex.Message;
                LogService.Error("保存主题失败", ex);
                EventHandler<string> failed = PersistenceFailed;
                if (failed != null) failed(this, LastOperationError);
            }
            if (record)
            {
                RecordOperation("保存主题", "强调色 " + Theme.AccentColor + " · 圆角 " + Theme.CornerRadius, true, "theme");
                PersistData();
            }
            RaiseAll();
            EventHandler handler = DataChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void ClearOperations()
        {
            Data.Operations.Clear();
            RecentOperations.Clear();
            PersistData();
            NotifyDataChanged();
        }

        public void PersistData()
        {
            try
            {
                DataService.SaveData(Data);
            }
            catch (Exception ex)
            {
                LastOperationError = "保存规则数据失败：" + ex.Message;
                LogService.Error("保存规则数据失败", ex);
                EventHandler<string> failed = PersistenceFailed;
                if (failed != null) failed(this, LastOperationError);
            }
        }

        public void SaveSettingsFile()
        {
            try
            {
                DataService.SaveSettings(Settings);
            }
            catch (Exception ex)
            {
                LastOperationError = "保存设置失败：" + ex.Message;
                LogService.Error("保存设置失败", ex);
                EventHandler<string> failed = PersistenceFailed;
                if (failed != null) failed(this, LastOperationError);
            }
        }

        public void RecordOperation(string title, string detail, bool success, string kind)
        {
            OperationRecord record = new OperationRecord
            {
                Time = DateTime.Now,
                Title = title ?? "",
                Detail = detail ?? "",
                Success = success,
                Kind = kind ?? "info"
            };
            Data.Operations.Insert(0, record);
            while (Data.Operations.Count > Math.Max(50, Settings.MaxLogRecords))
                Data.Operations.RemoveAt(Data.Operations.Count - 1);

            RecentOperations.Insert(0, record);
            while (RecentOperations.Count > 8) RecentOperations.RemoveAt(RecentOperations.Count - 1);

            if (success) LogService.Info(title + "：" + detail);
            else LogService.Warn(title + "：" + detail);
        }

        private void RebuildRecentOperations()
        {
            RecentOperations.Clear();
            int count = Math.Min(8, Data.Operations.Count);
            for (int i = 0; i < count; i++) RecentOperations.Add(Data.Operations[i]);
        }

        private void SortOperations()
        {
            if (Data.Operations == null || Data.Operations.Count < 2) return;
            List<OperationRecord> sorted = Data.Operations.OrderByDescending(delegate(OperationRecord r) { return r.Time; }).ToList();
            Data.Operations.Clear();
            foreach (OperationRecord record in sorted) Data.Operations.Add(record);
        }

        private OperationRecord FindLastOperation(string kind)
        {
            foreach (OperationRecord record in Data.Operations)
            {
                if (string.Equals(record.Kind, kind, StringComparison.OrdinalIgnoreCase)) return record;
            }
            return null;
        }

        private static string FormatRelativeTime(DateTime time)
        {
            TimeSpan span = DateTime.Now - time;
            if (span.TotalSeconds < 60) return "刚刚";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " 分钟前";
            if (span.TotalHours < 24) return (int)span.TotalHours + " 小时前";
            if (span.TotalDays < 7) return (int)span.TotalDays + " 天前";
            return time.ToString("MM-dd HH:mm");
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
