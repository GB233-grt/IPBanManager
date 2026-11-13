using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using IPBanManager.Models;
using IPBanManager.Services;
using Microsoft.Win32;

namespace IPBanManager.Views
{
    public partial class RulesView : UserControl
    {
        private ICollectionView _view;
        private ListCollectionView _listView;
        private RuleComparer _comparer;
        private bool _initialized;
        private bool _suppressFilter;

        public RulesView()
        {
            InitializeComponent();
            DataContext = App.State;
            InitializeList();
            PopulateGroupFilter(null);
            _initialized = true;
            UpdateCounts();
            if (App.State != null) App.State.DataChanged += State_DataChanged;
        }

        private Window OwnerWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void InitializeList()
        {
            if (App.State == null) return;
            _view = CollectionViewSource.GetDefaultView(App.State.Rules);
            _comparer = new RuleComparer("newest", App.State);
            _listView = _view as ListCollectionView;
            if (_listView != null) _listView.CustomSort = _comparer;
            _view.Filter = FilterRule;
            RulesGrid.ItemsSource = _view;
        }

        private void State_DataChanged(object sender, EventArgs e)
        {
            if (!_initialized) return;
            string selectedGroup = GetSelectedTag(GroupFilter);
            PopulateGroupFilter(selectedGroup);
            if (_view != null) _view.Refresh();
            UpdateCounts();
        }

        private void PopulateGroupFilter(string selectedGroupId)
        {
            if (GroupFilter == null || App.State == null) return;
            _suppressFilter = true;
            GroupFilter.Items.Clear();
            ComboBoxItem all = new ComboBoxItem();
            all.Content = "全部分组";
            all.Tag = "";
            GroupFilter.Items.Add(all);

            foreach (BanGroup group in App.State.Groups)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = group.Name + (group.Enabled ? "" : "（暂停）");
                item.Tag = group.Id;
                GroupFilter.Items.Add(item);
            }

            int index = 0;
            if (!string.IsNullOrEmpty(selectedGroupId))
            {
                for (int i = 1; i < GroupFilter.Items.Count; i++)
                {
                    ComboBoxItem item = GroupFilter.Items[i] as ComboBoxItem;
                    if (item != null && string.Equals(item.Tag as string, selectedGroupId, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i;
                        break;
                    }
                }
            }
            GroupFilter.SelectedIndex = index;
            _suppressFilter = false;
        }

