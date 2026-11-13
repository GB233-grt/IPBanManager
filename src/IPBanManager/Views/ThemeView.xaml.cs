using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IPBanManager.Models;
using IPBanManager.Services;
using Microsoft.Win32;

namespace IPBanManager.Views
{
    public partial class ThemeView : UserControl
    {
        private bool _loading;

        public ThemeView()
        {
            _loading = true;
            InitializeComponent();
            DataContext = App.State;
            LoadControls();
            _loading = false;
        }

        private ThemeSettings Theme
        {
            get { return App.State.Theme; }
        }

        private void LoadControls()
        {
            if (App.State == null) return;
            ThemeSettings theme = Theme;
            DarkSwitch.IsChecked = theme.IsDark;
            AccentHexBox.Text = theme.AccentColor;
            BackgroundHexBox.Text = theme.BackgroundColor;
            CornerSlider.Value = theme.CornerRadius;
            OpacitySlider.Value = theme.CardOpacity;
            BgImageOpacitySlider.Value = theme.BackgroundImageOpacity;
            BackgroundFileText.Text = string.IsNullOrWhiteSpace(theme.BackgroundImagePath)
                ? "未选择背景图"
                : Path.GetFileName(theme.BackgroundImagePath);

            bool matched = false;
            foreach (object child in AccentPanel.Children)
            {
                RadioButton swatch = child as RadioButton;
                if (swatch == null) continue;
                string value = swatch.Tag as string;
                bool isCurrent = string.Equals(value, theme.AccentColor, StringComparison.OrdinalIgnoreCase);
                swatch.IsChecked = isCurrent;
                if (isCurrent) matched = true;
            }
            if (!matched)
            {
                foreach (object child in AccentPanel.Children)
                {
                    RadioButton swatch = child as RadioButton;
                    if (swatch != null) swatch.IsChecked = false;
                }
            }

            UpdateLabels();
        }

        private void UpdateLabels()
        {
            CornerValueText.Text = ((int)Math.Round(CornerSlider.Value)) + " px";
            OpacityValueText.Text = ((int)Math.Round(OpacitySlider.Value * 100)) + "%";
            BgImageOpacityText.Text = ((int)Math.Round(BgImageOpacitySlider.Value * 100)) + "%";
        }

        private void ApplyDraft()
        {
            ThemeManager.Apply(Theme);
            if (App.MainWindowInstance != null) App.MainWindowInstance.ApplyWindowBackground();
        }

        private void DarkSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            Theme.IsDark = DarkSwitch.IsChecked == true;
            Theme.BackgroundColor = Theme.IsDark ? "#10141C" : "#F4F6FB";
            BackgroundHexBox.Text = Theme.BackgroundColor;
            ApplyDraft();
        }

        private void AccentSwatch_Checked(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            RadioButton swatch = sender as RadioButton;
            if (swatch == null) return;
            Theme.AccentColor = swatch.Tag as string;
            AccentHexBox.Text = Theme.AccentColor;
            ApplyDraft();
        }

        private void ApplyAccentHex_Click(object sender, RoutedEventArgs e)
        {
            string normalized;
            if (!TryNormalizeHex(AccentHexBox.Text, out normalized))
            {
                App.ShowToast("颜色格式不正确，请使用 #RRGGBB", false);
                return;
            }
            Theme.AccentColor = normalized;
            AccentHexBox.Text = normalized;
            ApplyDraft();
            foreach (object child in AccentPanel.Children)
            {
                RadioButton swatch = child as RadioButton;
                if (swatch != null)
                    swatch.IsChecked = string.Equals(swatch.Tag as string, normalized, StringComparison.OrdinalIgnoreCase);
            }
            App.ShowToast("强调色已应用，点击保存主题可持久化", true);
        }

        private void ApplyBackgroundHex_Click(object sender, RoutedEventArgs e)
        {
            string normalized;
            if (!TryNormalizeHex(BackgroundHexBox.Text, out normalized))
            {
                App.ShowToast("颜色格式不正确，请使用 #RRGGBB", false);
                return;
            }
            Theme.BackgroundColor = normalized;
            BackgroundHexBox.Text = normalized;
            ApplyDraft();
        }

