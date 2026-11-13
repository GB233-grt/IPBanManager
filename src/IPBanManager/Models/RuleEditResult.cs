using System.Collections.Generic;

namespace IPBanManager.Models
{
    /// <summary>规则编辑对话框的返回值。</summary>
    public class RuleEditResult
    {
        public RuleEditResult()
        {
            Domains = new List<string>();
            IpMode = "auto";
            CustomIp = "";
            GroupId = "custom";
            Remark = "";
            ExpandSubdomains = false;
            Enabled = true;
        }

        public List<string> Domains { get; set; }
        public string IpMode { get; set; }
        public string CustomIp { get; set; }
        public string GroupId { get; set; }
        public string Remark { get; set; }
        public bool ExpandSubdomains { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>批量导入对话框的返回值。</summary>
    public class BatchImportResult
    {
        public BatchImportResult()
        {
            Lines = new List<string>();
            GroupId = "custom";
            ExpandSubdomains = false;
            Enabled = true;
        }

        public List<string> Lines { get; set; }
        public string GroupId { get; set; }
        public bool ExpandSubdomains { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>单条规则编辑（不是批量新增）时的返回值。</summary>
    public class RuleUpdateResult
    {
        public string Domain { get; set; }
        public string IpMode { get; set; }
        public string CustomIp { get; set; }
        public string GroupId { get; set; }
        public string Remark { get; set; }
        public bool Enabled { get; set; }
    }
}
