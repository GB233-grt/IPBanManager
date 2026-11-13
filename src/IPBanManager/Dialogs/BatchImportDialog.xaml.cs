using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPBanManager.Models;
using IPBanManager.Services;
using Microsoft.Win32;

namespace IPBanManager
{
    public partial class BatchImportDialog : Window
    {
        private readonly IList<BanGroup> _groups;
        private readonly AppSettings _settings;

        public BatchImportResult Result { get; private set; }

        private BatchImportDialog(IList<BanGroup> groups, string defaultGroupId, AppSettings settings)
        {
            InitializeComponent();
            _groups = groups ?? new List<BanGroup>();
            _settings = settings ?? AppSettings.CreateDefault();
            Result = new BatchImportResult();
            GroupCombo.ItemsSource = _groups;
            GroupCombo.SelectedValue = string.IsNullOrEmpty(defaultGroupId) ? "custom" : defaultGroupId;
            if (GroupCombo.SelectedValue == null) GroupCombo.SelectedValue = "custom";
            ExpandCheck.IsChecked = _settings.AutoExpandSubdomains;
            UpdateStats();
        }

        public static BatchImportResult Show(Window owner, IList<BanGroup> groups,
            string defaultGroupId, AppSettings settings)
        {
            BatchImportDialog dialog = new BatchImportDialog(groups, defaultGroupId, settings);
            dialog.Owner = owner != null && owner.IsVisible ? owner : null;
            dialog.WindowStartupLocation = dialog.Owner == null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner;
            bool? result = dialog.ShowDialog();
            return result == true ? dialog.Result : null;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStats();
            HideError();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (StatsText == null) return;
            List<string> errors;
            List<string> domains = DomainUtil.ParseBatchText(InputBox.Text, out errors);
            int finalCount = domains.Count;
            if (ExpandCheck.IsChecked == true)
                finalCount = DomainUtil.ExpandDomains(domains, _settings.ExpansionPrefixes, true).Count;

            if (domains.Count == 0 && errors.Count == 0)
            {
                StatsText.Text = "等待输入";
                StatsText.Foreground = (System.Windows.Media.Brush)FindResource("SubTextBrush");
            }
            else
            {
                StatsText.Text = "识别 " + domains.Count + " 个域名" +
                    (ExpandCheck.IsChecked == true ? " → 扩展后 " + finalCount + " 条" : "") +
                    (errors.Count > 0 ? " · " + errors.Count + " 个错误" : "");
                StatsText.Foreground = (System.Windows.Media.Brush)FindResource(
                    errors.Count > 0 ? "WarningBrush" : "SuccessBrush");
            }

            if (errors.Count > 0)
            {
                ErrorText.Text = string.Join(Environment.NewLine, errors.GetRange(0, Math.Min(4, errors.Count)).ToArray());
                ErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "选择域名列表文件";
            dialog.Filter = "文本文件 (*.txt;*.csv;*.list)|*.txt;*.csv;*.list|所有文件 (*.*)|*.*";
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                string text = JsonHelper.ReadTextAuto(dialog.FileName);
                InputBox.Text = text;
                UpdateStats();
            }
            catch (Exception ex)
            {
                ShowError("读取文件失败：" + ex.Message);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            InputBox.Text = "";
            UpdateStats();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            List<string> errors;
            List<string> domains = DomainUtil.ParseBatchText(InputBox.Text, out errors);
            if (domains.Count == 0)
            {
                ShowError(errors.Count > 0 ? errors[0] : "请先粘贴或读取域名列表");
                return;
            }
            if (domains.Count > 2000)
            {
                ShowError("一次最多导入 2000 条规则，请分批导入。");
                return;
            }

            string groupId = GroupCombo.SelectedValue as string;
            if (string.IsNullOrEmpty(groupId))
            {
                BanGroup group = GroupCombo.SelectedItem as BanGroup;
                groupId = group == null ? "custom" : group.Id;
            }

            Result = new BatchImportResult
            {
                Lines = domains,
                GroupId = groupId,
                ExpandSubdomains = ExpandCheck.IsChecked == true,
                Enabled = EnabledCheck.IsChecked == true
            };
            DialogResult = true;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            if (ErrorText != null) ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape) DialogResult = false;
            base.OnKeyDown(e);
        }
    }
}
