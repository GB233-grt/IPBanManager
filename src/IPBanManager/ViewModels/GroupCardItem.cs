using System;
using IPBanManager.Models;

namespace IPBanManager.ViewModels
{
    /// <summary>分组页卡片的数据对象，避免在 XAML 里做复杂聚合。</summary>
    public class GroupCardItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public bool Enabled { get; set; }
        public bool IsSystem { get; set; }
        public int RuleCount { get; set; }
        public int EnabledRuleCount { get; set; }
        public string CountText { get; set; }
        public string StateText { get; set; }
        public string Initial { get; set; }
        public BanGroup Group { get; set; }

        public static GroupCardItem From(BanGroup group, int total, int enabled)
        {
            string initial = "分";
            if (!string.IsNullOrEmpty(group.Name)) initial = group.Name.Substring(0, 1);
            return new GroupCardItem
            {
                Id = group.Id,
                Name = group.Name,
                Color = group.Color,
                Enabled = group.Enabled,
                IsSystem = group.IsSystem,
                RuleCount = total,
                EnabledRuleCount = enabled,
                CountText = total + " 条规则 · " + enabled + " 条生效",
                StateText = group.Enabled ? "已启用" : "已暂停",
                Initial = initial,
                Group = group
            };
        }
    }
}
