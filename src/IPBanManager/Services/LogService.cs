using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IPBanManager.Services
{
    /// <summary>
    /// 双通道日志：文件日志用于排查问题，操作记录由 AppState 持久化到 data.json。
    /// </summary>
    public static class LogService
    {
        private static readonly object Gate = new object();

        public static string CurrentLogFile
        {
            get
            {
                return Path.Combine(StoragePaths.LogsDirectory,
                    "app-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message)
        {
            Write("ERROR", message, null);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message, ex);
        }

        public static void Write(string level, string message, Exception ex)
        {
            try
            {
                lock (Gate)
                {
                    string line = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}",
                        DateTime.Now, level, message ?? "");
                    if (ex != null)
                    {
                        line += Environment.NewLine + ex;
                    }
                    line += Environment.NewLine;

                    File.AppendAllText(CurrentLogFile, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // 日志失败不能影响用户操作。
            }
        }

        public static string Tail(int maxLines)
        {
            try
            {
                if (!File.Exists(CurrentLogFile)) return "（今天还没有日志）";
                string[] lines = File.ReadAllLines(CurrentLogFile, Encoding.UTF8);
                if (lines.Length <= maxLines) return string.Join(Environment.NewLine, lines);
                List<string> tail = new List<string>();
                for (int i = lines.Length - maxLines; i < lines.Length; i++) tail.Add(lines[i]);
                return string.Join(Environment.NewLine, tail);
            }
            catch (Exception ex)
            {
                return "读取日志失败：" + ex.Message;
            }
        }
    }
}
