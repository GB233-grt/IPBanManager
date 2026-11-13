using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager.CoreTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;
        private static string _root;

        private static int Main(string[] args)
        {
            _root = Path.Combine(Path.GetTempPath(), "IPBanManagerTests_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(_root);
            StoragePaths.Initialize(_root);

            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("净域核心测试，临时目录：" + _root);

            Run("域名/URL 归一化", TestDomainNormalize);
            Run("域名拒绝 IP 与通配符", TestDomainReject);
            Run("常见子域扩展", TestExpand);
            Run("批量文本解析", TestBatchParse);
            Run("规则去重更新", TestUpsert);
            Run("托管块生成", TestManagedBlock);
            Run("托管块合并保持用户内容", TestMerge);
            Run("HOSTS UTF-8 写入读取", TestHostsWriteRead);
            Run("备份创建/恢复/保留", TestBackup);
            Run("完整 Apply 与冲突检测", TestApply);
            Run("JSON 数据持久化", TestJsonRoundtrip);
            Run("设置/主题序列化", TestSettingsTheme);

            Console.WriteLine();
            Console.WriteLine("结果：通过 " + _passed + "，失败 " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name + "：" + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void TestDomainNormalize()
        {
            string d;
            string e;
            Assert(DomainUtil.TryNormalize("https://www.bilibili.com/video/BV1?x=1", out d, out e), e);
            Assert(d == "www.bilibili.com", "URL 归一化失败：" + d);

            Assert(DomainUtil.TryNormalize("BILIBILI.COM:443/path", out d, out e), e);
            Assert(d == "bilibili.com", "端口/大小写处理失败：" + d);

            Assert(DomainUtil.TryNormalize("example.com.", out d, out e), e);
            Assert(d == "example.com", "结尾点处理失败：" + d);

            Assert(DomainUtil.TryNormalize("例子.测试", out d, out e), e);
            Assert(d.StartsWith("xn--"), "IDN 未转 Punycode：" + d);
        }

        private static void TestDomainReject()
        {
            string d;
            string e;
            Assert(!DomainUtil.TryNormalize("0.0.0.0", out d, out e), "IP 应被拒绝");
            Assert(!DomainUtil.TryNormalize("*.bilibili.com", out d, out e), "通配符应被拒绝");
            Assert(!DomainUtil.TryNormalize("not_a_domain", out d, out e), "无点域名应被拒绝");
            Assert(!DomainUtil.TryNormalize("bad_domain.com", out d, out e), "下划线应被拒绝");
        }

        private static void TestExpand()
        {
            List<string> expanded = DomainUtil.ExpandDomains(
                new[] { "bilibili.com" }, "www,m,api", true);
            Assert(expanded.Count == 4, "扩展数量应为 4，实际 " + expanded.Count);
            Assert(expanded.Contains("bilibili.com"), "缺少根域名");
            Assert(expanded.Contains("www.bilibili.com"), "缺少 www");
            Assert(expanded.Contains("m.bilibili.com"), "缺少 m");
            Assert(expanded.Contains("api.bilibili.com"), "缺少 api");

            List<string> noExpand = DomainUtil.ExpandDomains(
                new[] { "bilibili.com" }, "www,m,api", false);
            Assert(noExpand.Count == 1, "未启用扩展应只有 1 条");

            List<string> fromSub = DomainUtil.ExpandDomains(
                new[] { "www.bilibili.com" }, "www,m,api", true);
            Assert(fromSub.Contains("bilibili.com"), "从 www 输入应回到根域名");
        }

        private static void TestBatchParse()
        {
            List<string> errors;
            string text = "# 注释行\r\n0.0.0.0 ad.example.com\r\nhttps://www.bilibili.com/video\r\n\r\nbad_domain";
            List<string> domains = DomainUtil.ParseBatchText(text, out errors);
            Assert(domains.Count == 2, "解析数量应为 2，实际 " + domains.Count);
            Assert(domains.Contains("ad.example.com"), "缺少 ad.example.com");
            Assert(domains.Contains("www.bilibili.com"), "缺少 bilibili URL");
            Assert(errors.Count == 1, "应记录 1 个错误");
        }

        private static void TestUpsert()
        {
            ObservableCollection<BanRule> rules = new ObservableCollection<BanRule>();
            UpsertResult first = RuleService.Upsert(rules, new[] { "bilibili.com", "www.bilibili.com" },
                "auto", "", "video", "哔哩哔哩", true);
            Assert(first.Added == 2 && rules.Count == 2, "首次新增失败");

            UpsertResult second = RuleService.Upsert(rules, new[] { "bilibili.com" },
                "custom", "127.0.0.1", "study", "更新备注", true);
            Assert(second.Updated == 1 && rules.Count == 2, "重复域名应更新而不是追加");
            Assert(rules[0].IpMode == "custom" && rules[0].CustomIp == "127.0.0.1", "更新字段未生效");
            Assert(rules[0].GroupId == "study", "分组更新未生效");

            UpsertResult bad = RuleService.Upsert(rules, new[] { "x.com" },
                "custom", "999.1.1.1", "custom", "", true);
            Assert(bad.Errors.Count == 1, "非法自定义 IP 应报错");
        }

        private static void TestManagedBlock()
        {
            List<BanGroup> groups = BanGroup.CreateBuiltIns();
            groups[2].Enabled = false; // 视频组暂停
            List<BanRule> rules = new List<BanRule>
            {
                new BanRule { Domain = "bilibili.com", GroupId = "video", Remark = "哔哩哔哩", Enabled = true, IpMode = "auto" },
                new BanRule { Domain = "example.com", GroupId = "study", Remark = "示例", Enabled = true, IpMode = "custom", CustomIp = "127.0.0.1" },
                new BanRule { Domain = "disabled.com", GroupId = "custom", Enabled = false, IpMode = "auto" }
            };
            AppSettings settings = AppSettings.CreateDefault();
            int active;
            int disabled;
            string block = HostsManager.BuildManagedBlock(rules, groups, settings, out active, out disabled);

            Assert(block.Contains(HostsManager.StartMarker), "缺少开始标记");
            Assert(block.Contains(HostsManager.EndMarker), "缺少结束标记");
            Assert(block.Contains("0.0.0.0 bilibili.com"), "暂停组规则不应出现未注释行");
            Assert(block.Contains("# 0.0.0.0 bilibili.com"), "暂停组规则应被注释");
            Assert(block.Contains("127.0.0.1 example.com"), "自定义 IP 未写入");
            Assert(block.Contains("# [已禁用]"), "禁用标记缺失");
            Assert(block.Contains("# 0.0.0.0 disabled.com"), "禁用规则映射应被注释");
            Assert(active == 1, "有效规则数应为 1，实际 " + active);
            Assert(disabled == 2, "禁用规则数应为 2，实际 " + disabled);

            // 自定义模板
            settings.BlockTemplate = "0.0.0.0 {domain} # block";
            settings.RedirectTemplate = "127.0.0.1 {domain} # redirect";
            string block2 = HostsManager.BuildManagedBlock(
                new List<BanRule> { new BanRule { Domain = "t.com", GroupId = "custom" } },
                groups, settings, out active, out disabled);
            Assert(block2.Contains("0.0.0.0 t.com # block"), "模板未生效");

            // 模板换行必须被清洗，不能向 HOSTS 注入额外行
            settings.BlockTemplate = "0.0.0.0 {domain}\r\n1.2.3.4 injected.com";
            string block3 = HostsManager.BuildManagedBlock(
                new List<BanRule> { new BanRule { Domain = "t.com", GroupId = "custom" } },
                groups, settings, out active, out disabled);
            Assert(block3.IndexOf("\r\n1.2.3.4") < 0, "模板换行注入未被清洗");
        }

        private static void TestMerge()
        {
            string user = "# my hosts\r\n127.0.0.1 local.id\r\n160.16.86.14 huggingface.co\r\n";
            int active;
            int disabled;
            string block1 = HostsManager.BuildManagedBlock(
                new List<BanRule> { new BanRule { Domain = "a.com", GroupId = "custom" } },
                BanGroup.CreateBuiltIns(), AppSettings.CreateDefault(), out active, out disabled);
            string merged1 = HostsManager.MergeHosts(user, block1);
            Assert(merged1.Contains("local.id"), "用户行丢失");
            Assert(CountOccurrences(merged1, HostsManager.StartMarker) == 1, "托管标记应只有 1 个");

            string block2 = HostsManager.BuildManagedBlock(
                new List<BanRule> { new BanRule { Domain = "b.com", GroupId = "custom" } },
                BanGroup.CreateBuiltIns(), AppSettings.CreateDefault(), out active, out disabled);
            string merged2 = HostsManager.MergeHosts(merged1, block2);
            Assert(merged2.Contains("local.id"), "第二次合并丢失用户行");
            Assert(merged2.Contains("b.com") && !merged2.Contains("a.com"), "旧托管内容未替换");
            Assert(CountOccurrences(merged2, HostsManager.StartMarker) == 1, "第二次合并出现重复标记");

            string removed = HostsManager.MergeHosts(merged2, "");
            Assert(!removed.Contains(HostsManager.StartMarker), "清空规则后托管块应移除");
            Assert(removed.Contains("huggingface.co"), "清空规则后用户行丢失");
        }

        private static void TestHostsWriteRead()
        {
            string path = Path.Combine(_root, "hosts-test");
            string content = "# 中文注释\r\n0.0.0.0 bilibili.com\r\n127.0.0.1 bilibili.com\r\n";
            HostsManager.WriteHosts(path, content, false);
            string read = HostsManager.ReadHosts(path);
            Assert(read.Contains("中文注释"), "UTF-8 中文读回失败");
            Assert(read.Contains("0.0.0.0 bilibili.com"), "内容丢失");

            HostsManager.WriteHosts(path, content, true);
            byte[] bytes = File.ReadAllBytes(path);
            Assert(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "BOM 未写入");
        }

        private static void TestBackup()
        {
            string hosts = Path.Combine(_root, "hosts-backup");
            File.WriteAllText(hosts, "# v1\r\n", Encoding.UTF8);
            string backupDir = Path.Combine(_root, "backups");
            Directory.CreateDirectory(backupDir);

            BackupItem item = BackupManager.CreateBackup(hosts, backupDir, "测试备份");
            Assert(item != null, "备份未创建");
            Assert(File.Exists(item.FilePath), "备份文件不存在");
            Assert(item.FileName.StartsWith("hosts_") && item.FileName.EndsWith(".bak"), "备份命名不正确");

            File.WriteAllText(hosts, "# v2 changed\r\n", Encoding.UTF8);
            BackupManager.RestoreBackup(item.FilePath, hosts, backupDir, "测试恢复");
            string restored = File.ReadAllText(hosts);
            Assert(restored.Contains("# v1"), "恢复内容不正确：" + restored);

            // 保留数量
            for (int i = 0; i < 4; i++)
            {
                System.Threading.Thread.Sleep(5);
                BackupManager.CreateBackup(hosts, backupDir, "批量" + i);
            }
            int before = BackupManager.ListBackups(backupDir).Count;
            int removed = BackupManager.Prune(backupDir, 2);
            int after = BackupManager.ListBackups(backupDir).Count;
            Assert(before >= 5 && after == 2, "保留策略失败 before=" + before + " after=" + after + " removed=" + removed);
        }

        private static void TestApply()
        {
            string hosts = Path.Combine(_root, "hosts-apply");
            string backupDir = Path.Combine(_root, "backups-apply");
            Directory.CreateDirectory(backupDir);
            File.WriteAllText(hosts, "# user line\r\n1.2.3.4 bilibili.com\r\n", new UTF8Encoding(false));

            AppSettings settings = AppSettings.CreateDefault();
            settings.HostsPath = hosts;
            settings.BackupDirectory = backupDir;
            settings.DnsFlushMode = "ipconfig";
            List<BanGroup> groups = BanGroup.CreateBuiltIns();
            List<BanRule> rules = new List<BanRule>
            {
                new BanRule { Domain = "bilibili.com", GroupId = "video", Remark = "哔哩哔哩" },
                new BanRule { Domain = "www.bilibili.com", GroupId = "video" }
            };

            HostsApplyResult result = HostsManager.Apply(settings, rules, groups);
            Assert(result.Success, "Apply 失败：" + result.Message);
            Assert(result.BackupPath != null && File.Exists(result.BackupPath), "Apply 未创建备份");
            string text = HostsManager.ReadHosts(hosts);
            Assert(text.Contains("0.0.0.0 bilibili.com"), "未写入 0.0.0.0");
            Assert(text.Contains("127.0.0.1 bilibili.com"), "未写入 127.0.0.1");
            Assert(text.Contains("1.2.3.4 bilibili.com"), "用户块外记录被破坏");
            Assert(result.Conflicts.Contains("bilibili.com"), "未检测到块外冲突");

            int backupsBefore = BackupManager.ListBackups(backupDir).Count;
            rules[0].Enabled = false;
            HostsApplyResult result2 = HostsManager.Apply(settings, rules, groups);
            Assert(result2.Success, "第二次 Apply 失败");
            int backupsAfter = BackupManager.ListBackups(backupDir).Count;
            Assert(backupsAfter > backupsBefore, "每次保存应新增备份");

            // DNS 刷新接口本身不应抛出
            DnsFlushResult dns = DnsService.Flush("api");
            Assert(dns != null, "DNS 返回空");
        }

        private static void TestJsonRoundtrip()
        {
            AppData data = new AppData();
            data.Groups.Add(new BanGroup { Id = "custom", Name = "自定义", Color = "#4C8DFF" });
            data.Rules.Add(new BanRule { Domain = "bilibili.com", GroupId = "custom", Remark = "中文备注" });
            data.Operations.Add(new OperationRecord { Title = "测试", Detail = "详情", Success = true, Kind = "test" });

            string path = Path.Combine(_root, "data-roundtrip.json");
            JsonHelper.Save(path, data);
            AppData loaded = JsonHelper.Load<AppData>(path);
            Assert(loaded != null, "反序列化为空");
            Assert(loaded.Rules.Count == 1, "规则数错误");
            Assert(loaded.Rules[0].Domain == "bilibili.com", "域名丢失");
            Assert(loaded.Rules[0].Remark == "中文备注", "中文备注丢失");
            Assert(loaded.Groups.Count == 1, "分组数错误");
            Assert(loaded.Operations[0].Title == "测试", "操作记录丢失");

            // 外部损坏数据中的 null 规则必须被清理，且 Apply 不抛 NRE。
            string nullRoot = Path.Combine(_root, "null-data");
            StoragePaths.Initialize(nullRoot);
            File.WriteAllText(StoragePaths.DataFile,
                "{\"Version\":1,\"Rules\":[null],\"Groups\":[null],\"Operations\":[null]}",
                new UTF8Encoding(false));
            string warning;
            AppData cleaned = DataService.LoadData(out warning);
            Assert(cleaned.Rules.Count == 0 && cleaned.Groups.Count >= 5, "null 集合清理失败");
            AppSettings nullSettings = AppSettings.CreateDefault();
            nullSettings.HostsPath = Path.Combine(nullRoot, "hosts");
            nullSettings.BackupDirectory = Path.Combine(nullRoot, "backups");
            nullSettings.DnsFlushMode = "ipconfig";
            HostsApplyResult nullResult = HostsManager.Apply(nullSettings, cleaned.Rules, cleaned.Groups);
            Assert(nullResult.Success, "清理 null 规则后 Apply 失败：" + nullResult.Message);
            StoragePaths.Initialize(_root);
        }

        private static void TestSettingsTheme()
        {
            AppSettings settings = AppSettings.CreateDefault();
            settings.HostsPath = Path.Combine(_root, "hosts-settings");
            settings.BackupDirectory = Path.Combine(_root, "backups-settings");
            settings.DnsFlushMode = "api";
            settings.BlockTemplate = "0.0.0.0 {domain}";
            DataService.SaveSettings(settings);
            AppSettings loaded = DataService.LoadSettings();
            Assert(loaded.DnsFlushMode == "api", "DNS 模式未持久化");

            ThemeSettings theme = ThemeSettings.CreateDefault();
            theme.AccentColor = "#FF7D9C";
            theme.CornerRadius = 18;
            DataService.SaveTheme(theme);
            ThemeSettings loadedTheme = DataService.LoadTheme();
            Assert(loadedTheme.AccentColor == "#FF7D9C" && Math.Abs(loadedTheme.CornerRadius - 18) < 0.01, "主题未持久化");

            // 非法颜色应回退默认色并写回合法值
            theme.AccentColor = "not-a-color";
            theme.Normalize();
            Assert(theme.AccentColor == "#4C8DFF", "非法主题色未回退默认值");
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }
    }
}
