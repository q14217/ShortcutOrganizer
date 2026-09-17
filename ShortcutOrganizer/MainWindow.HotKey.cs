using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        private const int HOTKEY_ID = 0xA007;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;
        private const int WM_HOTKEY = 0x0312;
        private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 供 UI 层读取的热键状态
        public bool LastHotKeyFailed { get; private set; } = false;
        public string LastHotKeyError { get; private set; } = null;

        private bool _hotKeyRegistered = false;

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                _windowHandle = new WindowInteropHelper(this).Handle;
                var source = HwndSource.FromHwnd(_windowHandle);
                source?.AddHook(WndProc);

                TryApplyHotKey(
                    _settings?.HotKeyModifiers ?? "Ctrl+Alt",
                    _settings?.HotKeyKey ?? "S");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "热键初始化失败");
            }
        }

        /// <summary>
        /// 尝试应用一组热键。失败时返回 false，并填充 LastHotKeyError。
        /// 可供设置面板调用。
        /// </summary>
        public bool TryApplyHotKey(string modifiers, string keyName)
        {
            UnregisterGlobalHotKey();

            try
            {
                if (_windowHandle == IntPtr.Zero)
                {
                    LastHotKeyFailed = true;
                    LastHotKeyError = "窗口句柄未就绪";
                    return false;
                }

                uint mods = 0;
                string m = modifiers ?? "Ctrl+Alt";
                if (m.Contains("Ctrl")) mods |= MOD_CTRL;
                if (m.Contains("Alt")) mods |= MOD_ALT;
                if (m.Contains("Shift")) mods |= MOD_SHIFT;
                if (m.Contains("Win")) mods |= MOD_WIN;
                mods |= MOD_NOREPEAT;

                Key key;
                if (!Enum.TryParse(keyName ?? "S", true, out key))
                    key = Key.S;

                uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

                if (!RegisterHotKey(_windowHandle, HOTKEY_ID, mods, vk))
                {
                    int err = Marshal.GetLastWin32Error();
                    LastHotKeyFailed = true;
                    LastHotKeyError = err == ERROR_HOTKEY_ALREADY_REGISTERED
                        ? "该组合键已被其他程序占用"
                        : $"注册失败（错误码 {err}）";
                    Logger.Warn($"注册全局热键失败: {modifiers}+{keyName}, {LastHotKeyError}");
                    return false;
                }

                _hotKeyRegistered = true;
                LastHotKeyFailed = false;
                LastHotKeyError = null;
                Logger.Info($"全局热键已注册: {modifiers}+{keyName}");
                return true;
            }
            catch (Exception ex)
            {
                LastHotKeyFailed = true;
                LastHotKeyError = ex.Message;
                Logger.Error(ex, "注册热键异常");
                return false;
            }
        }

        private void UnregisterGlobalHotKey()
        {
            if (!_hotKeyRegistered) return;
            try
            {
                if (_windowHandle != IntPtr.Zero)
                    UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _hotKeyRegistered = false;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "注销热键失败");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                RestoreFromTray();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}