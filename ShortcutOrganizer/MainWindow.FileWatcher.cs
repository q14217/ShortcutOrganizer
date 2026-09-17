using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        private FileSystemWatcher _configWatcher;
        private DispatcherTimer _reloadTimer;
        private DateTime _lastSaveTime = DateTime.MinValue;

        /// <summary>
        /// 启动配置文件监视。应在 LoadData() 之后调用。
        /// </summary>
        private void InitializeConfigWatcher()
        {
            try
            {
                string dir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                _configWatcher = new FileSystemWatcher(dir, Path.GetFileName(configPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };
                _configWatcher.Changed += (s, e) => ScheduleReload();
                _configWatcher.Created += (s, e) => ScheduleReload();
                _configWatcher.Renamed += (s, e) => ScheduleReload();

                _reloadTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(700)
                };
                _reloadTimer.Tick += (s, e) =>
                {
                    _reloadTimer.Stop();
                    ReloadFromDisk();
                };

                Logger.Info("配置监视已启动");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "初始化配置监视失败");
            }
        }

        private void StopConfigWatcher()
        {
            try
            {
                if (_configWatcher != null)
                {
                    _configWatcher.EnableRaisingEvents = false;
                    _configWatcher.Dispose();
                    _configWatcher = null;
                }
                _reloadTimer?.Stop();
            }
            catch { }
        }

        private void ScheduleReload()
        {
            // 忽略自己刚刚保存触发的变更
            if ((DateTime.Now - _lastSaveTime).TotalMilliseconds < 2000)
                return;

            Dispatcher.Invoke(() =>
            {
                _reloadTimer?.Stop();
                _reloadTimer?.Start();
            });
        }

        private void ReloadFromDisk()
        {
            try
            {
                if (!File.Exists(configPath)) return;

                // 记录当前视图状态，重载后恢复
                string currentCatName = SelectedCategory?.Name;
                bool wasShowAll = _showAllCategories;
                string searchText = SearchBox?.Text ?? "";

                // 清空并重新加载
                _categories.Clear();
                _allShortcuts.Clear();
                LoadData();

                // 恢复虚拟分类位置
                if (_favoritesCategory != null && !_categories.Contains(_favoritesCategory))
                    _categories.Insert(0, _favoritesCategory);
                if (_recentCategory != null && !_categories.Contains(_recentCategory))
                {
                    int idx = _favoritesCategory != null ? 1 : 0;
                    _categories.Insert(idx, _recentCategory);
                }

                // 恢复视图状态
                if (wasShowAll)
                {
                    ShowAllCheckBox.IsChecked = true;
                }
                else if (!string.IsNullOrEmpty(currentCatName))
                {
                    var cat = _categories.FirstOrDefault(c => c.Name == currentCatName);
                    if (cat != null)
                    {
                        SelectedCategory = cat;
                        CategoryListBox.SelectedItem = cat;
                    }
                }

                UpdateCategoryCounts();
                RefreshCurrentView();

                if (StatusText != null)
                    StatusText.Text = "已从磁盘重新加载";

                Logger.Info("已从磁盘重新加载配置");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "重新加载配置失败");
            }
        }
    }
}