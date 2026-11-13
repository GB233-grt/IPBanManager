using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager
{
    public partial class App : Application
    {
        private static Mutex _singleInstanceMutex;

        public static AppState State { get; set; }
        public static bool IsAdmin { get; set; }
        public static bool IsPreview { get; set; }
        public static MainWindow MainWindowInstance { get; set; }
        public static bool IsExiting { get; set; }
        public static string InitialPage { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InitialPage = "dashboard";

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            string dataRoot;
            bool startMinimized;
            ParseArguments(e.Args, out dataRoot, out startMinimized);

#if PREVIEW
            IsPreview = true;
            if (string.IsNullOrWhiteSpace(dataRoot))
                dataRoot = Path.Combine(Path.GetTempPath(), "IPBanManagerPreview");
#endif

            string mutexName = IsPreview ? "Local\\IPBanManagerPreview_SingleInstance" : "Local\\IPBanManager_SingleInstance";
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("净域已经在运行中，请从任务栏托盘打开。", "净域",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            try
            {
                StoragePaths.Initialize(dataRoot);
                LogService.Info("========== 净域启动 ==========");
                LogService.Info("数据目录：" + StoragePaths.Root);
                LogService.Info("预览模式：" + IsPreview);

                IsAdmin = IsPreview || AdminService.IsElevated();
                LogService.Info("管理员权限：" + IsAdmin);

                string warning;
                AppData data = DataService.LoadData(out warning);
                AppSettings settings = DataService.LoadSettings();
                ThemeSettings theme = DataService.LoadTheme();

#if PREVIEW
                PreparePreviewEnvironment(data, settings);
#endif

                State = new AppState(data, settings, theme, warning);
                ThemeManager.Apply(State.Theme);

                MainWindowInstance = new MainWindow();
                MainWindow = MainWindowInstance;
                MainWindowInstance.Show();

                if (startMinimized || State.Settings.StartMinimized)
                {
                    MainWindowInstance.HideToTray(false);
                }

                if (!string.IsNullOrEmpty(warning))
                {
                    MainWindowInstance.ShowToast("数据已自动修复：" + warning, false);
                }

                if (!IsAdmin && !IsPreview)
                {
                    MainWindowInstance.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        MainWindowInstance.ShowAdminWarning();
                    }), DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                LogService.Error("应用启动失败", ex);
                MessageBox.Show("净域启动失败：" + ex.Message + Environment.NewLine +
                    "日志：" + LogService.CurrentLogFile, "启动失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            IsExiting = true;
            try
            {
                if (MainWindowInstance != null) MainWindowInstance.DisposeTray();
                if (State != null)
                {
                    State.PersistData();
                    State.SaveSettingsFile();
                    DataService.SaveTheme(State.Theme);
                }
                LogService.Info("========== 净域退出 ==========");
            }
            catch (Exception ex)
            {
                LogService.Error("退出清理失败", ex);
            }
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
            }
            base.OnExit(e);
        }

        public static void ShowToast(string message, bool success)
        {
            if (MainWindowInstance != null) MainWindowInstance.ShowToast(message, success);
        }

        /// <summary>统一处理 HOSTS 操作结果：成功 Toast，权限不足时引导重启，失败时给出明确原因。</summary>
        public static void ReportHostsResult(Window owner, HostsApplyResult result)
        {
            if (result == null)
            {
                ShowToast("规则已保存，当前为手动应用模式", true);
                return;
            }

            if (result.Success)
            {
                ShowToast(result.Message, true);
                if (result.Conflicts != null && result.Conflicts.Count > 0)
                {
                    ShowToast("注意：HOSTS 块外存在同域名记录：" + string.Join("、", result.Conflicts.ToArray()), false);
                }
                return;
            }

            if (result.RequiresElevation)
            {
                string error;
                bool restart = ConfirmDialog.Show(owner, "需要管理员权限",
                    (result.Error ?? result.Message) + Environment.NewLine + Environment.NewLine +
                    "写入系统 HOSTS 需要管理员权限。是否以管理员身份重启净域？",
                    "以管理员身份重启", "取消", false);
                if (restart)
                {
                    if (AdminService.TryRelaunchElevated("", out error))
                    {
                        IsExiting = true;
                        Current.Shutdown();
                    }
                    else
                    {
                        ShowToast("提权重启失败：" + error, false);
                    }
                }
                return;
            }

            ShowToast(result.Error ?? result.Message ?? "应用 HOSTS 失败", false);
        }

        public static void ReportDnsResult(DnsFlushResult result)
        {
            if (result == null) return;
            ShowToast(result.Message, result.Success);
        }

        private static void ParseArguments(string[] args, out string dataRoot, out bool startMinimized)
        {
            dataRoot = "";
            startMinimized = false;
            if (args == null) return;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i] ?? "";
                if (string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                }
                else if (string.Equals(arg, "--data-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dataRoot = args[i + 1];
                    i++;
                }
                else if (arg.StartsWith("--data-dir=", StringComparison.OrdinalIgnoreCase))
                {
                    dataRoot = arg.Substring("--data-dir=".Length).Trim('"');
                }
                else if (string.Equals(arg, "--page", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    InitialPage = args[i + 1];
                    i++;
                }
                else if (arg.StartsWith("--page=", StringComparison.OrdinalIgnoreCase))
                {
                    InitialPage = arg.Substring("--page=".Length).Trim('"');
                }
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogService.Error("未处理的界面线程异常", e.Exception);
            ShowToast("发生未处理错误：" + e.Exception.Message, false);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            LogService.Error("未处理的应用程序异常", ex);
        }

#if PREVIEW
        private static void PreparePreviewEnvironment(AppData data, AppSettings settings)
        {
            string previewHosts = Path.Combine(StoragePaths.Root, "preview-hosts");
            if (!File.Exists(previewHosts))
            {
                File.WriteAllText(previewHosts,
                    "# 预览模式使用的临时 HOSTS，不会修改系统文件\r\n" +
                    "127.0.0.1 local.preview\r\n", new System.Text.UTF8Encoding(false));
            }
            settings.HostsPath = previewHosts;
            settings.BackupDirectory = Path.Combine(StoragePaths.Root, "backups");
            settings.AutoApplyHosts = true;

            if (data.Rules.Count == 0)
            {
                BanGroup video = null;
                BanGroup study = null;
                BanGroup game = null;
                BanGroup ad = null;
                foreach (BanGroup group in data.Groups)
                {
                    if (group.Id == "video") video = group;
                    else if (group.Id == "study") study = group;
                    else if (group.Id == "game") game = group;
                    else if (group.Id == "ad") ad = group;
                }

                AddPreviewRule(data, "bilibili.com", video, "哔哩哔哩", true, "auto", "");
                AddPreviewRule(data, "www.bilibili.com", video, "哔哩哔哩", true, "auto", "");
                AddPreviewRule(data, "m.bilibili.com", video, "哔哩哔哩", true, "auto", "");
                AddPreviewRule(data, "douyin.com", video, "抖音网页版", true, "auto", "");
                AddPreviewRule(data, "weibo.com", video, "微博", false, "auto", "");
                AddPreviewRule(data, "zhihu.com", study, "知乎", true, "auto", "");
                AddPreviewRule(data, "4399.com", game, "小游戏", true, "auto", "");
                AddPreviewRule(data, "steamcommunity.com", game, "Steam 社区", false, "auto", "");
                AddPreviewRule(data, "doubleclick.net", ad, "广告联盟", true, "auto", "");
                AddPreviewRule(data, "googlesyndication.com", ad, "广告脚本", true, "custom", "127.0.0.1");
                AddPreviewRule(data, "example-blocked.com", null, "自定义示例", true, "auto", "");
            }

            if (data.Operations.Count == 0)
            {
                data.Operations.Add(new OperationRecord
                {
                    Time = DateTime.Now.AddMinutes(-2),
                    Title = "应用 HOSTS",
                    Detail = "HOSTS 已更新，DNS 缓存已刷新 · 备份 hosts_20261113_133200_120.bak",
                    Success = true,
                    Kind = "apply"
                });
                data.Operations.Add(new OperationRecord
                {
                    Time = DateTime.Now.AddMinutes(-9),
                    Title = "添加封禁规则",
                    Detail = "新增 4 条，更新 0 条 · bilibili.com、www.bilibili.com、m.bilibili.com",
                    Success = true,
                    Kind = "add"
                });
                data.Operations.Add(new OperationRecord
                {
                    Time = DateTime.Now.AddHours(-1).AddMinutes(-7),
                    Title = "刷新 DNS",
                    Detail = "DNS 缓存已刷新（Windows API + ipconfig /flushdns）",
                    Success = true,
                    Kind = "dns"
                });
                data.Operations.Add(new OperationRecord
                {
                    Time = DateTime.Now.AddDays(-1).AddHours(-2),
                    Title = "保存主题",
                    Detail = "强调色 #4C8DFF · 圆角 14",
                    Success = true,
                    Kind = "theme"
                });
            }

            try
            {
                string[] existingBackups = Directory.GetFiles(settings.BackupDirectory, "hosts_*.bak");
                if (existingBackups.Length == 0)
                {
                    BackupManager.CreateBackup(previewHosts, settings.BackupDirectory, "应用规则前自动备份");
                    System.Threading.Thread.Sleep(5);
                    BackupManager.CreateBackup(previewHosts, settings.BackupDirectory, "手动备份");
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("创建预览备份失败：" + ex.Message);
            }
        }

        private static void AddPreviewRule(AppData data, string domain, BanGroup group,
            string remark, bool enabled, string ipMode, string customIp)
        {
            data.Rules.Add(new BanRule
            {
                Domain = domain,
                GroupId = group == null ? "custom" : group.Id,
                Remark = remark,
                Enabled = enabled,
                IpMode = ipMode,
                CustomIp = customIp,
                CreatedAt = DateTime.Now.AddDays(-1),
                UpdatedAt = DateTime.Now
            });
        }
#endif
    }
}