        private bool FilterRule(object item)
        {
            BanRule rule = item as BanRule;
            if (rule == null || App.State == null) return false;

            string query = SearchBox == null ? "" : (SearchBox.Text ?? "").Trim();
            if (query.Length > 0)
            {
                string groupName = App.State.GetGroupName(rule.GroupId);
                string ip = rule.IpMode == "custom" ? rule.CustomIp : "0.0.0.0 127.0.0.1";
                bool hit = (rule.Domain ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (rule.Remark ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (groupName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (ip ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!hit) return false;
            }

            string groupTag = GetSelectedTag(GroupFilter);
            if (!string.IsNullOrEmpty(groupTag) &&
                !string.Equals(rule.GroupId, groupTag, StringComparison.OrdinalIgnoreCase)) return false;

            string status = GetSelectedTag(StatusFilter);
            bool effective = App.State.GetEffectiveEnabled(rule);
            if (status == "enabled" && !effective) return false;
            if (status == "disabled" && effective) return false;

            return true;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || _suppressFilter) return;
            string sortMode = GetSelectedTag(SortFilter);
            if (string.IsNullOrEmpty(sortMode)) sortMode = "newest";
            if (_comparer == null || _comparer.Mode != sortMode)
            {
                _comparer = new RuleComparer(sortMode, App.State);
                if (_listView != null) _listView.CustomSort = _comparer;
            }
            if (_view != null) _view.Refresh();
            UpdateCounts();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            if (!_initialized) return;
            if (_view != null) _view.Refresh();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            if (FilteredText == null || _view == null) return;
            int count = _view.Cast<BanRule>().Count();
            FilteredText.Text = "共 " + count + " 条规则";
        }

        public void ShowAddDialog()
        {
            if (App.State == null) return;
            string group = GetSelectedTag(GroupFilter);
            if (string.IsNullOrEmpty(group)) group = "custom";
            RuleEditResult result = RuleEditDialog.Show(OwnerWindow, null, App.State.Groups,
                group, App.State.Settings);
            if (result == null) return;

            MutationResult mutation = App.State.AddRules(result.Domains, result.IpMode,
                result.CustomIp, result.GroupId, result.Remark, result.Enabled);
            if (!mutation.Success)
            {
                App.ShowToast(mutation.Error, false);
                return;
            }

            if (mutation.Hosts != null && mutation.Hosts.RequiresElevation)
            {
                App.ReportHostsResult(OwnerWindow, mutation.Hosts);
                return;
            }
            string message = mutation.Upsert.Summary;
            bool success = true;
            if (mutation.Hosts != null)
            {
                message += " · " + mutation.Hosts.Message;
                success = mutation.Hosts.Success;
            }
            else
            {
                message += " · 已保存，等待手动应用 HOSTS";
            }
            App.ShowToast(message, success);
            if (mutation.Hosts != null && mutation.Hosts.Conflicts != null && mutation.Hosts.Conflicts.Count > 0)
            {
                App.ShowToast("注意：HOSTS 块外存在同域名记录：" +
                    string.Join("、", mutation.Hosts.Conflicts.ToArray()), false);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            ShowAddDialog();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null) return;
            string group = GetSelectedTag(GroupFilter);
            if (string.IsNullOrEmpty(group)) group = "custom";
            BatchImportResult result = BatchImportDialog.Show(OwnerWindow, App.State.Groups,
                group, App.State.Settings);
            if (result == null) return;

            List<string> domains = result.Lines;
            if (result.ExpandSubdomains)
                domains = DomainUtil.ExpandDomains(domains, App.State.Settings.ExpansionPrefixes, true);

            MutationResult mutation = App.State.AddRules(domains, "auto", "", result.GroupId, "", result.Enabled);
            if (!mutation.Success)
            {
                App.ShowToast(mutation.Error, false);
                return;
            }
            string message = "导入完成：" + mutation.Upsert.Summary;
            bool success = true;
            if (mutation.Hosts != null)
            {
                message += " · " + mutation.Hosts.Message;
                success = mutation.Hosts.Success;
            }
            else
            {
                message += " · 等待手动应用";
            }
            App.ShowToast(message, success);
        }

        private void ApplyHosts_Click(object sender, RoutedEventArgs e)
        {
            if (App.State == null) return;
            App.ReportHostsResult(OwnerWindow, App.State.ApplyHosts());
        }

        private void EditRule_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null) return;
            BanRule rule = element.DataContext as BanRule;
            EditRule(rule);
        }

        private void RulesGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (RulesGrid.SelectedItem is BanRule) EditRule((BanRule)RulesGrid.SelectedItem);
        }

        private void EditRule(BanRule rule)
        {
            if (rule == null || App.State == null) return;
            RuleEditResult result = RuleEditDialog.Show(OwnerWindow, rule, App.State.Groups,
                rule.GroupId, App.State.Settings);
            if (result == null) return;
            if (result.Domains == null || result.Domains.Count == 0) return;

            RuleUpdateResult update = new RuleUpdateResult
            {
                Domain = result.Domains[0],
                IpMode = result.IpMode,
                CustomIp = result.CustomIp,
                GroupId = result.GroupId,
                Remark = result.Remark,
                Enabled = result.Enabled
            };

            App.State.ClearLastOperationError();
            HostsApplyResult hosts = App.State.EditRule(rule, update);
            if (!string.IsNullOrEmpty(App.State.LastOperationError))
            {
                App.ShowToast(App.State.LastOperationError, false);
                return;
            }
            if (hosts != null)
            {
                App.ReportHostsResult(OwnerWindow, hosts);
            }
            else
            {
                App.ShowToast("规则已更新，等待手动应用 HOSTS", true);
            }
        }

