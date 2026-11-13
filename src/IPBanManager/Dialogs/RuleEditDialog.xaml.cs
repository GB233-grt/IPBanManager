using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IPBanManager.Models;
using IPBanManager.Services;

namespace IPBanManager
{
    public partial class RuleEditDialog : Window
    {
        private readonly BanRule _rule;
        private readonly IList<BanGroup> _groups;
        private readonly AppSettings _settings;
        private readonly bool _editMode;

        public RuleEditResult Result { get; private set; }

        private RuleEditDialog(BanRule rule, IList<BanGroup> groups, string defaultGroupId, AppSettings settings)
        {
            InitializeComponent();
            _rule = rule;
            _groups = groups ?? new List<BanGroup>();
            _settings = settings ?? AppSettings.CreateDefault();
            _editMode = rule != null;
            Result = new RuleEditResult();

            GroupCombo.ItemsSource = _groups;
            ExpandCheck.IsChecked = _settings.AutoExpandSubdomains;

            if (_editMode)
            {
                TitleText.Text = "编辑封禁规则";
                DomainLabel.Text = "域名（编辑时只能修改为单个域名）";
                DomainBox.Text = rule.Domain;
                DomainBox.AcceptsReturn = false;
                DomainBox.TextWrapping = TextWrapping.NoWrap;
                DomainBox.Height = 40;
                DomainBox.VerticalContentAlignment = VerticalAlignment.Center;
                DomainHint.Text = "如需批量添加或修改多个域名，请使用“添加封禁规则”。";
                ExpandCheck.Visibility = Visibility.Collapsed;
                ExpandHint.Visibility = Visibility.Collapsed;
                IpModeCombo.SelectedIndex = string.Equals(rule.IpMode, "custom", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                CustomIpBox.Text = string.IsNullOrEmpty(rule.CustomIp) ? "127.0.0.1" : rule.CustomIp;
                RemarkBox.Text = rule.Remark;
                EnabledCheck.Content = "启用此规则";
                EnabledCheck.IsChecked = rule.Enabled;
                GroupCombo.SelectedValue = rule.GroupId;
            }
            else
            {
                IpModeCombo.SelectedIndex = 0;
                EnabledCheck.IsChecked = true;
                GroupCombo.SelectedValue = string.IsNullOrEmpty(defaultGroupId) ? "custom" : defaultGroupId;
                if (GroupCombo.SelectedValue == null) GroupCombo.SelectedValue = "custom";
                DomainBox.Focus();
            }

            UpdateCustomIpVisibility();
            UpdatePreview();
        }

        public static RuleEditResult Show(Window owner, BanRule rule, IList<BanGroup> groups,
            string defaultGroupId, AppSettings settings)
        {
            RuleEditDialog dialog = new RuleEditDialog(rule, groups, defaultGroupId, settings);
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

        private void DomainBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
            HideError();
        }

        private void ExpandCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void IpMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateCustomIpVisibility();
            UpdatePreview();
        }

        private void CustomIpBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
            HideError();
        }

        private void RemarkBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdateCustomIpVisibility()
        {
            if (CustomIpPanel == null) return;
            CustomIpPanel.Visibility = IpModeCombo.SelectedIndex == 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdatePreview()
        {
            if (PreviewText == null) return;
            string firstDomain = GetFirstDomain();
            if (string.IsNullOrEmpty(firstDomain))
            {
                PreviewText.Text = "输入域名后显示将要写入 HOSTS 的内容";
                return;
            }

            List<string> lines = new List<string>();
            List<string> domains = new List<string>();
            if (_editMode)
            {
                domains.Add(firstDomain);
            }
            else
            {
                List<string> errors;
                List<string> parsed = DomainUtil.ParseBatchText(DomainBox.Text, out errors);
                domains.AddRange(parsed);
                if (ExpandCheck.IsChecked == true)
                {
                    domains = DomainUtil.ExpandDomains(domains, _settings.ExpansionPrefixes, true);
                }
            }

            string sample = domains.Count > 0 ? domains[0] : firstDomain;
            if (IpModeCombo.SelectedIndex == 1)
            {
                string ip;
                string ipError;
                if (!DomainUtil.TryNormalizeIp(CustomIpBox.Text, out ip, out ipError)) ip = "127.0.0.1";
                lines.Add(ip + " " + sample);
            }
            else
            {
                lines.Add(Render(_settings.BlockTemplate, "0.0.0.0", sample));
                if (!string.Equals(lines[0], Render(_settings.RedirectTemplate, "127.0.0.1", sample),
                    StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(Render(_settings.RedirectTemplate, "127.0.0.1", sample));
                }
            }

            string head = string.Join(Environment.NewLine, lines.ToArray());
            if (!_editMode && domains.Count > 1)
            {
                head += Environment.NewLine + "…… 共 " + domains.Count + " 个域名";
            }
            PreviewText.Text = head;
        }

        private string GetFirstDomain()
        {
            if (string.IsNullOrWhiteSpace(DomainBox.Text)) return "";
            string[] lines = DomainBox.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i].Trim();
                if (text.Length == 0) continue;
                string domain;
                string error;
                if (DomainUtil.TryNormalize(text, out domain, out error)) return domain;
            }
            return "";
        }

        private static string Render(string template, string ip, string domain)
        {
            string text = string.IsNullOrWhiteSpace(template) ? ip + " {domain}" : template;
            text = text.Replace("{domain}", domain).Replace("{ip}", ip);
            if (text.IndexOf(domain, StringComparison.OrdinalIgnoreCase) < 0) text += " " + domain;
            return text;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            List<string> errors;
            List<string> parsed = DomainUtil.ParseBatchText(DomainBox.Text, out errors);
            if (parsed.Count == 0)
            {
                ShowError(errors.Count > 0 ? errors[0] : "请输入至少一个有效域名");
                return;
            }
            if (_editMode && parsed.Count > 1)
            {
                ShowError("编辑模式只能保留一个域名；如需批量请使用“添加封禁规则”。");
                return;
            }

            string mode = IpModeCombo.SelectedIndex == 1 ? "custom" : "auto";
            string customIp = "";
            if (mode == "custom")
            {
                string ipError;
                if (!DomainUtil.TryNormalizeIp(CustomIpBox.Text, out customIp, out ipError))
                {
                    ShowError(ipError);
                    CustomIpBox.Focus();
                    return;
                }
            }

            string groupId = GroupCombo.SelectedValue as string;
            if (string.IsNullOrEmpty(groupId))
            {
                BanGroup selected = GroupCombo.SelectedItem as BanGroup;
                groupId = selected == null ? "custom" : selected.Id;
            }

            List<string> finalDomains = parsed;
            bool expand = !_editMode && ExpandCheck.IsChecked == true;
            if (expand)
                finalDomains = DomainUtil.ExpandDomains(parsed, _settings.ExpansionPrefixes, true);

            Result = new RuleEditResult
            {
                Domains = finalDomains,
                IpMode = mode,
                CustomIp = customIp,
                GroupId = groupId,
                Remark = RemarkBox.Text.Trim(),
                ExpandSubdomains = expand,
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
