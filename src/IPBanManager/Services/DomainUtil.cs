using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace IPBanManager.Services
{
    /// <summary>
    /// 域名输入清洗与常见子域扩展。
    /// HOSTS 只能做“域名 → IP”的映射，因此这里拒绝直接输入 IP 或通配符。
    /// </summary>
    public static class DomainUtil
    {
        private static readonly Regex LabelRegex = new Regex(
            "^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly IdnMapping Idn = new IdnMapping();

        /// <summary>把域名、URL、带端口/路径的网址统一转换为小写 ASCII 域名。</summary>
        public static bool TryNormalize(string input, out string domain, out string error)
        {
            domain = "";
            error = "";

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "请输入域名或网址";
                return false;
            }

            string raw = input.Trim().Trim('"', '\'', '\uFEFF');
            raw = raw.Replace("\u200B", "").Trim();
            if (raw.Length == 0)
            {
                error = "请输入域名或网址";
                return false;
            }

            if (raw.StartsWith("*.", StringComparison.Ordinal))
            {
                error = "HOSTS 不支持通配符，请填写具体域名（可勾选“同时封禁常见子域”）";
                return false;
            }

            string candidate = raw;
            if (!candidate.Contains("://"))
            {
                if (candidate.StartsWith("//", StringComparison.Ordinal))
                    candidate = "http:" + candidate;
                else
                    candidate = "http://" + candidate;
            }

            string host = "";
            Uri uri;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out uri) && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
            }
            else
            {
                // 某些中文域名或未带协议的输入可能让 Uri 解析失败，退化为手工去路径。
                host = raw;
                int slash = host.IndexOf('/');
                if (slash >= 0) host = host.Substring(0, slash);
                int colon = host.LastIndexOf(':');
                if (colon > 0 && host.IndexOf(':') == colon) host = host.Substring(0, colon);
                host = host.Trim().TrimEnd('.');
            }

            host = host.Trim().TrimEnd('.');
            if (host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal))
                host = host.Substring(1, host.Length - 2);

            if (host.Length == 0)
            {
                error = "无法识别域名";
                return false;
            }

            IPAddress address;
            if (IPAddress.TryParse(host, out address))
            {
                error = "HOSTS 只能屏蔽域名，不能直接按 IP 屏蔽。请填写域名或网址";
                return false;
            }

            try
            {
                host = Idn.GetAscii(host).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                error = "域名格式不正确：" + host;
                return false;
            }

            if (host.Length > 253)
            {
                error = "域名过长（超过 253 个字符）";
                return false;
            }

            if (host.IndexOf('.') <= 0)
            {
                error = "域名格式不正确，需要包含点，例如 example.com";
                return false;
            }

            string[] labels = host.Split('.');
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].Length == 0 || !LabelRegex.IsMatch(labels[i]))
                {
                    error = "域名格式不正确：" + raw;
                    return false;
                }
            }

            domain = host;
            return true;
        }

        public static bool TryNormalizeIp(string input, out string ip, out string error)
        {
            ip = "";
            error = "";
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "请输入自定义 IP";
                return false;
            }

            string value = input.Trim();
            if (value.IndexOf('.') < 0 && value.IndexOf(':') < 0)
            {
                error = "IP 格式不正确";
                return false;
            }

            IPAddress address;
            if (!IPAddress.TryParse(value, out address))
            {
                error = "IP 格式不正确，例如 0.0.0.0 或 127.0.0.1";
                return false;
            }

            ip = address.ToString();
            return true;
        }

        public static List<string> ParsePrefixes(string csv)
        {
            List<string> prefixes = new List<string>();
            if (string.IsNullOrWhiteSpace(csv)) return prefixes;

            string[] parts = csv.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string prefix = parts[i].Trim().Trim('.');
                if (prefix.Length == 0) continue;
                if (!LabelRegex.IsMatch(prefix.ToLowerInvariant())) continue;
                if (!ContainsIgnoreCase(prefixes, prefix)) prefixes.Add(prefix.ToLowerInvariant());
            }
            return prefixes;
        }

        /// <summary>输入 bilibili.com 且启用扩展时，返回 bilibili.com / www / m / api 等。</summary>
        public static List<string> ExpandDomains(IEnumerable<string> domains, string prefixCsv, bool expand)
        {
            List<string> result = new List<string>();
            if (domains == null) return result;

            List<string> prefixes = ParsePrefixes(prefixCsv);
            foreach (string item in domains)
            {
                string domain;
                string error;
                if (!TryNormalize(item, out domain, out error)) continue;
                if (!ContainsIgnoreCase(result, domain)) result.Add(domain);
                if (!expand) continue;

                string baseDomain = StripKnownPrefix(domain, prefixes);
                if (!ContainsIgnoreCase(result, baseDomain)) result.Add(baseDomain);

                for (int i = 0; i < prefixes.Count; i++)
                {
                    string candidate = prefixes[i] + "." + baseDomain;
                    if (!ContainsIgnoreCase(result, candidate)) result.Add(candidate);
                }
            }
            return result;
        }

        /// <summary>
        /// 从批量导入文本中解析域名：忽略空行和 # 注释，支持每行一个域名、逗号/分号/空白分隔、
        /// 以及 "0.0.0.0 example.com" 这类 HOSTS 行。
        /// </summary>
        public static List<string> ParseBatchText(string text, out List<string> errors)
        {
            List<string> domains = new List<string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return domains;

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                string[] tokens = line.Split(new[] { ' ', '\t', ',', ';' },
                    StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < tokens.Length; i++)
                {
                    string token = tokens[i].Trim();
                    if (token.Length == 0) continue;

                    IPAddress address;
                    if (IPAddress.TryParse(token, out address))
                        continue; // HOSTS 行里的目标 IP，不是要屏蔽的域名

                    string domain;
                    string error;
                    if (TryNormalize(token, out domain, out error))
                    {
                        if (!ContainsIgnoreCase(domains, domain)) domains.Add(domain);
                    }
                    else
                    {
                        errors.Add("第 " + (lineIndex + 1) + " 行：" + token + "（" + error + "）");
                    }
                }
            }
            return domains;
        }

        public static bool IsValidDomain(string domain)
        {
            string normalized;
            string error;
            return TryNormalize(domain, out normalized, out error);
        }

        public static string StripKnownPrefix(string domain, List<string> prefixes)
        {
            if (string.IsNullOrEmpty(domain)) return domain;
            int dot = domain.IndexOf('.');
            if (dot <= 0) return domain;
            string first = domain.Substring(0, dot);
            if (ContainsIgnoreCase(prefixes, first))
            {
                string rest = domain.Substring(dot + 1);
                if (rest.IndexOf('.') > 0) return rest;
            }
            return domain;
        }

        public static bool ContainsIgnoreCase(List<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
