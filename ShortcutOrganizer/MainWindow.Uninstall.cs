using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== 卸载 ====================

        private void UninstallButton_Click(object sender, RoutedEventArgs e) => BatchUninstallShortcuts();
        private void BatchUninstallShortcutsMenuItem_Click(object sender, RoutedEventArgs e) => BatchUninstallShortcuts();

        private void UninstallShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s != null) UninstallShortcut(s, confirm: true);
        }

        private void BatchUninstallShortcuts()
        {
            var selected = ShortcutListBox.SelectedItems.Cast<ShortcutItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("请先选择要卸载的项。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var r = MessageBox.Show(
                $"确定要卸载选中的 {selected.Count} 个项吗？\n\n" +
                "• 安装版：调用卸载程序。\n" +
                "• 绿色版：文件夹移到回收站。",
                "确认批量卸载", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            int n = 0;
            foreach (var s in selected.ToList())
            {
                UninstallShortcut(s, confirm: false);
                n++;
            }
            StatusText.Text = $"批量卸载已处理 {n} 个项";
        }

        private void UninstallShortcut(ShortcutItem item, bool confirm)
        {
            string targetPath = item.TargetPath;
            if (string.IsNullOrEmpty(targetPath) || (!File.Exists(targetPath) && !Directory.Exists(targetPath)))
                targetPath = item.SourcePath;

            if (string.IsNullOrEmpty(targetPath) || (!File.Exists(targetPath) && !Directory.Exists(targetPath)))
            {
                MessageBox.Show($"找不到目标路径，无法卸载：{item.Name}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string uninstallString = FindUninstallString(targetPath);

            if (!string.IsNullOrEmpty(uninstallString))
            {
                if (confirm)
                {
                    var r = MessageBox.Show(
                        $"确定要卸载 \"{item.Name}\" 吗？\n\n将调用卸载程序：\n{uninstallString}",
                        "确认卸载", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r != MessageBoxResult.Yes) return;
                }

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{uninstallString}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    _allShortcuts.Remove(item);
                    RefreshCurrentView();
                    ScheduleSave();
                    StatusText.Text = $"已启动卸载程序：{item.Name}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动卸载程序失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                return;
            }

            // 绿色版 -> 移到回收站
            string folder = File.Exists(targetPath)
                ? Path.GetDirectoryName(targetPath)
                : (Directory.Exists(targetPath) ? targetPath : null);

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show($"找不到要删除的文件夹：{targetPath}", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string fullFolder = Path.GetFullPath(folder).TrimEnd('\\');
            string root = Path.GetPathRoot(fullFolder)?.TrimEnd('\\');
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');

            if (string.Equals(fullFolder, root, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullFolder, winDir, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("出于安全考虑，不能删除根目录或系统目录。", "操作被拒绝",
                    MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            if (confirm)
            {
                var r = MessageBox.Show(
                    $"找不到卸载程序，将把文件夹移到回收站：\n{folder}\n\n可从回收站还原。确定继续吗？",
                    "确认移到回收站", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                    folder,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                _allShortcuts.Remove(item);
                RefreshCurrentView();
                ScheduleSave();
                StatusText.Text = $"已移到回收站：{folder}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移到回收站失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FindUninstallString(string targetPath)
        {
            try
            {
                string installDir = File.Exists(targetPath)
                    ? Path.GetDirectoryName(targetPath)
                    : (Directory.Exists(targetPath) ? targetPath : null);

                if (string.IsNullOrEmpty(installDir)) return null;
                installDir = installDir.TrimEnd('\\');

                string[] subKeys =
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };
                RegistryKey[] roots = { Registry.LocalMachine, Registry.CurrentUser };

                foreach (var root in roots)
                {
                    foreach (var subKey in subKeys)
                    {
                        using (var key = root.OpenSubKey(subKey))
                        {
                            if (key == null) continue;

                            foreach (var sub in key.GetSubKeyNames())
                            {
                                using (var appKey = key.OpenSubKey(sub))
                                {
                                    if (appKey == null) continue;

                                    string uninstallString = appKey.GetValue("UninstallString") as string;
                                    if (string.IsNullOrEmpty(uninstallString)) continue;

                                    string installLocation = appKey.GetValue("InstallLocation") as string;
                                    if (!string.IsNullOrEmpty(installLocation) &&
                                        installLocation.TrimEnd('\\')
                                            .Equals(installDir, StringComparison.OrdinalIgnoreCase))
                                        return uninstallString;

                                    string displayIcon = appKey.GetValue("DisplayIcon") as string;
                                    if (!string.IsNullOrEmpty(displayIcon))
                                    {
                                        string iconPath = displayIcon;
                                        if (iconPath.Contains(","))
                                            iconPath = iconPath.Split(',')[0].Trim('"');
                                        if (File.Exists(iconPath))
                                        {
                                            string iconDir = Path.GetDirectoryName(iconPath)?.TrimEnd('\\');
                                            if (!string.IsNullOrEmpty(iconDir) &&
                                                iconDir.Equals(installDir, StringComparison.OrdinalIgnoreCase))
                                                return uninstallString;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Warn($"查找卸载程序失败: {ex.Message}"); }
            return null;
        }
    }
}