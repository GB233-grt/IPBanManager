using System.Windows;
using System.Windows.Controls;
using IPBanManager.Services;

namespace IPBanManager.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = App.State;
        }

        private Window OwnerWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindowInstance != null) App.MainWindowInstance.OpenAddRule();
        }

        private void RefreshDns_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null) return;
            App.ReportDnsResult(App.State.RefreshDnsOnly());
        }

        private void ApplyHosts_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null) return;
            App.ReportHostsResult(OwnerWindow, App.State.ApplyHosts());
        }

        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindowInstance != null) App.MainWindowInstance.NavigateTo("logs");
        }

        private void OpenHostsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindowInstance != null) App.MainWindowInstance.OpenHostsFolder();
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
        }

        private void EnableAll_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null || App.State.Rules.Count == 0) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "全部启用",
                "确定要启用全部 " + App.State.Rules.Count + " 条规则吗？分组暂停状态不会被改变。",
                "全部启用", "取消", false);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.SetAllRulesEnabled(App.State.Rules, true));
        }

        private void DisableAll_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null || App.State.Rules.Count == 0) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "全部禁用",
                "确定要禁用全部 " + App.State.Rules.Count + " 条规则吗？HOSTS 中的托管规则会被注释掉。",
                "全部禁用", "取消", true);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.SetAllRulesEnabled(App.State.Rules, false));
        }
    }
}
