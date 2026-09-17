using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        private void AutoBackupIfNeeded()
        {
            try
            {
                string backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutOrganizer", "backups");
                Directory.CreateDirectory(backupDir);

                string today = DateTime.Now.ToString("yyyyMMdd");
                string target = Path.Combine(backupDir, $"auto_{today}.json");

                if (!File.Exists(configPath)) return;
                if (File.Exists(target)) return;

                File.Copy(configPath, target, true);
                Logger.Info($"自动备份完成：{target}");

                var cutoff = DateTime.Now.AddDays(-10);
                foreach (var f in Directory.GetFiles(backupDir, "auto_*.json"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) < cutoff) File.Delete(f);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "自动备份失败");
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "快捷方式分类备份文件|*.scbackup|JSON文件|*.json",
                DefaultExt = "scbackup",
                FileName = $"ShortcutOrganizer_Backup_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var backup = new BackupData
                    {
                        Categories = _categories
                            .Where(c => c != _favoritesCategory && c != _recentCategory)
                            .OrderBy(c => c.SortOrder)
                            .Select(c => new CategoryInfo
                            {
                                Name = c.Name,
                                SortOrder = c.SortOrder
                            }).ToList(),
                        Shortcuts = _allShortcuts
                            .OrderBy(s => s.SortOrder)
                            .ThenBy(s => s.CreatedTime)
                            .Select(s => new ShortcutItemBackup
                            {
                                Name = s.Name,
                                TargetPath = PathHelper.Collapse(s.TargetPath),
                                Arguments = s.Arguments,
                                Category = s.Category,
                                SourcePath = PathHelper.Collapse(s.SourcePath),
                                CreatedTime = s.CreatedTime,
                                OpenCount = s.OpenCount,
                                LastOpenedTime = s.LastOpenedTime,
                                IsPinned = s.IsPinned
                            }).ToList()
                    };
                    string json = JsonSerializer.Serialize(backup, JsonHelpers.Default);
                    File.WriteAllText(dlg.FileName, json);
                    Logger.Info($"手动备份到 {dlg.FileName}");
                    MessageBox.Show("配置备份成功！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "备份失败");
                    MessageBox.Show($"备份失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "快捷方式分类备份文件|*.scbackup|JSON文件|*.json",
                Title = "选择备份文件"
            };
            if (dlg.ShowDialog() == true)
            {
                if (MessageBox.Show("恢复操作将覆盖当前所有分类和快捷方式，是否继续？",
                    "确认恢复", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var backup = JsonSerializer.Deserialize<BackupData>(json, JsonHelpers.Default);
                    if (backup == null) throw new Exception("备份文件格式无效");

                    var fav = _favoritesCategory;
                    var recent = _recentCategory;

                    _categories.Clear();
                    _allShortcuts.Clear();

                    if (fav != null) _categories.Add(fav);
                    if (recent != null) _categories.Add(recent);

                    foreach (var cat in backup.Categories)
                        _categories.Add(new CategoryInfo
                        {
                            Name = cat.Name,
                            ShortcutCount = 0,
                            SortOrder = cat.SortOrder
                        });

                    int order = 0;
                    foreach (var it in backup.Shortcuts)
                    {
                        var s = new ShortcutItem
                        {
                            Name = it.Name,
                            TargetPath = PathHelper.Expand(it.TargetPath),
                            Arguments = it.Arguments,
                            Category = it.Category,
                            SourcePath = PathHelper.Expand(it.SourcePath),
                            CreatedTime = it.CreatedTime,
                            OpenCount = it.OpenCount,
                            LastOpenedTime = it.LastOpenedTime,
                            IsPinned = it.IsPinned,
                            SortOrder = order++
                        };
                        if (!string.IsNullOrEmpty(s.TargetPath) && File.Exists(s.TargetPath))
                        { s.Icon = GetCachedIcon(s.TargetPath); s.IsValid = true; }
                        else if (!string.IsNullOrEmpty(s.SourcePath) && File.Exists(s.SourcePath))
                        { s.Icon = GetCachedIcon(s.SourcePath); s.IsValid = true; }
                        else { s.Icon = GetDefaultIcon(); s.IsValid = false; }
                        _allShortcuts.Add(s);
                    }

                    UpdateCategoryCounts();

                    if (_showAllCategories)
                    {
                        CategoryListBox.SelectedItem = null;
                        CurrentCategoryText.Text = "所有分类";
                    }
                    else
                    {
                        var firstReal = _categories.FirstOrDefault(c =>
                            c != _favoritesCategory && c != _recentCategory);
                        if (firstReal != null)
                        {
                            SelectedCategory = firstReal;
                            CategoryListBox.SelectedItem = firstReal;
                        }
                    }

                    RefreshCurrentView();
                    ScheduleSave();
                    Logger.Info($"从 {dlg.FileName} 恢复成功");
                    MessageBox.Show("配置恢复成功！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "恢复失败");
                    MessageBox.Show($"恢复失败：{ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                DefaultExt = "csv",
                FileName = $"ShortcutOrganizer_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("名称,分类,目标路径,参数,添加时间,打开次数,上次打开时间,状态,固定");
                foreach (var s in _allShortcuts.OrderBy(x => x.SortOrder))
                {
                    sb.AppendLine(string.Join(",",
                        CsvEscape(s.Name),
                        CsvEscape(s.Category),
                        CsvEscape(s.TargetPath),
                        CsvEscape(s.Arguments),
                        s.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        s.OpenCount,
                        s.LastOpenedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                        s.IsValid ? "有效" : "已失效",
                        s.IsPinned ? "是" : "否"));
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
                Logger.Info($"CSV 导出到 {dlg.FileName}");
                StatusText.Text = "CSV 导出成功";
                MessageBox.Show("CSV 导出成功！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "导出失败");
                MessageBox.Show($"导出失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "CSV 文件|*.csv|所有文件|*.*",
                Title = "选择要导入的 CSV 文件"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dlg.FileName, Encoding.UTF8);
                if (lines.Length < 2)
                {
                    MessageBox.Show("CSV 文件为空或无数据行。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int imported = 0, skipped = 0;
                int maxSort = _allShortcuts.Count > 0
                    ? _allShortcuts.Max(s => s.SortOrder) : -1;

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    var cols = ParseCsvLine(lines[i]);
                    if (cols.Count < 3) { skipped++; continue; }

                    string name = cols[0];
                    string cat = cols.Count > 1 ? cols[1] : "未分类";
                    string target = cols.Count > 2 ? cols[2] : "";
                    string args = cols.Count > 3 ? cols[3] : "";
                    DateTime created = DateTime.Now;
                    if (cols.Count > 4 && DateTime.TryParse(cols[4], out var dt)) created = dt;
                    int openCount = 0;
                    if (cols.Count > 5 && int.TryParse(cols[5], out var oc)) openCount = oc;
                    DateTime? lastOpened = null;
                    if (cols.Count > 6 && DateTime.TryParse(cols[6], out var lo)) lastOpened = lo;
                    bool isPinned = cols.Count > 8 &&
                        (cols[8] == "是" || cols[8].Equals("true", StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrWhiteSpace(target)) { skipped++; continue; }

                    if (_allShortcuts.Any(s =>
                        string.Equals(s.TargetPath, target, StringComparison.OrdinalIgnoreCase)))
                    { skipped++; continue; }

                    if (string.IsNullOrWhiteSpace(cat)) cat = "未分类";
                    if (!_categories.Any(c => c.Name == cat &&
                        c != _favoritesCategory && c != _recentCategory))
                    {
                        int maxOrder = _categories
                            .Where(c => c != _favoritesCategory && c != _recentCategory)
                            .Select(c => c.SortOrder)
                            .DefaultIfEmpty(-1).Max();
                        _categories.Add(new CategoryInfo
                        {
                            Name = cat,
                            ShortcutCount = 0,
                            SortOrder = maxOrder + 1
                        });
                    }

                    var item = new ShortcutItem
                    {
                        Name = string.IsNullOrWhiteSpace(name)
                            ? Path.GetFileNameWithoutExtension(target) : name,
                        TargetPath = target,
                        Arguments = args,
                        Category = cat,
                        SourcePath = target,
                        CreatedTime = created,
                        OpenCount = openCount,
                        LastOpenedTime = lastOpened,
                        IsPinned = isPinned,
                        Icon = GetDefaultIcon(),
                        IsValid = File.Exists(target) || Directory.Exists(target),
                        SortOrder = ++maxSort
                    };

                    _allShortcuts.Add(item);
                    LoadIconAsync(item, target);
                    imported++;
                }

                UpdateCategoryCounts();
                RefreshCurrentView();
                ScheduleSave();
                Logger.Info($"CSV 导入 {imported} 项，跳过 {skipped} 项");
                MessageBox.Show(
                    $"导入完成：成功 {imported} 项，跳过 {skipped} 项。",
                    "导入结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "导入失败");
                MessageBox.Show($"导入失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string CsvEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null) return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '"') inQuotes = true;
                    else sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}