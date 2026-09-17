using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        private void ScheduleSave()
        {
            _saveTimer?.Stop();
            _saveTimer?.Start();
        }

        private void SaveData()
        {
            _lastSaveTime = DateTime.Now;   // ⭐ 记录保存时间，避免自己触发的重载
            try
            {
                var saveData = new
                {
                    Categories = _categories
                        .Where(c => c != _favoritesCategory && c != _recentCategory)
                        .OrderBy(c => c.SortOrder)
                        .Select(c => new { c.Name, c.SortOrder })
                        .ToList(),

                    Shortcuts = _allShortcuts
                        .OrderBy(s => s.SortOrder)
                        .ThenBy(s => s.CreatedTime)
                        .Select(s => new
                        {
                            s.Name,
                            TargetPath = PathHelper.Collapse(s.TargetPath),
                            s.Arguments,
                            s.Category,
                            SourcePath = PathHelper.Collapse(s.SourcePath),
                            CreatedTime = s.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            s.OpenCount,
                            LastOpenedTime = s.LastOpenedTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                            s.SortOrder,
                            s.IsPinned
                        }).ToList()
                };
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                string json = JsonSerializer.Serialize(saveData, JsonHelpers.Default);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "保存数据失败");
            }
        }

        private void LoadData()
        {
            if (!File.Exists(configPath)) return;

            try
            {
                string json = File.ReadAllText(configPath);

                try
                {
                    ParseAndLoad(json);
                    Logger.Info($"数据加载完成: {_categories.Count} 分类, {_allShortcuts.Count} 快捷方式");
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "data.json 解析失败，尝试从备份恢复");
                }

                if (TryRestoreFromLatestBackup(out string usedBackup))
                {
                    try
                    {
                        _categories.Clear();
                        _allShortcuts.Clear();
                        string json2 = File.ReadAllText(configPath);
                        ParseAndLoad(json2);
                        Logger.Info($"已从备份恢复: {usedBackup}");
                        MessageBox.Show(
                            $"数据文件已损坏，已自动从最近的备份恢复：\n{usedBackup}",
                            "已从备份恢复",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                    catch (Exception ex2)
                    {
                        Logger.Error(ex2, "从备份恢复后仍解析失败");
                    }
                }

                MessageBox.Show(
                    "数据文件已损坏，且未找到可用的备份。\n\n程序将以空白数据启动。",
                    "数据损坏",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "加载数据失败");
            }
        }

        private void ParseAndLoad(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                var root = document.RootElement;

                if (root.TryGetProperty("Categories", out JsonElement catsEl))
                {
                    _categories.Clear();
                    int order = 0;
                    foreach (var cat in catsEl.EnumerateArray())
                    {
                        if (!cat.TryGetProperty("Name", out var nameEl)) continue;
                        int so = order++;
                        if (cat.TryGetProperty("SortOrder", out var soEl) && soEl.TryGetInt32(out var sov))
                            so = sov;
                        _categories.Add(new CategoryInfo
                        {
                            Name = nameEl.GetString(),
                            ShortcutCount = 0,
                            SortOrder = so
                        });
                    }
                }

                if (root.TryGetProperty("Shortcuts", out JsonElement scEl))
                {
                    int order = 0;
                    foreach (var item in scEl.EnumerateArray())
                    {
                        var s = new ShortcutItem();
                        if (item.TryGetProperty("Name", out var n)) s.Name = n.GetString();
                        if (item.TryGetProperty("TargetPath", out var t))
                            s.TargetPath = PathHelper.Expand(t.GetString());
                        if (item.TryGetProperty("Arguments", out var a)) s.Arguments = a.GetString();
                        if (item.TryGetProperty("Category", out var c)) s.Category = c.GetString();
                        if (item.TryGetProperty("SourcePath", out var sp))
                            s.SourcePath = PathHelper.Expand(sp.GetString());
                        if (item.TryGetProperty("CreatedTime", out var ct))
                            s.CreatedTime = DateTime.TryParse(ct.GetString(), out var dt) ? dt : DateTime.Now;
                        else s.CreatedTime = DateTime.Now;

                        if (item.TryGetProperty("OpenCount", out var oc) && oc.TryGetInt32(out var ocv))
                            s.OpenCount = ocv;

                        if (item.TryGetProperty("LastOpenedTime", out var lot) &&
                            lot.ValueKind == JsonValueKind.String)
                            s.LastOpenedTime = DateTime.TryParse(lot.GetString(), out var lotv)
                                ? lotv : (DateTime?)null;

                        if (item.TryGetProperty("SortOrder", out var so) && so.TryGetInt32(out var sov))
                            s.SortOrder = sov;
                        else
                            s.SortOrder = order;
                        order++;

                        if (item.TryGetProperty("IsPinned", out var pin) &&
                            (pin.ValueKind == JsonValueKind.True || pin.ValueKind == JsonValueKind.False))
                            s.IsPinned = pin.GetBoolean();

                        bool valid = false;
                        if (!string.IsNullOrEmpty(s.TargetPath) && File.Exists(s.TargetPath))
                        { s.Icon = GetCachedIcon(s.TargetPath); valid = true; }
                        else if (!string.IsNullOrEmpty(s.SourcePath) && File.Exists(s.SourcePath))
                        { s.Icon = GetCachedIcon(s.SourcePath); valid = true; }
                        else s.Icon = GetDefaultIcon();
                        s.IsValid = valid;

                        _allShortcuts.Add(s);
                    }
                }
            }
        }

        private bool TryRestoreFromLatestBackup(out string usedBackup)
        {
            usedBackup = null;
            try
            {
                string backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutOrganizer", "backups");
                if (!Directory.Exists(backupDir)) return false;

                var files = Directory.GetFiles(backupDir, "auto_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();

                foreach (var f in files)
                {
                    try
                    {
                        string test = File.ReadAllText(f);
                        using (JsonDocument.Parse(test)) { }

                        File.Copy(f, configPath, true);
                        usedBackup = f;
                        return true;
                    }
                    catch { continue; }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "扫描备份失败");
            }
            return false;
        }
    }
}