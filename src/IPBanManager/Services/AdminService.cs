using System;
using System.Diagnostics;
using System.Security.Principal;

namespace IPBanManager.Services
{
    public static class AdminService
    {
        public static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("管理员权限检测失败：" + ex.Message);
                return false;
            }
        }

        /// <summary>以 runas 方式重启自身。用户取消 UAC 时会返回 false。</summary>
        public static bool TryRelaunchElevated(string arguments, out string error)
        {
            error = "";
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = exe;
                info.Arguments = arguments ?? "";
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.WorkingDirectory = Environment.CurrentDirectory;
                Process.Start(info);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LogService.Warn("提权重启失败：" + ex.Message);
                return false;
            }
        }

        public static bool OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                Process.Start("explorer.exe", "\"" + path + "\"");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Warn("打开目录失败：" + ex.Message);
                return false;
            }
        }

        public static bool OpenFile(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                Process.Start("notepad.exe", "\"" + path + "\"");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Warn("打开文件失败：" + ex.Message);
                return false;
            }
        }
    }
}
