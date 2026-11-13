using System.Collections.ObjectModel;

namespace IPBanManager.Models
{
    /// <summary>
    /// 持久化到 %APPDATA%\IPBanManager\data.json 的完整业务数据。
    /// </summary>
    public class AppData
    {
        public AppData()
        {
            Version = 1;
            Rules = new ObservableCollection<BanRule>();
            Groups = new ObservableCollection<BanGroup>();
            Operations = new ObservableCollection<OperationRecord>();
        }

        public int Version { get; set; }
        public ObservableCollection<BanRule> Rules { get; set; }
        public ObservableCollection<BanGroup> Groups { get; set; }
        public ObservableCollection<OperationRecord> Operations { get; set; }
    }
}
