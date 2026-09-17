using System;
using System.IO;
using System.Windows;
using System.Drawing;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== 托盘图标 ====================

        private void InitializeTrayIcon()
        {
            try
            {
                _trayIcon = new System.Windows.Forms.NotifyIcon();
                _trayIcon.Text = "奥格快捷收纳箱";

                // ---- 加载图标：优先嵌入资源 ----
                _trayIcon.Icon = TrayIconLoader.Load();
                _trayIcon.Visible = false;

                // ---- 事件 ----
                _trayIcon.DoubleClick += (s, e) => RestoreFromTray();

                // ---- 右键菜单 ----
                var menu = new System.Windows.Forms.ContextMenuStrip();

                var showItem = new System.Windows.Forms.ToolStripMenuItem("显示主窗口");
                showItem.Click += (s, e) => RestoreFromTray();
                menu.Items.Add(showItem);

                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var addItem = new System.Windows.Forms.ToolStripMenuItem("新建分类");
                addItem.Click += (s, e) =>
                {
                    RestoreFromTray();
                    Dispatcher.Invoke(() => AddCategoryButton_Click(this, new RoutedEventArgs()));
                };
                menu.Items.Add(addItem);

                var favItem = new System.Windows.Forms.ToolStripMenuItem("查看常用");
                favItem.Click += (s, e) =>
                {
                    RestoreFromTray();
                    Dispatcher.Invoke(() =>
                    {
                        if (_favoritesCategory != null)
                            CategoryListBox.SelectedItem = _favoritesCategory;
                    });
                };
                menu.Items.Add(favItem);

                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
                exitItem.Click += (s, e) => ExitApplication();
                menu.Items.Add(exitItem);

                _trayIcon.ContextMenuStrip = menu;

                Logger.Info("托盘图标初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "托盘初始化失败");
                MessageBox.Show(
                    $"托盘初始化失败：{ex.Message}\n\n最小化后可能无法恢复，请直接关闭窗口退出。",
                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HideToTray()
        {
            try
            {
                if (_trayIcon == null)
                {
                    Logger.Warn("HideToTray 调用但 _trayIcon 为 null，跳过");
                    return;
                }

                Hide();

                _trayIcon.Visible = true;

                // 气泡提示（受设置控制）
                bool showBalloon = _settings?.ShowTrayBalloon ?? true;
                if (showBalloon)
                {
                    try
                    {
                        _trayIcon.ShowBalloonTip(1500, "奥格快捷收纳箱",
                            "程序已最小化到托盘，双击图标可恢复。",
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                    catch { }
                }

                Logger.Info("已隐藏到托盘");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "隐藏到托盘失败");
            }
        }

        private void RestoreFromTray()
        {
            try
            {
                Show();
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
                Focus();

                if (_trayIcon != null)
                    _trayIcon.Visible = false;

                Logger.Info("已从托盘恢复");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "从托盘恢复失败");
            }
        }

        private void ExitApplication()
        {
            _reallyExit = true;
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            bool minimizeToTray = _settings?.MinimizeToTray ?? true;
            if (!minimizeToTray) return;

            if (WindowState == WindowState.Minimized)
                HideToTray();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_reallyExit)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            StopConfigWatcher();
            try { UnregisterGlobalHotKey(); } catch { }

            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
            }
            catch { }

            Logger.Info("===== 应用退出 =====");
        }
    }
}