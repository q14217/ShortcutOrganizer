using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ShortcutOrganizer
{
    public static class SingleInstanceHelper
    {
        private static Mutex _mutex;
        // 用 Local\ 前缀，只限制当前登录会话
        private const string MutexName = @"Local\ShortcutOrganizer_SingleInstance_Mutex";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// 尝试获取单实例锁。
        /// 返回 true：当前进程是唯一实例。
        /// 返回 false：已有实例在运行，已尝试激活它。
        /// </summary>
        public static bool TryAcquireOrActivate()
        {
            bool createdNew;
            try
            {
                _mutex = new Mutex(true, MutexName, out createdNew);
            }
            catch (Exception ex)
            {
                Logger.Warn($"Mutex 创建失败: {ex.Message}，按单实例处理");
                return true;
            }

            if (createdNew)
            {
                Logger.Info("当前进程获得单实例锁");
                return true;
            }

            Logger.Info("检测到已有实例，尝试激活");
            ActivateExistingInstance();
            return false;
        }

        /// <summary>
        /// 释放 Mutex（正常退出时调用）
        /// </summary>
        public static void Release()
        {
            try
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
                    _mutex = null;
                    Logger.Info("单实例锁已释放");
                }
            }
            catch { }
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var sameName = Process.GetProcessesByName(current.ProcessName);

                IntPtr targetHandle = IntPtr.Zero;

                foreach (var p in sameName)
                {
                    try
                    {
                        if (p.Id == current.Id) continue;
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            targetHandle = p.MainWindowHandle;
                            break;
                        }
                    }
                    catch { }
                }

                if (targetHandle != IntPtr.Zero)
                {
                    ShowWindowAsync(targetHandle, SW_RESTORE);
                    SetForegroundWindow(targetHandle);
                    Logger.Info($"已激活已有实例，句柄=0x{targetHandle.ToInt64():X}");
                }
                else
                {
                    Logger.Warn("找不到已有实例的主窗口句柄");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"激活已有实例失败: {ex.Message}");
            }
        }
    }
}