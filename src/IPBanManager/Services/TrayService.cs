using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using IPBanManager.Services;
using Forms = System.Windows.Forms;

namespace IPBanManager
{
    /// <summary>
    /// 托盘图标与快捷菜单。使用 WinForms NotifyIcon，避免引入第三方托盘库。
    /// </summary>
    public class TrayService : IDisposable
    {
        private readonly MainWindow _window;
        private Forms.NotifyIcon _notifyIcon;
        private Icon _icon;

        public TrayService(MainWindow window)
        {
            _window = window;
            Create();
        }

        private void Create()
        {
            _notifyIcon = new Forms.NotifyIcon();
            _icon = LoadIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.Text = "净域 · 本地 HOSTS 封禁工具";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { _window.Dispatcher.Invoke(new Action(_window.ShowFromTray)); };

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            menu.Items.Add("打开主界面", null, delegate { _window.Dispatcher.Invoke(new Action(_window.ShowFromTray)); });
            menu.Items.Add("添加封禁规则", null, delegate { _window.Dispatcher.Invoke(new Action(_window.OpenAddRule)); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("全部启用", null, delegate
            {
                _window.Dispatcher.Invoke(new Action(delegate
                {
                    if (App.State == null) return;
                    HostsApplyResult result = App.State.SetAllRulesEnabled(App.State.Rules, true);
                    App.ReportHostsResult(_window, result);
                }));
            });
            menu.Items.Add("全部禁用", null, delegate
            {
                _window.Dispatcher.Invoke(new Action(delegate
                {
                    if (App.State == null) return;
                    HostsApplyResult result = App.State.SetAllRulesEnabled(App.State.Rules, false);
                    App.ReportHostsResult(_window, result);
                }));
            });
            menu.Items.Add("刷新 DNS 缓存", null, delegate
            {
                _window.Dispatcher.Invoke(new Action(delegate
                {
                    if (App.State == null) return;
                    App.ReportDnsResult(App.State.RefreshDnsOnly());
                }));
            });
            menu.Items.Add("打开 HOSTS 所在目录", null, delegate
            {
                _window.Dispatcher.Invoke(new Action(_window.OpenHostsFolder));
            });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("退出净域", null, delegate { _window.Dispatcher.Invoke(new Action(_window.ExitApplication)); });
            _notifyIcon.ContextMenuStrip = menu;
        }

        public void ShowBalloon(string title, string text)
        {
            if (_notifyIcon == null) return;
            try
            {
                _notifyIcon.BalloonTipTitle = title;
                _notifyIcon.BalloonTipText = text;
                _notifyIcon.ShowBalloonTip(2500);
            }
            catch
            {
                // 气泡提示失败不影响功能。
            }
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            if (_icon != null)
            {
                _icon.Dispose();
                _icon = null;
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                string exe = Assembly.GetExecutingAssembly().Location;
                Icon icon = Icon.ExtractAssociatedIcon(exe);
                if (icon != null) return icon;
            }
            catch (Exception ex)
            {
                LogService.Warn("读取程序图标失败：" + ex.Message);
            }
            return SystemIcons.Shield;
        }
    }
}
