using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IPBanManager.Models;

namespace IPBanManager.Services
{
    public class UpsertResult
    {
        public UpsertResult()
        {
            Domains = new List<string>();
            Errors = new List<string>();
        }

        public int Added { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public List<string> Domains { get; set; }
        public List<string> Errors { get; set; }

        public string Summary
        {
            get
            {
                string text = "新增 " + Added + " 条，更新 " + Updated + " 条";
                if (Skipped > 0) text += "，跳过 " + Skipped + " 条";
                if (Errors.Count > 0) text += "，" + Errors.Count + " 条无效";
                return text;
            }
        }
    }

    /// <summary>
    /// 纯规则逻辑，便于单独测试：去重、更新而不是无限追加。
    /// </summary>
    public static class RuleService
    {
        public static UpsertResult Upsert(
            ObservableCollection<BanRule> rules,
            IEnumerable<string> domains,
            string ipMode,
            string customIp,
            string groupId,
            string remark,
            bool enabled)
        {
            UpsertResult result = new UpsertResult();
            if (domains == null) return result;
            if (rules == null) throw new ArgumentNullException("rules");

            string mode = string.Equals(ipMode, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "auto";
            if (mode == "custom")
            {
                string normalizedIp;
                string ipError;
                if (!DomainUtil.TryNormalizeIp(customIp, out normalizedIp, out ipError))
                {
                    result.Errors.Add(ipError);
                    result.Skipped++;
                    return result;
                }
                customIp = normalizedIp;
            }
            else
            {
                customIp = "";
            }

            foreach (string input in domains)
            {
                string domain;
                string error;
                if (!DomainUtil.TryNormalize(input, out domain, out error))
                {
                    result.Errors.Add(error);
                    result.Skipped++;
                    continue;
                }

                BanRule existing = FindByDomain(rules, domain);
                if (existing != null)
                {
                    existing.Domain = domain;
                    existing.IpMode = mode;
                    existing.CustomIp = customIp;
                    existing.GroupId = string.IsNullOrEmpty(groupId) ? "custom" : groupId;
                    if (!string.IsNullOrEmpty(remark)) existing.Remark = remark;
                    existing.Enabled = enabled;
                    existing.UpdatedAt = DateTime.Now;
                    if (!DomainUtil.ContainsIgnoreCase(result.Domains, domain)) result.Domains.Add(domain);
                    result.Updated++;
                }
                else
                {
                    BanRule rule = new BanRule
                    {
                        Domain = domain,
                        IpMode = mode,
                        CustomIp = customIp,
                        GroupId = string.IsNullOrEmpty(groupId) ? "custom" : groupId,
                        Remark = remark ?? "",
                        Enabled = enabled,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    rules.Add(rule);
                    if (!DomainUtil.ContainsIgnoreCase(result.Domains, domain)) result.Domains.Add(domain);
                    result.Added++;
                }
            }

            return result;
        }

        /// <summary>编辑单条规则。返回 null 表示成功，否则返回错误提示。</summary>
        public static string UpdateSingle(
            ObservableCollection<BanRule> rules,
            BanRule target,
            string domainInput,
            string ipMode,
            string customIp,
            string groupId,
            string remark,
            bool enabled)
        {
            if (target == null) return "规则不存在";
            if (rules == null) return "规则集合不可用";

            string domain;
            string error;
            if (!DomainUtil.TryNormalize(domainInput, out domain, out error)) return error;

            BanRule sameDomain = FindByDomain(rules, domain);
            if (sameDomain != null && !ReferenceEquals(sameDomain, target))
                return "域名 " + domain + " 已存在于其他规则中，请直接编辑那条规则";

            string mode = string.Equals(ipMode, "custom", StringComparison.OrdinalIgnoreCase) ? "custom" : "auto";
            if (mode == "custom")
            {
                string normalizedIp;
                string ipError;
                if (!DomainUtil.TryNormalizeIp(customIp, out normalizedIp, out ipError)) return ipError;
                customIp = normalizedIp;
            }
            else
            {
                customIp = "";
            }

            target.Domain = domain;
            target.IpMode = mode;
            target.CustomIp = customIp;
            target.GroupId = string.IsNullOrEmpty(groupId) ? "custom" : groupId;
            target.Remark = remark ?? "";
            target.Enabled = enabled;
            target.UpdatedAt = DateTime.Now;
            return null;
        }

        public static BanRule FindByDomain(IEnumerable<BanRule> rules, string domain)
        {
            if (rules == null) return null;
            foreach (BanRule rule in rules)
            {
                if (string.Equals(rule.Domain, domain, StringComparison.OrdinalIgnoreCase)) return rule;
            }
            return null;
        }

        public static int SetEnabled(IEnumerable<BanRule> rules, bool enabled)
        {
            int count = 0;
            if (rules == null) return 0;
            foreach (BanRule rule in rules)
            {
                rule.Enabled = enabled;
                rule.UpdatedAt = DateTime.Now;
                count++;
            }
            return count;
        }
    }
}
