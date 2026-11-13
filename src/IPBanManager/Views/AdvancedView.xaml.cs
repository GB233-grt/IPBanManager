using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using IPBanManager.Models;
using IPBanManager.Services;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace IPBanManager.Views
{
    public partial class AdvancedView : UserControl
    {
        private bool _loading;

        public AdvancedView()
        {
            _loading = true;
            InitializeComponent();
            LoadSettings();
            _loading = false;
        }

        private Window OwnerWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void LoadSettings()
        {
            if (App.State == null) return;
            AppSettings settings = App.State.Settings;
            HostsPathBox.Text = settings.HostsPath;
            BackupDirBox.Text = settings.BackupDirectory;
            RetentionBox.Text = settings.BackupRetention.ToString();
            BomCheck.IsChecked = settings.WriteUtf8Bom;
            BlockTemplateBox.Text = settings.BlockTemplate;
            RedirectTemplateBox.Text = settings.RedirectTemplate;
            PrefixesBox.Text = settings.ExpansionPrefixes;
            AutoApplySwitch.IsChecked = settings.AutoApplyHosts;
            ExpandSwitch.IsChecked = settings.AutoExpandSubdomains;
            TraySwitch.IsChecked = settings.TrayEnabled;
            MinimizeTraySwitch.IsChecked = settings.MinimizeToTray;
            CloseTraySwitch.IsChecked = settings.CloseToTray;
            StartSwitch.IsChecked = settings.StartWithWindows;
            StartMinSwitch.IsChecked = settings.StartMinimized;
            DataDirText.Text = StoragePaths.Root;
            SelectDnsMode(settings.DnsFlushMode);

            PermissionText.Text = App.State.IsPreview ? "预览模式" : (App.State.IsAdmin ? "管理员" : "普通权限");
            PermissionDetail.Text = App.State.AdminStatusDetail;
            if (App.IsPreview)
            {
                ElevateButton.IsEnabled = false;
                ElevateButton.ToolTip = "预览模式不需要提权";
            }
            else
            {
                ElevateButton.IsEnabled = !App.IsAdmin;
            }

            UpdateTemplatePreview();
        }

        private void SelectDnsMode(string mode)
        {
            for (int i = 0; i < DnsModeCombo.Items.Count; i++)
            {
                ComboBoxItem item = DnsModeCombo.Items[i] as ComboBoxItem;
                if (item != null && string.Equals(item.Tag as string, mode, StringComparison.OrdinalIgnoreCase))
                {
                    DnsModeCombo.SelectedIndex = i;
                    return;
                }
            }
            DnsModeCombo.SelectedIndex = 0;
        }

        private string SelectedDnsMode()
        {
            ComboBoxItem item = DnsModeCombo.SelectedItem as ComboBoxItem;
            return item == null ? "auto" : (item.Tag as string ?? "auto");
        }

        private void Template_Changed(object sender, TextChangedEventArgs e)
        {
            if (_loading) return;
            UpdateTemplatePreview();
        }

        private void UpdateTemplatePreview()
        {
            if (TemplatePreview == null) return;
            string block = Render(BlockTemplateBox.Text, "0.0.0.0", "example.com");
            string redirect = Render(RedirectTemplateBox.Text, "127.0.0.1", "example.com");
            TemplatePreview.Text = "# 封禁：示例 [自定义]" + Environment.NewLine +
                block + Environment.NewLine + redirect;
        }

        private static string Render(string template, string ip, string domain)
        {
            string text = string.IsNullOrWhiteSpace(template) ? ip + " {domain}" : template;
            text = text.Replace("{domain}", domain).Replace("{ip}", ip);
            if (text.IndexOf(domain, StringComparison.OrdinalIgnoreCase) < 0) text += " " + domain;
            return text;
        }

        private void BrowseHosts_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择 HOSTS 文件";
            dialog.Filter = "HOSTS 文件 (hosts)|hosts|所有文件 (*.*)|*.*";
            dialog.CheckFileExists = true;
            try
            {
                string directory = Path.GetDirectoryName(HostsPathBox.Text);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    dialog.InitialDirectory = directory;
            }
            catch
            {
                // 初始目录不可用时不设置。
            }
            if (dialog.ShowDialog(OwnerWindow) == true) HostsPathBox.Text = dialog.FileName;
        }

        private void BrowseBackupDir_Click(object sender, RoutedEventArgs e)
        {
            using (Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择 HOSTS 备份目录";
                dialog.ShowNewFolderButton = true;
                try
                {
                    if (!string.IsNullOrEmpty(BackupDirBox.Text) && Directory.Exists(BackupDirBox.Text))
                        dialog.SelectedPath = BackupDirBox.Text;
                }
                catch
                {
                    // 忽略。
                }
                if (dialog.ShowDialog() == Forms.DialogResult.OK) BackupDirBox.Text = dialog.SelectedPath;
            }
        }

        private void OpenHostsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string directory = Path.GetDirectoryName(HostsPathBox.Text);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                    AdminService.OpenFolder(directory);
                else
                    App.ShowToast("HOSTS 所在目录不存在", false);
            }
            catch (Exception ex)
            {
                App.ShowToast("打开目录失败：" + ex.Message, false);
            }
        }

        private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string directory = BackupDirBox.Text;
                if (string.IsNullOrWhiteSpace(directory)) directory = StoragePaths.BackupsDirectory;
                StoragePaths.EnsureDirectory(directory);
                AdminService.OpenFolder(directory);
            }
            catch (Exception ex)
            {
                App.ShowToast("打开备份目录失败：" + ex.Message, false);
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            AdminService.OpenFolder(StoragePaths.Root);
        }

        private void TestDns_Click(object sender, RoutedEventArgs e)
        {
            DnsFlushResult result = DnsService.Flush(SelectedDnsMode());
            string message = result.Message;
            if (!result.Success && !string.IsNullOrEmpty(result.Detail))
                message += "：" + result.Detail;
            App.ShowToast(message, result.Success);
        }

        private void Elevate_Click(object sender, RoutedEventArgs e)
        {
            if (App.IsPreview)
            {
                App.ShowToast("预览模式不需要提权", true);
                return;
            }
            string error;
            if (AdminService.TryRelaunchElevated("", out error))
            {
                App.IsExiting = true;
                Application.Current.Shutdown();
            }
            else
            {
                App.ShowToast("提权重启失败：" + error, false);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveCore(false);
        }

        private void SaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            SaveCore(true);
        }

        private void SaveCore(bool applyHosts)
        {
            if (App.State == null) return;
            AppSettings settings = App.State.Settings;

            string hostsPath = (HostsPathBox.Text ?? "").Trim();
            if (hostsPath.Length == 0)
            {
                App.ShowToast("HOSTS 路径不能为空", false);
                return;
            }
            try
            {
                hostsPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(hostsPath));
            }
            catch (Exception ex)
            {
                App.ShowToast("HOSTS 路径无效：" + ex.Message, false);
                return;
            }
            string hostsDirectory = Path.GetDirectoryName(hostsPath);
            if (string.IsNullOrEmpty(hostsDirectory) || !Directory.Exists(hostsDirectory))
            {
                App.ShowToast("HOSTS 所在目录不存在：" + hostsDirectory, false);
                return;
            }

            string backupDir = (BackupDirBox.Text ?? "").Trim();
            if (backupDir.Length == 0) backupDir = StoragePaths.BackupsDirectory;
            try
            {
                backupDir = StoragePaths.EnsureDirectory(backupDir);
            }
            catch (Exception ex)
            {
                App.ShowToast("备份目录不可用：" + ex.Message, false);
                return;
            }

            int retention;
            if (!int.TryParse(RetentionBox.Text, out retention) || retention < 1 || retention > 500)
            {
                App.ShowToast("备份保留数量需要是 1 到 500 之间的整数", false);
                return;
            }

            string blockTemplate = (BlockTemplateBox.Text ?? "").Trim();
            string redirectTemplate = (RedirectTemplateBox.Text ?? "").Trim();
            if (blockTemplate.Length == 0) blockTemplate = "0.0.0.0 {domain}";
            if (redirectTemplate.Length == 0) redirectTemplate = "127.0.0.1 {domain}";
            if (blockTemplate.IndexOf("{domain}", StringComparison.OrdinalIgnoreCase) < 0)
                blockTemplate += " {domain}";
            if (redirectTemplate.IndexOf("{domain}", StringComparison.OrdinalIgnoreCase) < 0)
                redirectTemplate += " {domain}";

            string prefixes = (PrefixesBox.Text ?? "").Trim();
            if (DomainUtil.ParsePrefixes(prefixes).Count == 0) prefixes = "www,m,api";

            settings.HostsPath = hostsPath;
            settings.BackupDirectory = backupDir;
            settings.BackupRetention = retention;
            settings.WriteUtf8Bom = BomCheck.IsChecked == true;
            settings.BlockTemplate = blockTemplate;
            settings.RedirectTemplate = redirectTemplate;
            settings.ExpansionPrefixes = prefixes;
            settings.AutoApplyHosts = AutoApplySwitch.IsChecked == true;
            settings.AutoExpandSubdomains = ExpandSwitch.IsChecked == true;
            settings.TrayEnabled = TraySwitch.IsChecked == true;
            settings.MinimizeToTray = MinimizeTraySwitch.IsChecked == true;
            settings.CloseToTray = CloseTraySwitch.IsChecked == true;
            settings.StartWithWindows = StartSwitch.IsChecked == true;
            settings.StartMinimized = StartMinSwitch.IsChecked == true;
            settings.DnsFlushMode = SelectedDnsMode();

            AutoStartResult autoStart = App.State.SaveSettings();
            if (App.MainWindowInstance != null) App.MainWindowInstance.RefreshTray();
            _loading = true;
            LoadSettings();
            _loading = false;

            if (autoStart != null && !autoStart.Success && !string.IsNullOrEmpty(autoStart.Message))
            {
                App.ShowToast("设置已保存，但" + autoStart.Message, false);
                return;
            }

            App.ShowToast(applyHosts ? "设置已保存，正在应用 HOSTS…" : "设置已保存", true);
            if (applyHosts)
            {
                App.ReportHostsResult(OwnerWindow, App.State.ApplyHosts());
            }
        }
    }
}
