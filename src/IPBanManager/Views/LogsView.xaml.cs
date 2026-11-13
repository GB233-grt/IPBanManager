using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager.Views
{
    public partial class LogsView : UserControl
    {
        private readonly ObservableCollection<BackupItem> _backups = new ObservableCollection<BackupItem>();
        private ICollectionView _logView;
        private bool _initialized;

        public LogsView()
        {
            InitializeComponent();
            DataContext = App.State;
            BackupsList.ItemsSource = _backups;

            if (App.State != null)
            {
                _logView = CollectionViewSource.GetDefaultView(App.State.Operations);
                _logView.Filter = FilterLog;
                LogList.ItemsSource = _logView;
                App.State.DataChanged += State_DataChanged;
            }
            _initialized = true;
            UpdateLogCount();
            RefreshBackups();
        }

        private Window OwnerWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void State_DataChanged(object sender, EventArgs e)
        {
            if (!_initialized) return;
            if (_logView != null) _logView.Refresh();
            UpdateLogCount();
            RefreshBackups();
        }

        private bool FilterLog(object item)
        {
            OperationRecord record = item as OperationRecord;
            if (record == null) return false;
            string query = LogSearchBox == null ? "" : (LogSearchBox.Text ?? "").Trim();
            if (query.Length == 0) return true;
            return (record.Title ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (record.Detail ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LogSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogSearchPlaceholder.Visibility = string.IsNullOrEmpty(LogSearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
            if (_logView != null) _logView.Refresh();
            UpdateLogCount();
        }

        private void UpdateLogCount()
        {
            if (LogCountText == null || _logView == null) return;
            int count = _logView.Cast<OperationRecord>().Count();
            LogCountText.Text = count + " 条";
        }

        private void RefreshBackups()
        {
            if (App.State == null) return;
            _backups.Clear();
            foreach (BackupItem item in App.State.LoadBackups()) _backups.Add(item);
            BackupCountText.Text = _backups.Count + " 个";
            NoBackupPanel.Visibility = _backups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshBackups();
            if (_logView != null) _logView.Refresh();
            UpdateLogCount();
            App.ShowToast("列表已刷新", true);
        }

        private void BackupNow_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null) return;
            BackupItem item = App.State.BackupNow();
            if (item == null)
            {
                App.ShowToast(App.State.LastOperationError ?? "备份失败", false);
                return;
            }
            App.ShowToast("HOSTS 已备份：" + item.FileName, true);
            RefreshBackups();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            BackupItem item = BackupsList.SelectedItem as BackupItem;
            if (item == null)
            {
                App.ShowToast("请先在右侧列表中选择一个备份", false);
                return;
            }
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "恢复 HOSTS 备份",
                "确定要恢复备份 " + item.FileName + " 吗？" + Environment.NewLine +
                "当前 HOSTS 会先自动备份，恢复后立即刷新 DNS。规则列表不会改变。",
                "恢复备份", "取消", true);
            if (!confirmed) return;

            HostsApplyResult result = App.State.RestoreBackup(item.FilePath);
            if (result.RequiresElevation)
            {
                App.ReportHostsResult(OwnerWindow, result);
                return;
            }
            App.ShowToast(result.Message, result.Success);
            RefreshBackups();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null || App.State.Operations.Count == 0) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "清空操作记录",
                "确定清空界面中的 " + App.State.Operations.Count + " 条操作记录吗？文件日志不会被删除。",
                "清空记录", "取消", true);
            if (!confirmed) return;
            App.State.ClearOperations();
            App.ShowToast("操作记录已清空", true);
        }

        private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
        {
            AdminService.OpenFolder(StoragePaths.LogsDirectory);
        }

        private void OpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StoragePaths.EnsureDirectory(App.State.Settings.BackupDirectory);
                AdminService.OpenFolder(App.State.Settings.BackupDirectory);
            }
            catch (Exception ex)
            {
                App.ShowToast("打开备份目录失败：" + ex.Message, false);
            }
        }
    }
}
