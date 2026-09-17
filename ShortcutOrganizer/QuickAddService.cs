using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 独立于 MainWindow 的"快速添加"服务，用于处理命令行 --add 请求。
    /// </summary>
    public static class QuickAddService
    {
        // ==================== Win32 API（激活已有主程序）====================

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        // ==================== 主入口 ====================

        public static void HandleAdd(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    MessageBox.Show($"文件不存在：{filePath}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutOrganizer", "data.json");

                // 读取现有分类
                var categories = LoadCategories(configPath);
                if (categories.Count == 0)
                {
                    MessageBox.Show("尚无分类，请先打开程序创建一个分类。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 选择分类
                var win = new QuickAddWindow(categories, filePath);
                if (win.ShowDialog() != true) return;

                string targetCat = win.SelectedCategory;
                if (string.IsNullOrEmpty(targetCat)) return;

                // 追加到 data.json
                AddShortcutToFile(configPath, filePath, targetCat);

                MessageBox.Show(
                    $"已添加「{Path.GetFileName(filePath)}」到分类「{targetCat}」。",
                    "添加成功", MessageBoxButton.OK, MessageBoxImage.Information);

                // ⭐ 激活已有的主程序窗口 ⭐
                ActivateExistingInstance();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "快速添加失败");
                MessageBox.Show($"添加失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== 激活已有主程序 ====================

        /// <summary>
        /// 查找已经运行的 ShortcutOrganizer 主程序，把它拉到前台。
        /// 如果没找到，什么也不做。
        /// </summary>
        private static void ActivateExistingInstance()
        {
            try
            {
                var current = System.Diagnostics.Process.GetCurrentProcess();
                var sameName = System.Diagnostics.Process.GetProcessesByName(current.ProcessName);

                foreach (var p in sameName)
                {
                    // 跳过自己
                    if (p.Id == current.Id) continue;

                    // 有主窗口句柄 → 这就是主程序
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindowAsync(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        Logger.Info($"已激活已存在的主程序 PID={p.Id}");
                        return;
                    }
                }

                Logger.Info("未找到正在运行的主程序");
            }
            catch (Exception ex)
            {
                Logger.Warn($"激活主程序失败: {ex.Message}");
            }
        }

        // ==================== 读写 data.json ====================

        private static System.Collections.Generic.List<string> LoadCategories(string configPath)
        {
            var result = new System.Collections.Generic.List<string>();
            try
            {
                if (!File.Exists(configPath)) return result;

                string json = WaitAndRead(configPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("Categories", out var cats))
                    {
                        foreach (var c in cats.EnumerateArray())
                        {
                            if (c.TryGetProperty("Name", out var n))
                            {
                                string name = n.GetString();
                                if (!string.IsNullOrEmpty(name) && !name.StartsWith("⭐") && !name.StartsWith("🕐"))
                                    result.Add(name);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "快速添加：读取分类失败");
            }
            return result;
        }

        private static void AddShortcutToFile(string configPath, string filePath, string category)
        {
            string json = WaitAndRead(configPath);

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                var options = new JsonSerializerOptions { WriteIndented = true };

                var newObj = new System.Collections.Generic.Dictionary<string, object>();
                var catsList = new System.Collections.Generic.List<object>();
                var scList = new System.Collections.Generic.List<object>();

                if (root.TryGetProperty("Categories", out var cats))
                {
                    foreach (var c in cats.EnumerateArray())
                    {
                        string name = c.TryGetProperty("Name", out var n) ? n.GetString() : null;
                        int so = c.TryGetProperty("SortOrder", out var s) && s.TryGetInt32(out var sv) ? sv : 0;
                        catsList.Add(new { Name = name, SortOrder = so });
                    }
                }

                if (root.TryGetProperty("Shortcuts", out var scs))
                {
                    foreach (var s in scs.EnumerateArray())
                    {
                        scList.Add(JsonSerializer.Deserialize<object>(s.GetRawText()));
                    }
                }

                // 取当前最大排序号
                int maxOrder = 0;
                foreach (var s in scList)
                {
                    try
                    {
                        var t = JsonSerializer.Serialize(s);
                        using (var d = JsonDocument.Parse(t))
                        {
                            if (d.RootElement.TryGetProperty("SortOrder", out var soEl) &&
                                soEl.TryGetInt32(out var sov) && sov > maxOrder)
                                maxOrder = sov;
                        }
                    }
                    catch { }
                }

                scList.Add(new
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    TargetPath = PathHelper.Collapse(filePath),
                    Arguments = "",
                    Category = category,
                    SourcePath = PathHelper.Collapse(filePath),
                    CreatedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    OpenCount = 0,
                    LastOpenedTime = (string)null,
                    SortOrder = maxOrder + 1,
                    IsPinned = false
                });

                newObj["Categories"] = catsList;
                newObj["Shortcuts"] = scList;

                string outJson = JsonSerializer.Serialize(newObj, options);
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));

                // 带重试的写入
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        File.WriteAllText(configPath, outJson);
                        break;
                    }
                    catch (IOException)
                    {
                        System.Threading.Thread.Sleep(200);
                    }
                }

                Logger.Info($"快速添加：{filePath} → {category}");
            }
        }

        private static string WaitAndRead(string path)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (!File.Exists(path)) return "{}";
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var sr = new StreamReader(fs))
                        return sr.ReadToEnd();
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
            return File.Exists(path) ? File.ReadAllText(path) : "{}";
        }
    }
}