        private void ChooseBackground_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择背景图片";
            dialog.Filter = "图片文件 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                string ext = Path.GetExtension(dialog.FileName);
                string fileName = "bg_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" +
                    Guid.NewGuid().ToString("N").Substring(0, 6) + ext;
                string target = Path.Combine(StoragePaths.AssetsDirectory, fileName);
                File.Copy(dialog.FileName, target, true);
                Theme.BackgroundImagePath = target;
                BackgroundFileText.Text = Path.GetFileName(target);
                ApplyDraft();
                App.ShowToast("背景图已应用", true);
            }
            catch (Exception ex)
            {
                App.ShowToast("设置背景图失败：" + ex.Message, false);
            }
        }

        private void ClearBackground_Click(object sender, RoutedEventArgs e)
        {
            Theme.BackgroundImagePath = "";
            BackgroundFileText.Text = "未选择背景图";
            ApplyDraft();
        }

        private void BgImageOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || Theme == null) return;
            Theme.BackgroundImageOpacity = BgImageOpacitySlider.Value;
            BgImageOpacityText.Text = ((int)Math.Round(BgImageOpacitySlider.Value * 100)) + "%";
            ApplyDraft();
        }

        private void CornerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || Theme == null) return;
            Theme.CornerRadius = CornerSlider.Value;
            CornerValueText.Text = ((int)Math.Round(CornerSlider.Value)) + " px";
            ApplyDraft();
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading || Theme == null) return;
            Theme.CardOpacity = OpacitySlider.Value;
            OpacityValueText.Text = ((int)Math.Round(OpacitySlider.Value * 100)) + "%";
            ApplyDraft();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            App.State.SaveTheme(Theme, true);
            if (App.MainWindowInstance != null) App.MainWindowInstance.ApplyWindowBackground();
            App.ShowToast("主题已保存", true);
        }

        private void ExportTheme_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "导出主题配置";
            dialog.Filter = "JSON 文件 (*.json)|*.json";
            dialog.FileName = "净域主题_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".json";
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                DataService.ExportTheme(dialog.FileName, Theme);
                App.ShowToast("主题已导出", true);
            }
            catch (Exception ex)
            {
                App.ShowToast("导出主题失败：" + ex.Message, false);
            }
        }

        private void ImportTheme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "导入主题配置";
            dialog.Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try
            {
                ThemeSettings imported = DataService.ImportTheme(dialog.FileName);
                App.State.SaveTheme(imported, true);
                _loading = true;
                LoadControls();
                _loading = false;
                if (App.MainWindowInstance != null) App.MainWindowInstance.ApplyWindowBackground();
                App.ShowToast("主题已导入并应用", true);
            }
            catch (Exception ex)
            {
                App.ShowToast("导入主题失败：" + ex.Message, false);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = ConfirmDialog.Show(Window.GetWindow(this), "恢复默认主题",
                "将强调色、背景、圆角和透明度恢复为默认值，确定继续吗？",
                "恢复默认", "取消", false);
            if (!confirmed) return;
            App.State.SaveTheme(ThemeSettings.CreateDefault(), true);
            _loading = true;
            LoadControls();
            _loading = false;
            if (App.MainWindowInstance != null) App.MainWindowInstance.ApplyWindowBackground();
            App.ShowToast("已恢复默认主题", true);
        }

        private static bool TryNormalizeHex(string input, out string normalized)
        {
            normalized = "";
            if (string.IsNullOrWhiteSpace(input)) return false;
            string value = input.Trim();
            if (!value.StartsWith("#", StringComparison.Ordinal)) value = "#" + value;
            try
            {
                object converted = ColorConverter.ConvertFromString(value);
                if (!(converted is Color)) return false;
                Color color = (Color)converted;
                normalized = ColorUtil.ToHex(color);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
