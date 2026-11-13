using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IPBanManager.Services
{
    public class BackupItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long Size { get; set; }
        public string Reason { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (Size < 1024) return Size + " B";
                return (Size / 1024.0).ToString("0.0") + " KB";
            }
        }
    }

    /// <summary>
    /// HOSTS 自动备份。每次写入前创建 hosts_yyyyMMdd_HHmmss_fff.bak，
    /// 旁边写一个 .meta.txt 记录来源、原因和时间，便于审计。
    /// </summary>
    public static class BackupManager
    {
        public static BackupItem CreateBackup(string hostsPath, string backupDirectory, string reason)
        {
            if (string.IsNullOrWhiteSpace(hostsPath) || !File.Exists(hostsPath)) return null;

            string directory = StoragePaths.EnsureDirectory(backupDirectory);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string fileName = "hosts_" + stamp + ".bak";
            string target = Path.Combine(directory, fileName);
            int suffix = 1;
            while (File.Exists(target))
            {
                target = Path.Combine(directory, "hosts_" + stamp + "_" + suffix + ".bak");
                suffix++;
            }

            File.Copy(hostsPath, target, false);
            FileInfo info = new FileInfo(target);

            try
            {
                string meta = "来源：" + hostsPath + Environment.NewLine +
                              "时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                              "原因：" + (reason ?? "") + Environment.NewLine;
                File.WriteAllText(target + ".meta.txt", meta, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LogService.Warn("写入备份元数据失败：" + ex.Message);
            }

            return new BackupItem
            {
                FilePath = target,
                FileName = Path.GetFileName(target),
                CreatedAt = info.CreationTime,
                Size = info.Length,
                Reason = reason
            };
        }

        public static List<BackupItem> ListBackups(string backupDirectory)
        {
            List<BackupItem> items = new List<BackupItem>();
            if (string.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory)) return items;

            string[] files;
            try
            {
                files = Directory.GetFiles(backupDirectory, "hosts_*.bak");
            }
            catch (Exception ex)
            {
                LogService.Error("枚举备份失败", ex);
                return items;
            }

            foreach (string file in files)
            {
                try
                {
                    FileInfo info = new FileInfo(file);
                    BackupItem item = new BackupItem
                    {
                        FilePath = file,
                        FileName = info.Name,
                        CreatedAt = info.LastWriteTime,
                        Size = info.Length,
                        Reason = ReadReason(file)
                    };
                    items.Add(item);
                }
                catch (Exception ex)
                {
                    LogService.Warn("读取备份信息失败：" + file + " " + ex.Message);
                }
            }

            items.Sort(delegate(BackupItem a, BackupItem b) { return b.CreatedAt.CompareTo(a.CreatedAt); });
            return items;
        }

        public static string RestoreBackup(string backupPath, string hostsPath, string backupDirectory, string reason)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("备份文件不存在", backupPath);
            if (string.IsNullOrWhiteSpace(hostsPath)) throw new ArgumentException("HOSTS 路径为空");

            // 恢复前先备份当前文件，避免恢复动作本身造成不可逆丢失。
            if (File.Exists(hostsPath))
            {
                CreateBackup(hostsPath, backupDirectory, "恢复前自动备份：" + reason);
            }

            string directory = Path.GetDirectoryName(hostsPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = hostsPath + ".ipban-restore-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(backupPath, temp, true);
                Exception replaceException = null;
                try
                {
                    File.Replace(temp, hostsPath, null);
                }
                catch (Exception ex)
                {
                    replaceException = ex;
                    try
                    {
                        File.Copy(temp, hostsPath, true);
                    }
                    catch
                    {
                        throw replaceException;
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
                catch
                {
                    // 忽略临时文件清理失败。
                }
            }
            return "已恢复备份 " + Path.GetFileName(backupPath);
        }

        public static int Prune(string backupDirectory, int keepCount)
        {
            if (keepCount < 1) keepCount = 50;
            List<BackupItem> items = ListBackups(backupDirectory);
            int removed = 0;
            for (int i = items.Count - 1; i >= keepCount; i--)
            {
                try
                {
                    File.Delete(items[i].FilePath);
                    TryDelete(items[i].FilePath + ".meta.txt");
                    removed++;
                }
                catch (Exception ex)
                {
                    LogService.Warn("清理旧备份失败：" + items[i].FileName + " " + ex.Message);
                }
            }
            return removed;
        }

        public static string ReadReason(string backupPath)
        {
            try
            {
                string meta = backupPath + ".meta.txt";
                if (!File.Exists(meta)) return "自动备份";
                string[] lines = File.ReadAllLines(meta);
                foreach (string line in lines)
                {
                    if (line.StartsWith("原因：", StringComparison.Ordinal))
                        return line.Substring(3).Trim();
                }
            }
            catch
            {
                // 忽略元数据读取失败。
            }
            return "自动备份";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 忽略。
            }
        }
    }
}
