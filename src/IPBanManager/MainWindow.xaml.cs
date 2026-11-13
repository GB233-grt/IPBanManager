using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using IPBanManager.Services;
using IPBanManager.Views;

namespace IPBanManager
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, UserControl> _views = new Dictionary<string, UserControl>();
        private TrayService _tray;
        private DispatcherTimer _toastTimer;
        private string _currentPage = "";
        private bool _adminPromptShown;
        private bool _reallyClosing;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;

            if (App.State != null)
            {
                App.State.DataChanged += State_DataChanged;
                App.State.PersistenceFailed += State_PersistenceFailed;
                if (App.State.Settings.TrayEnabled) _tray = new TrayService(this);
            }
        }

        public void ShowToast(string message, bool success)
        {
            if (ToastBorder == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { ShowToast(message, success); }));
                return;
            }
            if (!IsVisible && _tray != null)
            {
                _tray.ShowBalloon(success ? "净域" : "净域 - 操作失败", message ?? "");
                return;
            }

            ToastText.Text = message ?? "";
            ToastIcon.Text = success ? "\uE73E" : "\uE7BA";
            ToastIcon.Foreground = (Brush)FindResource(success ? "SuccessBrush" : "DangerBrush");
            ToastIconBadge.Background = (Brush)FindResource(success ? "SuccessSoftBrush" : "DangerSoftBrush");
            ToastBorder.Visibility = Visibility.Visible;
            ToastBorder.Opacity = 0;
            ToastTransform.Y = 12;

            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            ToastBorder.BeginAnimation(OpacityProperty, fade);

            DoubleAnimation slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(220));
            slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            ToastTransform.BeginAnimation(TranslateTransform.YProperty, slide);

            if (_toastTimer == null)
            {
                _toastTimer = new DispatcherTimer();
                _toastTimer.Interval = TimeSpan.FromMilliseconds(3600);
                _toastTimer.Tick += delegate { HideToast(); };
            }
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            if (_toastTimer != null) _toastTimer.Stop();
            DoubleAnimation fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
            fade.Completed += delegate { ToastBorder.Visibility = Visibility.Collapsed; };
            ToastBorder.BeginAnimation(OpacityProperty, fade);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FitToWorkArea();
            if (App.IsPreview)
            {
                PreviewPill.Visibility = Visibility.Visible;
            }
            RefreshChrome();
            RefreshSidebar();
            ApplyWindowBackground();
            string page = string.IsNullOrWhiteSpace(App.InitialPage) ? "dashboard" : App.InitialPage;
            NavigateTo(page);
        }

        private void FitToWorkArea()
        {
            NativeRect work;
            if (!SystemParametersInfo(0x0030, 0, out work, 0)) return;

            uint dpi = 96;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                uint windowDpi = GetDpiForWindow(handle);
                if (windowDpi >= 96) dpi = windowDpi;
            }
            double scale = dpi / 96.0;
            double workWidth = (work.Right - work.Left) / scale;
            double workHeight = (work.Bottom - work.Top) / scale;

            double width = Math.Min(Width, Math.Max(MinWidth, workWidth - (20 / scale)));
            double height = Math.Min(Height, Math.Max(MinHeight, workHeight - (16 / scale)));
            if (width < Width) Width = width;
            if (height < Height) Height = height;
            Left = (work.Left / scale) + Math.Max(0, (workWidth - Width) / 2);
            Top = (work.Top / scale) + Math.Max(0, (workHeight - Height) / 2);
            LogService.Info(string.Format("窗口适配：DPI={0} 工作区={1}x{2} 窗口={3}x{4}",
                dpi, workWidth, workHeight, Width, Height));
        }

        private void State_DataChanged(object sender, EventArgs e)
        {
            RefreshSidebar();
        }

        private void State_PersistenceFailed(object sender, string message)
        {
            ShowToast(message, false);
        }

        private void RefreshChrome()
        {
            if (App.IsPreview)
            {
                PreviewPill.Visibility = Visibility.Visible;
                AdminPill.Visibility = Visibility.Collapsed;
                AdminPill.Background = (Brush)FindResource("AccentSoftBrush");
                AdminPillIcon.Text = "\uE946";
                AdminPillIcon.Foreground = (Brush)FindResource("AccentBrush");
                AdminPillText.Text = "预览模式";
                AdminPillText.Foreground = (Brush)FindResource("AccentBrush");
                AdminBanner.Visibility = Visibility.Collapsed;
                return;
            }
            AdminPill.Visibility = Visibility.Visible;

            if (App.IsAdmin)
            {
                AdminPill.Background = (Brush)FindResource("SuccessSoftBrush");
                AdminPillIcon.Text = "\uE73E";
                AdminPillIcon.Foreground = (Brush)FindResource("SuccessBrush");
                AdminPillText.Text = "管理员";
                AdminPillText.Foreground = (Brush)FindResource("SuccessBrush");
            }
            else
            {
                AdminPill.Background = (Brush)FindResource("WarningSoftBrush");
                AdminPillIcon.Text = "\uE7BA";
                AdminPillIcon.Foreground = (Brush)FindResource("WarningBrush");
                AdminPillText.Text = "未提权";
                AdminPillText.Foreground = (Brush)FindResource("WarningBrush");
            }
        }

        private void RefreshSidebar()
        {
            if (App.State == null) return;
            SideStatusText.Text = App.State.EnabledRuleCount + " 条规则生效";
            SideHostsText.Text = "HOSTS · " + App.State.RuleCount + " 条规则 · " + App.State.BehaviorSummary;
            SideHostsText.ToolTip = App.State.HostsPathDisplay;
            RefreshChrome();
        }

        public void NavigateTo(string key)
        {
            if (string.IsNullOrEmpty(key) || key == _currentPage) return;
            UserControl view;
            if (!_views.TryGetValue(key, out view))
            {
                switch (key)
                {
                    case "dashboard": view = new DashboardView(); break;
                    case "rules": view = new RulesView(); break;
                    case "groups": view = new GroupsView(); break;
                    case "theme": view = new ThemeView(); break;
                    case "advanced": view = new AdvancedView(); break;
                    case "logs": view = new LogsView(); break;
                    default: return;
                }
                _views[key] = view;
            }

            _currentPage = key;
            PageHost.Content = view;
            SetNavState(key);
            AnimatePage();
        }

        private void SetNavState(string key)
        {
            RadioButton button = null;
            if (key == "dashboard") button = NavDashboard;
            else if (key == "rules") button = NavRules;
            else if (key == "groups") button = NavGroups;
            else if (key == "theme") button = NavTheme;
            else if (key == "advanced") button = NavAdvanced;
            else if (key == "logs") button = NavLogs;
            if (button != null) button.IsChecked = true;
        }

        private void AnimatePage()
        {
            PageHost.Opacity = 0;
            PageTransform.X = 16;
            DoubleAnimation fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            DoubleAnimation slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(240));
            slide.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            PageHost.BeginAnimation(OpacityProperty, fade);
            PageTransform.BeginAnimation(TranslateTransform.XProperty, slide);
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            RadioButton button = sender as RadioButton;
            if (button != null) NavigateTo(button.Tag as string);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            }
            catch
            {
                // 拖动过程中可能因窗口状态切换抛异常，忽略。
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (MaximizeButton != null)
                MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";

            if (WindowState == WindowState.Minimized && _tray != null &&
                App.State != null && App.State.Settings.MinimizeToTray)
            {
                Dispatcher.BeginInvoke(new Action(delegate { HideToTray(true); }), DispatcherPriority.Background);
            }
        }

        public void HideToTray(bool showBalloon)
        {
            if (_tray == null) return;
            ShowInTaskbar = false;
            Hide();
            if (showBalloon) _tray.ShowBalloon("净域仍在运行", "双击托盘图标可以重新打开主界面。");
        }

        public void ShowFromTray()
        {
            Show();
            ShowInTaskbar = true;
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        public void RefreshTray()
        {
            if (App.State == null) return;
            if (App.State.Settings.TrayEnabled)
            {
                if (_tray == null) _tray = new TrayService(this);
            }
            else if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
                if (!IsVisible) ShowFromTray();
            }
        }

        public void DisposeTray()
        {
            if (_tray != null)
            {
                _tray.Dispose();
                _tray = null;
            }
        }

        public void OpenAddRule()
        {
            if (!IsVisible) ShowFromTray();
            NavigateTo("rules");
            Dispatcher.BeginInvoke(new Action(delegate
            {
                RulesView view = PageHost.Content as RulesView;
                if (view != null) view.ShowAddDialog();
            }), DispatcherPriority.Background);
        }

        public void ShowRulesForGroup(string groupId)
        {
            NavigateTo("rules");
            Dispatcher.BeginInvoke(new Action(delegate
            {
                RulesView view = PageHost.Content as RulesView;
                if (view != null) view.SetGroupFilter(groupId);
            }), DispatcherPriority.Background);
        }

        public void OpenHostsFolder()
        {
            if (App.State == null) return;
            try
            {
                string directory = Path.GetDirectoryName(App.State.Settings.HostsPath);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    AdminService.OpenFolder(directory);
                }
                else
                {
                    ShowToast("HOSTS 目录不存在：" + directory, false);
                }
            }
            catch (Exception ex)
            {
                ShowToast("打开目录失败：" + ex.Message, false);
            }
        }

        public void ShowAdminWarning()
        {
            if (App.IsPreview || App.IsAdmin) return;
            AdminBanner.Visibility = Visibility.Visible;
            if (_adminPromptShown) return;
            _adminPromptShown = true;

            bool restart = ConfirmDialog.Show(this, "未以管理员身份运行",
                "净域需要管理员权限才能写入 HOSTS 文件。当前可以浏览规则，但保存会失败。" +
                Environment.NewLine + Environment.NewLine + "是否以管理员身份重启？",
                "以管理员身份重启", "以只读模式继续", false);
            if (restart) TryElevate();
        }

        private void ElevateButton_Click(object sender, RoutedEventArgs e)
        {
            TryElevate();
        }

        private void DismissAdminBanner_Click(object sender, RoutedEventArgs e)
        {
            AdminBanner.Visibility = Visibility.Collapsed;
        }

        private void TryElevate()
        {
            if (App.IsPreview)
            {
                ShowToast("预览模式不需要提权", true);
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
                ShowToast("提权重启失败：" + error, false);
            }
        }

        public void ApplyWindowBackground()
        {
            if (App.State == null) return;
            try
            {
                string path = App.State.Theme.BackgroundImagePath;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    BackgroundImageBrush.ImageSource = image;
                    BackgroundImageLayer.Opacity = App.State.Theme.BackgroundImageOpacity;
                }
                else
                {
                    BackgroundImageBrush.ImageSource = null;
                    BackgroundImageLayer.Opacity = 0;
                }
            }
            catch (Exception ex)
            {
                BackgroundImageBrush.ImageSource = null;
                BackgroundImageLayer.Opacity = 0;
                LogService.Warn("加载背景图失败：" + ex.Message);
            }
        }

        public void ExitApplication()
        {
            _reallyClosing = true;
            App.IsExiting = true;
            DisposeTray();
            Application.Current.Shutdown();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyClosing && !App.IsExiting && App.State != null &&
                App.State.Settings.CloseToTray && App.State.Settings.TrayEnabled && _tray != null)
            {
                e.Cancel = true;
                HideToTray(true);
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            DisposeTray();
            base.OnClosed(e);
            if (!App.IsExiting)
            {
                App.IsExiting = true;
                Application.Current.Shutdown();
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int corner = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
                if (App.State != null && App.State.Theme.IsDark)
                {
                    int dark = 1;
                    DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
                }
            }
            catch
            {
                // Windows 10 不支持圆角偏好，忽略即可。
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint parameter, out NativeRect rect, uint flags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
