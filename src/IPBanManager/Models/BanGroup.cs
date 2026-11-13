using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace IPBanManager.Models
{
    /// <summary>
    /// 规则分组。Enabled=false 表示整组逻辑暂停：不修改组内规则的 Enabled，只在写入 HOSTS 时跳过。
    /// </summary>
    public class BanGroup : INotifyPropertyChanged
    {
        private string _name;
        private string _color;
        private bool _enabled;

        public BanGroup()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "新分组";
            Color = "#4C8DFF";
            Enabled = true;
            IsSystem = false;
            CreatedAt = DateTime.Now;
        }

        public string Id { get; set; }

        public string Name
        {
            get { return _name; }
            set { if (_name != value) { _name = value; Raise("Name"); } }
        }

        public string Color
        {
            get { return _color; }
            set { if (_color != value) { _color = value; Raise("Color"); } }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { if (_enabled != value) { _enabled = value; Raise("Enabled"); } }
        }

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public static List<BanGroup> CreateBuiltIns()
        {
            return new List<BanGroup>
            {
                new BanGroup { Id = "study", Name = "学习专注", Color = "#6C8CFF", IsSystem = true, SortOrder = 0 },
                new BanGroup { Id = "game", Name = "游戏", Color = "#FF9A76", IsSystem = true, SortOrder = 1 },
                new BanGroup { Id = "video", Name = "视频", Color = "#FF7D9C", IsSystem = true, SortOrder = 2 },
                new BanGroup { Id = "ad", Name = "广告", Color = "#B38CFF", IsSystem = true, SortOrder = 3 },
                new BanGroup { Id = "custom", Name = "自定义", Color = "#58B7A5", IsSystem = true, SortOrder = 4 }
            };
        }

        private void Raise(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(name));
        }
    }
}
