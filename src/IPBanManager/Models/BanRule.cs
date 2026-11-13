using System;
using System.ComponentModel;

namespace IPBanManager.Models
{
    /// <summary>
    /// 一条 HOSTS 封禁规则。一个规则对应一个域名，便于搜索、分组和单独启停。
    /// IpMode: auto = 同时写入 0.0.0.0 与 127.0.0.1；custom = 只写入 CustomIp。
    /// </summary>
    public class BanRule : INotifyPropertyChanged
    {
        private string _domain;
        private string _ipMode;
        private string _customIp;
        private string _groupId;
        private string _remark;
        private bool _enabled;

        public BanRule()
        {
            Id = Guid.NewGuid().ToString("N");
            IpMode = "auto";
            CustomIp = "";
            GroupId = "custom";
            Remark = "";
            Enabled = true;
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public string Id { get; set; }

        public string Domain
        {
            get { return _domain; }
            set { if (_domain != value) { _domain = value; Raise("Domain"); } }
        }

        public string IpMode
        {
            get { return _ipMode; }
            set { if (_ipMode != value) { _ipMode = value; Raise("IpMode"); } }
        }

        public string CustomIp
        {
            get { return _customIp; }
            set { if (_customIp != value) { _customIp = value; Raise("CustomIp"); } }
        }

        public string GroupId
        {
            get { return _groupId; }
            set { if (_groupId != value) { _groupId = value; Raise("GroupId"); } }
        }

        public string Remark
        {
            get { return _remark; }
            set { if (_remark != value) { _remark = value; Raise("Remark"); } }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { if (_enabled != value) { _enabled = value; Raise("Enabled"); } }
        }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsAutoIp
        {
            get { return !string.Equals(IpMode, "custom", StringComparison.OrdinalIgnoreCase); }
        }

        public BanRule Clone()
        {
            return new BanRule
            {
                Id = Id,
                Domain = Domain,
                IpMode = IpMode,
                CustomIp = CustomIp,
                GroupId = GroupId,
                Remark = Remark,
                Enabled = Enabled,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
            };
        }

        private void Raise(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
