using System;
using System.Diagnostics;
using System.Text;

namespace IPBanManager.Services
{
    public class DnsFlushResult
    {
        public bool Success { get; set; }
        public bool ApiOk { get; set; }
        public bool IpconfigOk { get; set; }
        public string Method { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
    }

    /// <summary>
    /// DNS 缓存刷新。默认 auto 模式同时调用 Windows API 和 ipconfig /flushdns，
    /// 任一成功即视为刷新成功；两种都失败才报告失败。
    /// </summary>
    public static class DnsService
    {
        [System.Runtime.InteropServices.DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache", SetLastError = true)]
        private static extern bool DnsFlushResolverCache();

        public static DnsFlushResult Flush(string mode)
        {
            bool useApi = true;
            bool useIpconfig = true;
            if (string.Equals(mode, "api", StringComparison.OrdinalIgnoreCase))
            {
                useIpconfig = false;
            }
            else if (string.Equals(mode, "ipconfig", StringComparison.OrdinalIgnoreCase))
            {
                useApi = false;
            }

            DnsFlushResult result = new DnsFlushResult();
            StringBuilder detail = new StringBuilder();

            if (useApi)
            {
                try
                {
                    result.ApiOk = DnsFlushResolverCache();
                    detail.AppendLine("Windows API DnsFlushResolverCache：" + (result.ApiOk ? "成功" : "返回 False"));
                }
                catch (Exception ex)
                {
                    result.ApiOk = false;
                    detail.AppendLine("Windows API 调用异常：" + ex.Message);
                    LogService.Warn("DnsFlushResolverCache 调用失败：" + ex.Message);
                }
            }

            if (useIpconfig)
            {
                string output;
                result.IpconfigOk = RunIpconfigFlushDns(out output);
                detail.AppendLine("ipconfig /flushdns：" + (result.IpconfigOk ? "成功" : "失败"));
                if (!string.IsNullOrWhiteSpace(output)) detail.AppendLine(output.Trim());
            }

            result.Success = (useApi && result.ApiOk) || (useIpconfig && result.IpconfigOk);
            if (result.ApiOk && result.IpconfigOk) result.Method = "Windows API + ipconfig /flushdns";
            else if (result.ApiOk) result.Method = "Windows API";
            else if (result.IpconfigOk) result.Method = "ipconfig /flushdns";
            else result.Method = "无";

            if (result.Success)
            {
                result.Message = "DNS 缓存已刷新（" + result.Method + "）";
                LogService.Info(result.Message);
            }
            else
            {
                result.Message = "DNS 刷新失败，请手动执行 ipconfig /flushdns";
                LogService.Warn(result.Message + " " + detail);
            }

            result.Detail = detail.ToString().Trim();
            return result;
        }

        private static bool RunIpconfigFlushDns(out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = "ipconfig.exe";
                info.Arguments = "/flushdns";
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding = Encoding.Default;

                using (Process process = Process.Start(info))
                {
                    if (process == null) return false;
                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(8000))
                    {
                        try { process.Kill(); } catch { }
                        output = "ipconfig 超时（8 秒）";
                        return false;
                    }
                    output = (stdout + Environment.NewLine + stderr).Trim();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                LogService.Warn("启动 ipconfig 失败：" + ex.Message);
                return false;
            }
        }
    }
}
