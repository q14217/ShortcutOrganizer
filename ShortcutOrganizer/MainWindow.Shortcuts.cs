using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== 添加快捷方式 ====================

        private void AddShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "快捷方式文件|*.lnk|所有文件|*.*",
                Title = "选择要添加的快捷方式",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                string cat = GetTargetCategoryForAdd();
                if (cat == null) return;

                int ok = 0;
                foreach (var f in dialog.FileNames)
                    if (ProcessLnkFile(f, cat)) ok++;
                if (ok > 0)
                {
                    StatusText.Text = $"成功添加 {ok} 个快捷方式";
                    ScheduleSave();
                }
            }
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "所有文件|*.*",
                Title = "选择要添加的文件",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                string cat = GetTargetCategoryForAdd();
                if (cat == null) return;

                int ok = 0;
                foreach (var f in dialog.FileNames)
                    if (AddFileAsShortcut(f, cat)) ok++;
                if (ok > 0)
                {
                    StatusText.Text = $"成功添加 {ok} 个文件";
                    ScheduleSave();
                }
            }
        }

        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择要导入快捷方式的文件夹",
                Multiselect = false
            };

            if (dlg.ShowDialog() != true) return;

            string cat = GetTargetCategoryForAdd();
            if (cat == null) return;

            var files = SafeEnumerateLnkFiles(dlg.FolderName).ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("该文件夹下没有找到 .lnk 快捷方式。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int ok = 0, skipped = 0;
            foreach (var f in files)
            {
                try { if (ProcessLnkFile(f, cat)) ok++; }
                catch { skipped++; }
            }

            StatusText.Text = skipped > 0
                ? $"从文件夹导入 {ok} 个快捷方式（跳过 {skipped} 个失败项）"
                : $"从文件夹导入 {ok} 个快捷方式";
            ScheduleSave();
        }

        private static IEnumerable<string> SafeEnumerateLnkFiles(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                yield break;

            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                string[] lnkFiles = Array.Empty<string>();
                try { lnkFiles = Directory.GetFiles(current, "*.lnk", SearchOption.TopDirectoryOnly); }
                catch { }

                foreach (var f in lnkFiles) yield return f;

                string[] subDirs = Array.Empty<string>();
                try { subDirs = Directory.GetDirectories(current, "*", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var dir in subDirs)
                {
                    string name = Path.GetFileName(dir);
                    if (string.Equals(name, "System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Recovery", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "Config.Msi", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var attrs = File.GetAttributes(dir);
                        if ((attrs & FileAttributes.Hidden) != 0 ||
                            (attrs & FileAttributes.System) != 0) continue;
                    }
                    catch { continue; }

                    stack.Push(dir);
                }
            }
        }

        private bool ProcessFileToCategory(string filePath, string targetCategory)
        {
            if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                return ProcessLnkFile(filePath, targetCategory);
            return AddFileAsShortcut(filePath, targetCategory);
        }

        private bool ProcessLnkFile(string lnkPath, string targetCategory)
        {
            try
            {
                if (_allShortcuts.Any(s => string.Equals(s.SourcePath, lnkPath, StringComparison.OrdinalIgnoreCase)))
                    return false;

                string targetPath = GetLnkTargetPath(lnkPath);
                string args = GetLnkArguments(lnkPath);
                if (string.IsNullOrEmpty(targetPath)) targetPath = lnkPath;

                string name = Path.GetFileNameWithoutExtension(lnkPath);

                var item = new ShortcutItem
                {
                    Name = name,
                    TargetPath = targetPath,
                    Arguments = args,
                    Category = targetCategory,
                    SourcePath = lnkPath,
                    CreatedTime = DateTime.Now,
                    Icon = GetDefaultIcon(),
                    IsValid = !string.IsNullOrEmpty(targetPath) &&
                              (File.Exists(targetPath) || Directory.Exists(targetPath)),
                    SortOrder = _allShortcuts.Count > 0 ? _allShortcuts.Max(s => s.SortOrder) + 1 : 0
                };

                Dispatcher.Invoke(() =>
                {
                    _allShortcuts.Add(item);
                    LoadIconAsync(item, targetPath);
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "处理快捷方式失败");
                return false;
            }
        }

        private bool AddFileAsShortcut(string filePath, string targetCategory)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                var item = new ShortcutItem
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    TargetPath = filePath,
                    Category = targetCategory,
                    SourcePath = filePath,
                    CreatedTime = DateTime.Now,
                    Icon = GetDefaultIcon(),
                    IsValid = true,
                    SortOrder = _allShortcuts.Count > 0 ? _allShortcuts.Max(s => s.SortOrder) + 1 : 0
                };

                Dispatcher.Invoke(() =>
                {
                    _allShortcuts.Add(item);
                    LoadIconAsync(item, filePath);
                });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "添加文件失败");
                return false;
            }
        }

        private void LoadIconAsync(ShortcutItem item, string path)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var icon = GetCachedIcon(path);
                    Dispatcher.Invoke(() => item.Icon = icon);
                }
                catch (Exception ex) { Logger.Warn($"异步加载图标失败: {ex.Message}"); }
            });
        }

        // ==================== 固定到常用 ====================

        private void TogglePinMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;

            s.IsPinned = !s.IsPinned;
            UpdateCategoryCounts();
            RefreshCurrentView();
            ScheduleSave();

            StatusText.Text = s.IsPinned
                ? $"已固定「{s.Name}」到常用"
                : $"已取消固定「{s.Name}」";
        }

        // ==================== 快捷方式拖拽启动（拖到分类）====================

        private void ShortcutListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ShortcutListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(null);
            Vector diff = _dragStartPoint - pos;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var lbItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (lbItem == null) return;

            var clicked = lbItem.DataContext as ShortcutItem;
            if (clicked == null) return;

            List<ShortcutItem> items;
            if (ShortcutListBox.SelectedItems.Contains(clicked) &&
                ShortcutListBox.SelectedItems.Count > 1)
            {
                items = ShortcutListBox.SelectedItems.Cast<ShortcutItem>().ToList();
            }
            else
            {
                items = new List<ShortcutItem> { clicked };
            }

            var data = new DataObject(ShortcutDragFormat, items);
            try
            {
                DragDrop.DoDragDrop(lbItem, data, DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "拖拽失败");
            }
        }

        // ==================== 批量操作 ====================

        private void BatchDeleteShortcutsButton_Click(object sender, RoutedEventArgs e) => BatchDeleteShortcuts();
        private void BatchDeleteShortcutsMenuItem_Click(object sender, RoutedEventArgs e) => BatchDeleteShortcuts();

        private void BatchDeleteShortcuts()
        {
            var selected = ShortcutListBox.SelectedItems.Cast<ShortcutItem>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show($"确定要删除选中的 {selected.Count} 个快捷方式吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DeleteShortcutsWithUndo(selected, $"删除 {selected.Count} 个快捷方式");
                RefreshCurrentView();
                ScheduleSave();
            }
        }

        private void BatchMoveShortcutsButton_Click(object sender, RoutedEventArgs e) => BatchMoveShortcuts();
        private void BatchMoveShortcutsMenuItem_Click(object sender, RoutedEventArgs e) => BatchMoveShortcuts();

        private void BatchMoveShortcuts()
        {
            var selected = ShortcutListBox.SelectedItems.Cast<ShortcutItem>().ToList();
            if (selected.Count == 0) return;

            var existing = _categories
                .Where(c => c != _favoritesCategory && c != _recentCategory)
                .Select(c => c.Name)
                .ToList();

            var dialog = new InputDialog("移动到分类", "请输入目标分类名称：", existing);
            if (dialog.ShowDialog() == true)
            {
                string target = dialog.Result;
                if (string.IsNullOrWhiteSpace(target)) return;

                if (!_categories.Any(c => c.Name == target))
                    _categories.Add(new CategoryInfo { Name = target, ShortcutCount = 0 });

                MoveShortcutsWithUndo(selected, target, $"移动 {selected.Count} 项到「{target}」");
                UpdateCategoryCounts();
                RefreshCurrentView();
                ScheduleSave();
            }
        }

        // ==================== 失效扫描 ====================

        private void ScanInvalidButton_Click(object sender, RoutedEventArgs e)
            => ScanInvalidShortcuts(silent: false);

        private void ScanInvalidShortcuts(bool silent)
        {
            int invalid = 0;
            foreach (var s in _allShortcuts)
            {
                bool valid = !string.IsNullOrEmpty(s.TargetPath) &&
                             (File.Exists(s.TargetPath) || Directory.Exists(s.TargetPath));
                s.IsValid = valid;
                if (!valid) invalid++;
            }

            if (!silent)
            {
                if (invalid == 0)
                {
                    MessageBox.Show("未发现失效的快捷方式。", "扫描完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var r = MessageBox.Show(
                        $"发现 {invalid} 个失效项（已用红点标记）。\n是否批量删除这些失效项？",
                        "扫描完成", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes)
                    {
                        var bad = _allShortcuts.Where(x => !x.IsValid).ToList();
                        DeleteShortcutsWithUndo(bad, $"删除 {bad.Count} 个失效项");
                        RefreshCurrentView();
                        ScheduleSave();
                    }
                }
            }
            StatusText.Text = $"失效扫描完成，{invalid} 个失效项";
        }

        // ==================== 右键菜单 ====================

        private void OpenShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s != null) OpenShortcut(s);
        }

        private void RunAsAdminMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = s.TargetPath,
                    Arguments = s.Arguments ?? "",
                    UseShellExecute = true,
                    Verb = "runas"
                });
                IncrementOpenCount(s);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"以管理员身份运行失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFileLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;
            string path = !string.IsNullOrEmpty(s.TargetPath) && File.Exists(s.TargetPath)
                ? s.TargetPath : s.SourcePath;
            if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                string dir = File.Exists(path) ? Path.GetDirectoryName(path) : path;
                if (Directory.Exists(dir))
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
        }

        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null || string.IsNullOrEmpty(s.TargetPath)) return;
            try
            {
                Clipboard.SetText(s.TargetPath);
                StatusText.Text = "已复制路径到剪贴板";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败：{ex.Message}");
            }
        }

        private void RenameShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;
            var dialog = new RenameDialog(s.Name);
            if (dialog.ShowDialog() == true)
                RenameShortcutInternal(s, dialog.NewName);
        }

        private void MoveShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;

            var existing = _categories
                .Where(c => c != _favoritesCategory && c != _recentCategory)
                .Select(c => c.Name)
                .ToList();

            var dialog = new InputDialog("移动到分类", "请输入目标分类名称：", existing);
            if (dialog.ShowDialog() == true)
            {
                string target = dialog.Result;
                if (string.IsNullOrWhiteSpace(target)) return;
                if (!_categories.Any(c => c.Name == target))
                    _categories.Add(new CategoryInfo { Name = target, ShortcutCount = 0 });

                MoveShortcutsWithUndo(new List<ShortcutItem> { s }, target,
                    $"移动「{s.Name}」到「{target}」");
                UpdateCategoryCounts();
                RefreshCurrentView();
                ScheduleSave();
            }
        }

        private void DeleteShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;
            if (MessageBox.Show($"确定要删除快捷方式 \"{s.Name}\" 吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DeleteShortcutsWithUndo(new List<ShortcutItem> { s }, $"删除「{s.Name}」");
                RefreshCurrentView();
                ScheduleSave();
            }
        }

        private void PropertiesShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var s = GetShortcutFromMenuItem(sender);
            if (s == null) return;
            string info = $"名称: {s.Name}\n" +
                          $"目标路径: {s.TargetPath}\n" +
                          $"分类: {s.Category}\n" +
                          $"添加时间: {s.CreatedTime:yyyy-MM-dd HH:mm:ss}\n" +
                          $"打开次数: {s.OpenCount}\n" +
                          $"上次打开: {(s.LastOpenedTime.HasValue ? s.LastOpenedTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "从未")}\n" +
                          $"状态: {(s.IsValid ? "有效" : "已失效")}\n" +
                          $"固定: {(s.IsPinned ? "是" : "否")}\n" +
                          $"来源: {s.SourcePath}";
            MessageBox.Show(info, "属性", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private ShortcutItem GetShortcutFromMenuItem(object sender)
        {
            if (sender is MenuItem mi && mi.Tag is ShortcutItem s) return s;
            return null;
        }

        // ==================== 双击 / 打开 ====================

        private void ShortcutListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var pt = e.GetPosition(ShortcutListBox);
            var hit = ShortcutListBox.InputHitTest(pt) as DependencyObject;
            bool overItem = false;
            while (hit != null)
            {
                if (hit is ListBoxItem) { overItem = true; break; }
                hit = VisualTreeHelper.GetParent(hit);
            }

            if (!overItem)
            {
                AddFileButton_Click(this, new RoutedEventArgs());
                return;
            }

            if (ShortcutListBox.SelectedItem is ShortcutItem item)
                OpenShortcut(item);
        }

        private void OpenShortcut(ShortcutItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.TargetPath)) return;
                if (File.Exists(item.TargetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.TargetPath,
                        Arguments = item.Arguments ?? "",
                        UseShellExecute = true
                    });
                    IncrementOpenCount(item);
                }
                else if (Directory.Exists(item.TargetPath))
                {
                    Process.Start("explorer.exe", item.TargetPath);
                    IncrementOpenCount(item);
                }
                else
                {
                    item.IsValid = false;
                    MessageBox.Show($"目标文件或目录不存在: {item.TargetPath}\n\n已标记为失效。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：{ex.Message}");
            }
        }

        private void IncrementOpenCount(ShortcutItem item)
        {
            item.OpenCount++;
            item.LastOpenedTime = DateTime.Now;
            UpdateCategoryCounts();
            ScheduleSave();
        }

        // ==================== 辅助：视觉树 ====================
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}