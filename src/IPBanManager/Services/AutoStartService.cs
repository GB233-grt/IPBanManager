using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace IPBanManager.Services
{
    public class AutoStartResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 开机自启写 HKCU，不需要管理员权限。值名 IPBanManager，命令带 --minimized。
    /// </summary>
    public static class AutoStartService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "IPBanManager";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static AutoStartResult SetEnabled(bool enabled, bool startMinimized)
        {
            AutoStartResult result = new AutoStartResult();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null)
                    {
                        result.Success = false;
                        result.Message = "无法打开 HKCU Run 注册表项";
                        return result;
                    }

                    if (enabled)
                    {
                        string exe = Process.GetCurrentProcess().MainModule.FileName;
                        string command = "\"" + exe + "\"" + (startMinimized ? " --minimized" : "");
                        key.SetValue(ValueName, command, RegistryValueKind.String);
                        result.Success = true;
                        result.Message = "已设置开机自启";
                        LogService.Info("开机自启已启用：" + command);
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                        result.Success = true;
                        result.Message = "已关闭开机自启";
                        LogService.Info("开机自启已关闭");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "设置开机自启失败：" + ex.Message;
                LogService.Error("设置开机自启失败", ex);
            }
            return result;
        }
    }
}