        private void DeleteRule_Click(object sender, RoutedEventArgs e)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null) return;
            BanRule rule = element.DataContext as BanRule;
            if (rule == null) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "删除规则",
                "确定删除域名 “" + rule.Domain + "” 的封禁规则吗？删除后会自动从 HOSTS 托管区域移除。",
                "删除", "取消", true);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.DeleteRules(new[] { rule }));
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (RulesGrid.SelectedItems.Count == 0)
            {
                App.ShowToast("请先在表格中选择要删除的规则", false);
                return;
            }
            List<BanRule> selected = RulesGrid.SelectedItems.Cast<BanRule>().ToList();
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "批量删除",
                "确定删除选中的 " + selected.Count + " 条规则吗？操作会立即更新 HOSTS。",
                "删除 " + selected.Count + " 条", "取消", true);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.DeleteRules(selected));
        }

        private void RuleEnabled_Click(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            if (box == null) return;
            BanRule rule = box.DataContext as BanRule;
            if (rule == null) return;
            HostsApplyResult result = App.State.SetRuleEnabled(rule, box.IsChecked == true);
            App.ReportHostsResult(OwnerWindow, result);
        }

        private List<BanRule> VisibleRules()
        {
            if (_view == null) return new List<BanRule>();
            return _view.Cast<BanRule>().ToList();
        }

        private void EnableVisible_Click(object sender, RoutedEventArgs e)
        {
            List<BanRule> rules = VisibleRules();
            if (rules.Count == 0) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "启用筛选结果",
                "将启用当前筛选出的 " + rules.Count + " 条规则（不会改变分组暂停状态）。",
                "全部启用", "取消", false);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.SetAllRulesEnabled(rules, true));
        }

        private void DisableVisible_Click(object sender, RoutedEventArgs e)
        {
            List<BanRule> rules = VisibleRules();
            if (rules.Count == 0) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "禁用筛选结果",
                "将禁用当前筛选出的 " + rules.Count + " 条规则，HOSTS 中对应条目会被注释。",
                "全部禁用", "取消", true);
            if (!confirmed) return;
            App.ReportHostsResult(OwnerWindow, App.State.SetAllRulesEnabled(rules, false));
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            List<BanRule> rules = VisibleRules();
            if (rules.Count == 0)
            {
                App.ShowToast("当前没有可导出的规则", false);
                return;
            }
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "导出规则";
            dialog.Filter = "文本文件 (*.txt)|*.txt|CSV 文件 (*.csv)|*.csv";
            dialog.FileName = "净域规则_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".txt";
            if (dialog.ShowDialog(OwnerWindow) != true) return;

            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# 净域规则导出 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.AppendLine("# 域名\t分组\t写入方式\t状态\t备注");
                foreach (BanRule rule in rules)
                {
                    builder.Append(rule.Domain).Append('\t');
                    builder.Append(App.State.GetGroupName(rule.GroupId)).Append('\t');
                    builder.Append(rule.IpMode == "custom" ? rule.CustomIp : "自动").Append('\t');
                    builder.Append(rule.Enabled ? "启用" : "禁用").Append('\t');
                    builder.Append(rule.Remark ?? "").AppendLine();
                }
                File.WriteAllText(dialog.FileName, builder.ToString(), new UTF8Encoding(true));
                App.ShowToast("已导出 " + rules.Count + " 条规则", true);
            }
            catch (Exception ex)
            {
                App.ShowToast("导出失败：" + ex.Message, false);
            }
        }

        public void SetGroupFilter(string groupId)
        {
            if (GroupFilter == null) return;
            PopulateGroupFilter(groupId);
            if (_view != null) _view.Refresh();
            UpdateCounts();
        }

        private static string GetSelectedTag(ComboBox combo)
        {
            if (combo == null) return "";
            ComboBoxItem item = combo.SelectedItem as ComboBoxItem;
            return item == null ? "" : (item.Tag as string ?? "");
        }

        private class RuleComparer : IComparer
        {
            public RuleComparer(string mode, AppState state)
            {
                Mode = mode ?? "newest";
                _state = state;
            }

            public string Mode { get; private set; }
            private readonly AppState _state;

            public int Compare(object x, object y)
            {
                BanRule a = x as BanRule;
                BanRule b = y as BanRule;
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;

                if (Mode == "newest")
                    return b.CreatedAt.CompareTo(a.CreatedAt);
                if (Mode == "group")
                {
                    int ga = GroupOrder(a);
                    int gb = GroupOrder(b);
                    if (ga != gb) return ga.CompareTo(gb);
                    return string.Compare(a.Domain, b.Domain, StringComparison.OrdinalIgnoreCase);
                }
                if (Mode == "status")
                {
                    int ea = _state != null && _state.GetEffectiveEnabled(a) ? 0 : 1;
                    int eb = _state != null && _state.GetEffectiveEnabled(b) ? 0 : 1;
                    if (ea != eb) return ea.CompareTo(eb);
                    return string.Compare(a.Domain, b.Domain, StringComparison.OrdinalIgnoreCase);
                }
                return string.Compare(a.Domain, b.Domain, StringComparison.OrdinalIgnoreCase);
            }

            private int GroupOrder(BanRule rule)
            {
                if (_state == null) return 0;
                BanGroup group = _state.GetGroup(rule.GroupId);
                return group == null ? 999 : group.SortOrder;
            }
        }
    }
}
