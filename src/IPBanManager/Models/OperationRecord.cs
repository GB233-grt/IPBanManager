using System;

namespace IPBanManager.Models
{
    /// <summary>
    /// 持久化的操作记录，首页和“日志与备份”页面共用。
    /// Kind 用于选择图标：add/delete/edit/toggle/apply/dns/backup/restore/group/theme/settings/error。
    /// </summary>
    public class OperationRecord
    {
        public OperationRecord()
        {
            Time = DateTime.Now;
            Title = "";
            Detail = "";
            Kind = "info";
        }

        public DateTime Time { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public bool Success { get; set; }
        public string Kind { get; set; }
    }
}
