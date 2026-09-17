using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShortcutOrganizer
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public AppSettings Result { get; private set; }
        public bool Saved { get; private set; } = false;

        public SettingsWindow(AppSettings current)
        {
            InitializeComponent();
            _settings = current ?? new AppSettings();
            LoadToUi();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateHotKeyStatus();
        }

        private void LoadToUi()
        {
            var mods = _settings.HotKeyModifiers ?? "";
            ModCtrlCheck.IsChecked = mods.Contains("Ctrl");
            ModAltCheck.IsChecked = mods.Contains("Alt");
            ModShiftCheck.IsChecked = mods.Contains("Shift");
            ModWinCheck.IsChecked = mods.Contains("Win");

            HotKeyTextBox.Text = _settings.HotKeyKey ?? "S";

            foreach (ComboBoxItem item in ViewModeCombo.Items)
            {
                if ((item.Tag as string) == _settings.ViewMode)
                {
                    ViewModeCombo.SelectedItem = item;
                    break;
                }
            }
            if (ViewModeCombo.SelectedItem == null)
                ViewModeCombo.SelectedIndex = 0;

            AutoBackupCheck.IsChecked = _settings.AutoBackupOnStartup;
            MinTrayCheck.IsChecked = _settings.MinimizeToTray;
            ShowBalloonCheck.IsChecked = _settings.ShowTrayBalloon;

            ContextMenuCheck.IsChecked = ContextMenuRegistrar.IsRegistered();
        }

        private void UpdateHotKeyStatus()
        {
            var mw = Owner as MainWindow;
            if (mw != null && mw.LastHotKeyFailed)
            {
                HotKeyStatusText.Text = $"⚠ 当前热键注册失败：{mw.LastHotKeyError}";
                HotKeyStatusText.Foreground = (Brush)FindResource("DangerBrush");
            }
            else
            {
                HotKeyStatusText.Text = "✓ 当前热键有效";
                HotKeyStatusText.Foreground = (Brush)FindResource("SuccessBrush");
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var mods = new List<string>();
            if (ModCtrlCheck.IsChecked == true) mods.Add("Ctrl");
            if (ModAltCheck.IsChecked == true) mods.Add("Alt");
            if (ModShiftCheck.IsChecked == true) mods.Add("Shift");
            if (ModWinCheck.IsChecked == true) mods.Add("Win");

            if (mods.Count == 0)
            {
                MessageBox.Show("请至少选择一个修饰键。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string key = (HotKeyTextBox.Text ?? "").Trim();
            if (key.Length == 0)
            {
                MessageBox.Show("请输入一个按键（如 S、K、F1 等）。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _settings.HotKeyModifiers = string.Join("+", mods);
            _settings.HotKeyKey = key.Length > 1 ? key : key.ToUpper();
            _settings.ViewMode = (ViewModeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Card";
            _settings.AutoBackupOnStartup = AutoBackupCheck.IsChecked == true;
            _settings.MinimizeToTray = MinTrayCheck.IsChecked == true;
            _settings.ShowTrayBalloon = ShowBalloonCheck.IsChecked == true;

            _settings.Save();

            // 尝试应用热键
            if (Owner is MainWindow mw)
            {
                bool ok = mw.TryApplyHotKey(_settings.HotKeyModifiers, _settings.HotKeyKey);
                if (!ok)
                {
                    UpdateHotKeyStatus();
                    MessageBox.Show(
                        $"⚠ 全局热键注册失败：{mw.LastHotKeyError}\n\n" +
                        "其它设置已保存，但热键未生效。\n" +
                        "请更换一个组合键后再次点击“保存”。",
                        "热键注册失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            // 处理右键菜单注册
            try
            {
                bool wantRegistered = ContextMenuCheck.IsChecked == true;
                bool isRegistered = ContextMenuRegistrar.IsRegistered();

                if (wantRegistered && !isRegistered)
                {
                    if (!ContextMenuRegistrar.Register())
                    {
                        MessageBox.Show("右键菜单注册失败，请查看日志。", "警告",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else if (!wantRegistered && isRegistered)
                {
                    ContextMenuRegistrar.Unregister();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "处理右键菜单注册失败");
            }

            Result = _settings;
            Saved = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenDataDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShortcutOrganizer");
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开失败：{ex.Message}");
            }
        }

        private void OpenLogDir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Logger.LogDirectory);
                Process.Start("explorer.exe", Logger.LogDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开失败：{ex.Message}");
            }
        }
    }
}