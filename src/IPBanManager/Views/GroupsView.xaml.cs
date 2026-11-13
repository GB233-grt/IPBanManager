using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using IPBanManager.Models;
using IPBanManager.Services;
using IPBanManager.ViewModels;

namespace IPBanManager.Views
{
    public partial class GroupsView : UserControl
    {
        private readonly ObservableCollection<GroupCardItem> _items = new ObservableCollection<GroupCardItem>();

        public GroupsView()
        {
            InitializeComponent();
            DataContext = App.State;
            GroupItems.ItemsSource = _items;
            RebuildCards();
            if (App.State != null) App.State.DataChanged += State_DataChanged;
        }

        private Window OwnerWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void State_DataChanged(object sender, EventArgs e)
        {
            RebuildCards();
        }

        private void RebuildCards()
        {
            if (App.State == null) return;
            _items.Clear();
            foreach (BanGroup group in App.State.Groups)
            {
                int total = 0;
                int enabled = 0;
                foreach (BanRule rule in App.State.Rules)
                {
                    if (!string.Equals(rule.GroupId, group.Id, StringComparison.OrdinalIgnoreCase)) continue;
                    total++;
                    if (rule.Enabled && group.Enabled) enabled++;
                }
                _items.Add(GroupCardItem.From(group, total, enabled));
            }
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptDialog.Show(OwnerWindow, "新建分组", "分组名称", "",
                "例如：工作、新闻、社交");
            if (string.IsNullOrWhiteSpace(name)) return;
            string[] palette = new[] { "#6C8CFF", "#FF9A76", "#FF7D9C", "#B38CFF", "#58B7A5", "#F2B84B", "#5BA8D6" };
            string color = palette[App.State.Groups.Count % palette.Length];
            BanGroup group = App.State.AddGroup(name, color);
            if (group == null)
            {
                App.ShowToast("已存在同名分组，或名称无效", false);
                return;
            }
            App.ShowToast("分组“" + group.Name + "”已创建", true);
        }

        private void RenameGroup_Click(object sender, RoutedEventArgs e)
        {
            GroupCardItem item = GetItem(sender);
            if (item == null) return;
            string name = PromptDialog.Show(OwnerWindow, "重命名分组", "分组名称", item.Name, "");
            if (string.IsNullOrWhiteSpace(name) || name == item.Name) return;
            string error = App.State.RenameGroup(item.Group, name, item.Color);
            if (error != null)
            {
                App.ShowToast(error, false);
                return;
            }
            App.ShowToast("分组已重命名为“" + name + "”", true);
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            GroupCardItem item = GetItem(sender);
            if (item == null || item.IsSystem) return;
            bool confirmed = ConfirmDialog.Show(OwnerWindow, "删除分组",
                "确定删除分组“" + item.Name + "”吗？组内 " + item.RuleCount +
                " 条规则会移动到“自定义”分组，不会被删除。",
                "删除分组", "取消", true);
            if (!confirmed) return;
            HostsApplyResult result = App.State.DeleteGroup(item.Group);
            if (!string.IsNullOrEmpty(App.State.LastOperationError))
            {
                App.ShowToast(App.State.LastOperationError, false);
                return;
            }
            App.ReportHostsResult(OwnerWindow, result);
        }

        private void GroupSwitch_Click(object sender, RoutedEventArgs e)
        {
            ToggleButton toggle = sender as ToggleButton;
            if (toggle == null) return;
            GroupCardItem item = toggle.DataContext as GroupCardItem;
            if (item == null) return;
            HostsApplyResult result = App.State.SetGroupEnabled(item.Group, toggle.IsChecked == true);
            App.ReportHostsResult(OwnerWindow, result);
        }

        private void ViewRules_Click(object sender, RoutedEventArgs e)
        {
            GroupCardItem item = GetItem(sender);
            if (item == null) return;
            if (App.MainWindowInstance != null) App.MainWindowInstance.ShowRulesForGroup(item.Id);
        }

        private static GroupCardItem GetItem(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;
            return element == null ? null : element.DataContext as GroupCardItem;
        }
    }
}
