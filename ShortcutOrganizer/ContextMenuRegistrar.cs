using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 注册/注销资源管理器右键菜单："发送到奥格快捷收纳箱"。
    /// 只写 HKCU，无需管理员权限。
    /// </summary>
    public static class ContextMenuRegistrar
    {
        private const string MenuRoot = @"Software\Classes\*\shell\ShortcutOrganizer";
        private const string CommandKey = @"Software\Classes\*\shell\ShortcutOrganizer\command";
        private const string MenuLabel = "发送到奥格快捷收纳箱";

        public static bool IsRegistered()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(MenuRoot))
                    return key != null;
            }
            catch { return false; }
        }

        public static bool Register()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;

                using (var key = Registry.CurrentUser.CreateSubKey(MenuRoot))
                {
                    if (key == null) return false;
                    key.SetValue("", MenuLabel);
                    key.SetValue("Icon", exePath);
                    key.SetValue("MultiSelectModel", "Player");
                }
                using (var key = Registry.CurrentUser.CreateSubKey(CommandKey))
                {
                    if (key == null) return false;
                    key.SetValue("", $"\"{exePath}\" --add \"%1\"");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "注册右键菜单失败");
                return false;
            }
        }

        public static bool Unregister()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(MenuRoot, false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "注销右键菜单失败");
                return false;
            }
        }
    }
}