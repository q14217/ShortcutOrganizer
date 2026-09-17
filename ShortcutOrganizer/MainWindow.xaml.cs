using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShortcutOrganizer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // ==================== 字段 ====================
        private readonly ObservableCollection<CategoryInfo> _categories
            = new ObservableCollection<CategoryInfo>();
        private readonly ObservableCollection<ShortcutItem> _allShortcuts
            = new ObservableCollection<ShortcutItem>();

        private CategoryInfo _selectedCategory;
        private readonly string configPath;
        private bool _showAllCategories = false;
        private string _searchKeyword = "";
        private string _sortMode = "Default";
        private string _viewMode = "Card";

        private readonly LruCache<string, BitmapSource> _iconCache =
            new LruCache<string, BitmapSource>(500, StringComparer.OrdinalIgnoreCase);

        private System.Windows.Threading.DispatcherTimer _saveTimer;

        private CategoryInfo _favoritesCategory;
        private CategoryInfo _recentCategory;
        private AppSettings _settings = AppSettings.Load();

        // 分类拖拽
        private Point _catDragStartPoint;
        private const string CategoryReorderFormat = "ShortcutOrganizer.Category";

        // 快捷方式拖拽到分类
        private Point _dragStartPoint;
        private const string ShortcutDragFormat = "ShortcutOrganizer.ShortcutItems";

        // 托盘 / 热键共享字段
        private System.Windows.Forms.NotifyIcon _trayIcon;
        private bool _reallyExit = false;
        private IntPtr _windowHandle = IntPtr.Zero;

        private static readonly string[] DefaultCategoryNames =
        {
            "聊天通讯", "游戏", "影视", "音乐", "办公",
            "浏览器", "开发工具", "系统工具", "学习", "未分类"
        };

        // ==================== 属性 ====================
        public ObservableCollection<CategoryInfo> Categories => _categories;
        public ObservableCollection<ShortcutItem> AllShortcuts => _allShortcuts;

        public bool ShowAllCategories
        {
            get => _showAllCategories;
            set
            {
                if (_showAllCategories != value)
                {
                    _showAllCategories = value;
                    OnPropertyChanged(nameof(ShowAllCategories));
                }
            }
        }

        public CategoryInfo SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged(nameof(SelectedCategory));
                    Dispatcher.Invoke(() => RefreshCurrentView());
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ==================== 构造函数 ====================
        public MainWindow()
        {
            try
            {
                Logger.Info("MainWindow 构造开始");
                InitializeComponent();

                _sortMode = _settings.SortMode ?? "Default";
                _viewMode = _settings.ViewMode ?? "Card";

                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutOrganizer");
                configPath = Path.Combine(appDataPath, "data.json");

                _saveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1200)
                };
                _saveTimer.Tick += (s, e) =>
                {
                    _saveTimer.Stop();
                    SaveData();
                };

                DataContext = this;
                CategoryListBox.ItemsSource = _categories;

                bool isFirstRun = !File.Exists(configPath);

                LoadData();
                // ⭐ 启动配置文件监视（在 LoadData 之后）⭐
                InitializeConfigWatcher();

                if (_categories.Count == 0)
                {
                    AddDefaultCategories();
                    SaveData();
                    Logger.Info(isFirstRun ? "首次启动，已创建默认分类" : "数据为空，已重建默认分类");
                }
                else
                {
                    if (!_categories.Any(c => c.Name == "未分类"))
                    {
                        _categories.Add(new CategoryInfo
                        {
                            Name = "未分类",
                            ShortcutCount = 0,
                            SortOrder = _categories.Any()
                                ? _categories.Max(c => c.SortOrder) + 1 : 0
                        });
                    }
                }

                _allShortcuts.CollectionChanged += (s, e) =>
                {
                    UpdateCategoryCounts();
                    Dispatcher.Invoke(() => RefreshCurrentView());
                };

                if (ShortcutListBox != null)
                    ShortcutListBox.SelectionChanged += (s, e) => UpdateSelectionInfo();

                ApplyViewMode(_viewMode);
                ApplySortSelection(_sortMode);

                InitializeTrayIcon();

                Logger.Info("MainWindow 构造完成");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "构造函数异常");
                MessageBox.Show($"初始化失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddDefaultCategories()
        {
            int order = 0;
            foreach (var name in DefaultCategoryNames)
            {
                if (_categories.Any(c =>
                    c != _favoritesCategory &&
                    c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _categories.Add(new CategoryInfo
                {
                    Name = name,
                    ShortcutCount = 0,
                    SortOrder = order++
                });
            }
        }

        // ==================== 窗口加载 ====================
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_favoritesCategory == null)
            {
                _favoritesCategory = new CategoryInfo
                {
                    Name = "⭐ 常用",
                    ShortcutCount = 0,
                    SortOrder = -1
                };
                _categories.Insert(0, _favoritesCategory);
            }

            if (_recentCategory == null)
            {
                _recentCategory = new CategoryInfo
                {
                    Name = "🕐 最近",
                    ShortcutCount = 0,
                    SortOrder = 0
                };
                _categories.Insert(1, _recentCategory);
            }

            var firstReal = _categories.FirstOrDefault(c =>
                c != _favoritesCategory && c != _recentCategory);
            if (firstReal != null)
            {
                SelectedCategory = firstReal;
                CategoryListBox.SelectedItem = firstReal;
                CurrentCategoryText.Text = firstReal.Name;
            }

            ShowAllCheckBox.IsChecked = false;
            ThemeToggleButton.Content = ThemeManager.IsDark ? "☀" : "🌙";
            UpdateViewToggleIcon();

            if (_settings.AutoBackupOnStartup)
                AutoBackupIfNeeded();

            RefreshCurrentView();
        }

        // ==================== 键盘快捷键 ====================
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                if (e.Key == Key.F1)
                {
                    new ShortcutsHelpWindow { Owner = this }.ShowDialog();
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.L)
                {
                    ViewToggleButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.Z)
                {
                    PerformUndo();
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.F)
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.N)
                {
                    AddCategoryButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                else if (ctrl && e.Key == Key.A)
                {
                    if (ShortcutListBox.IsKeyboardFocusWithin || ShortcutListBox.IsFocused)
                    {
                        ShortcutListBox.SelectAll();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Delete)
                {
                    if (ShortcutListBox.IsKeyboardFocusWithin || ShortcutListBox.IsFocused)
                    {
                        BatchDeleteShortcuts();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F2)
                {
                    if (ShortcutListBox.SelectedItem is ShortcutItem s)
                    {
                        var dialog = new RenameDialog(s.Name);
                        if (dialog.ShowDialog() == true)
                            RenameShortcutInternal(s, dialog.NewName);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Enter)
                {
                    if (ShortcutListBox.SelectedItem is ShortcutItem s)
                    {
                        OpenShortcut(s);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F5)
                {
                    ScanInvalidShortcuts(silent: false);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    if (SearchBox.Text?.Length > 0)
                    {
                        SearchBox.Text = "";
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "快捷键处理失败");
            }
        }

        // ==================== 主题 / 视图 / 设置 ====================
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Toggle();
            ThemeToggleButton.Content = ThemeManager.IsDark ? "☀" : "🌙";
        }

        private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _viewMode = _viewMode == "Card" ? "List" : "Card";
            ApplyViewMode(_viewMode);
            _settings.ViewMode = _viewMode;
            _settings.Save();
        }

        private void ApplyViewMode(string mode)
        {
            try
            {
                if (ShortcutListBox == null) return;

                if (mode == "List")
                {
                    ShortcutListBox.ItemTemplate = (DataTemplate)FindResource("ListTemplate");
                    ShortcutListBox.ItemsPanel = (ItemsPanelTemplate)FindResource("ListPanel");
                }
                else
                {
                    ShortcutListBox.ItemTemplate = (DataTemplate)FindResource("CardTemplate");
                    ShortcutListBox.ItemsPanel = (ItemsPanelTemplate)FindResource("CardPanel");
                }
                UpdateViewToggleIcon();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "切换视图失败");
            }
        }

        private void UpdateViewToggleIcon()
        {
            if (ViewToggleButton == null) return;
            ViewToggleButton.Content = _viewMode == "Card" ? "🔲" : "☰";
            ViewToggleButton.ToolTip = _viewMode == "Card"
                ? "切换到列表视图 (Ctrl+L)"
                : "切换到卡片视图 (Ctrl+L)";
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings) { Owner = this };
            win.ShowDialog();

            if (win.Saved)
            {
                _settings = win.Result;
                _sortMode = _settings.SortMode;
                ApplySortSelection(_sortMode);
                ApplyViewMode(_settings.ViewMode);
                RefreshCurrentView();
                StatusText.Text = "设置已保存";
            }
        }

        // ==================== 搜索 / 排序 ====================
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            _searchKeyword = SearchBox.Text?.Trim() ?? "";
            RefreshCurrentView();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox == null) return;
            if (!(SortComboBox.SelectedItem is ComboBoxItem item)) return;
            if (!(item.Tag is string tag)) return;

            _sortMode = tag;

            try
            {
                if (_settings != null)
                {
                    _settings.SortMode = tag;
                    _settings.Save();
                }
                if (IsLoaded && ShortcutListBox != null)
                    RefreshCurrentView();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "排序切换失败");
            }
        }

        private void ApplySortSelection(string tag)
        {
            if (SortComboBox == null) return;
            foreach (ComboBoxItem item in SortComboBox.Items)
            {
                if ((item.Tag as string) == tag)
                {
                    SortComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        // ==================== 分类选中 ====================
        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (CategoryListBox.SelectedItem is CategoryInfo category)
            {
                SelectedCategory = category;
                if (!_showAllCategories && CurrentCategoryText != null)
                    CurrentCategoryText.Text = category.Name;
            }
        }

        // ==================== 视图刷新 ====================
        private void RefreshCurrentView()
        {
            try
            {
                if (ShortcutListBox == null) return;

                IEnumerable<ShortcutItem> items;

                if (SelectedCategory == _favoritesCategory && _favoritesCategory != null)
                {
                    // 固定项永远置顶；其余按打开次数
                    items = _allShortcuts
                        .Where(s => s.IsPinned || s.OpenCount > 0)
                        .OrderByDescending(s => s.IsPinned)
                        .ThenByDescending(s => s.OpenCount)
                        .ThenByDescending(s => s.LastOpenedTime)
                        .Take(100);

                    if (!string.IsNullOrEmpty(_searchKeyword))
                        items = items.Where(MatchesSearch);

                    if (CurrentCategoryText != null)
                        CurrentCategoryText.Text = "⭐ 常用";
                }
                else if (SelectedCategory == _recentCategory && _recentCategory != null)
                {
                    // 最近 7 天打开过的
                    var cutoff = DateTime.Now.AddDays(-7);
                    items = _allShortcuts
                        .Where(s => s.LastOpenedTime.HasValue && s.LastOpenedTime.Value >= cutoff)
                        .OrderByDescending(s => s.LastOpenedTime)
                        .Take(100);

                    if (!string.IsNullOrEmpty(_searchKeyword))
                        items = items.Where(MatchesSearch);

                    if (CurrentCategoryText != null)
                        CurrentCategoryText.Text = "🕐 最近";
                }
                else
                {
                    if (_showAllCategories || SelectedCategory == null)
                    {
                        items = _allShortcuts;
                        if (CurrentCategoryText != null && _showAllCategories)
                            CurrentCategoryText.Text = "所有分类";
                    }
                    else
                    {
                        items = _allShortcuts.Where(s => s.Category == SelectedCategory.Name);
                        if (CurrentCategoryText != null)
                            CurrentCategoryText.Text = SelectedCategory.Name;
                    }

                    if (!string.IsNullOrEmpty(_searchKeyword))
                        items = items.Where(MatchesSearch);

                    items = ApplySort(items);
                }

                var list = items.ToList();
                ShortcutListBox.ItemsSource = list;

                if (ShortcutCountText != null)
                    ShortcutCountText.Text = list.Count.ToString();

                if (EmptyStatePanel != null)
                    EmptyStatePanel.Visibility = list.Count == 0
                        ? Visibility.Visible : Visibility.Collapsed;

                if (StatusText != null)
                {
                    string suffix = string.IsNullOrEmpty(_searchKeyword)
                        ? "" : $"（搜索“{_searchKeyword}”）";
                    StatusText.Text = $"显示 {list.Count} 个项{suffix}";
                }

                UpdateSelectionInfo();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "刷新视图失败");
            }
        }

        private bool MatchesSearch(ShortcutItem s)
        {
            return (!string.IsNullOrEmpty(s.Name) &&
                    s.Name.IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrEmpty(s.TargetPath) &&
                    s.TargetPath.IndexOf(_searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private IEnumerable<ShortcutItem> ApplySort(IEnumerable<ShortcutItem> src)
        {
            switch (_sortMode)
            {
                case "NameAsc": return src.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase);
                case "NameDesc": return src.OrderByDescending(s => s.Name, StringComparer.OrdinalIgnoreCase);
                case "TimeAsc": return src.OrderBy(s => s.CreatedTime);
                case "TimeDesc": return src.OrderByDescending(s => s.CreatedTime);
                case "CountDesc": return src.OrderByDescending(s => s.OpenCount).ThenBy(s => s.Name);
                default: return src.OrderBy(s => s.SortOrder).ThenBy(s => s.CreatedTime);
            }
        }

        private void UpdateSelectionInfo()
        {
            if (SelectionInfoText == null) return;
            int n = ShortcutListBox?.SelectedItems?.Count ?? 0;
            SelectionInfoText.Text = n > 0 ? $"已选中 {n} 项" : "";
        }

        private void UpdateCategoryCounts()
        {
            foreach (var cat in _categories)
            {
                if (cat == _favoritesCategory || cat == _recentCategory) continue;
                cat.ShortcutCount = _allShortcuts.Count(s => s.Category == cat.Name);
            }
            if (_favoritesCategory != null)
                _favoritesCategory.ShortcutCount = _allShortcuts.Count(s => s.IsPinned || s.OpenCount > 0);

            if (_recentCategory != null)
            {
                var cutoff = DateTime.Now.AddDays(-7);
                _recentCategory.ShortcutCount = _allShortcuts.Count(
                    s => s.LastOpenedTime.HasValue && s.LastOpenedTime.Value >= cutoff);
            }
        }

        // ==================== 拖放（文件）====================
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (SelectedCategory == null && !_showAllCategories)
                {
                    MessageBox.Show("请先选择一个分类。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string targetCat = GetTargetCategoryForAdd();
                if (targetCat == null) return;

                int successCount = 0;
                foreach (string filePath in files)
                {
                    bool ok = await System.Threading.Tasks.Task.Run(
                        () => ProcessFileToCategory(filePath, targetCat));
                    if (ok) successCount++;
                }

                if (successCount > 0)
                {
                    StatusText.Text = $"成功添加 {successCount} 个项";
                    ScheduleSave();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "拖放处理失败");
                MessageBox.Show($"处理失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ==================== 显示所有分类 ====================
        private void ShowAllCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            ShowAllCategories = ShowAllCheckBox.IsChecked == true;
            if (ShowAllCategories)
            {
                CategoryListBox.SelectedItem = null;
                CurrentCategoryText.Text = "所有分类";
            }
            else if (SelectedCategory != null)
            {
                CurrentCategoryText.Text = SelectedCategory.Name;
            }
            RefreshCurrentView();
        }

        // ==================== 辅助 ====================
        private string GetTargetCategoryForAdd()
        {
            if (_showAllCategories) return "未分类";
            if (SelectedCategory == _favoritesCategory || SelectedCategory == _recentCategory)
                return "未分类";
            if (SelectedCategory == null)
            {
                MessageBox.Show("请先选择一个分类。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return null;
            }
            return SelectedCategory.Name;
        }

        private void SelectAllInCategoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShortcutListBox.SelectAll();
        }
    }
}