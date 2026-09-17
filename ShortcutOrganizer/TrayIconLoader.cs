using System;
using System.IO;
using System.Drawing;
using System.Windows;
using System.Diagnostics;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 加载托盘图标。优先从嵌入资源加载，兼容单文件发布。
    /// </summary>
    public static class TrayIconLoader
    {
        /// <summary>
        /// 按优先级加载图标：
        /// 1. 从 WPF 嵌入资源（pack://application:,,,/app.ico）
        /// 2. 从当前 exe 提取（非单文件发布时有效）
        /// 3. 系统默认图标
        /// </summary>
        public static Icon Load()
        {
            // ---- 方式 1：从 WPF 资源加载（单文件发布下也有效）----
            try
            {
                var uri = new Uri("pack://application:,,,/app.ico", UriKind.Absolute);
                var streamInfo = System.Windows.Application.GetResourceStream(uri);
                if (streamInfo != null && streamInfo.Stream != null)
                {
                    using (var stream = streamInfo.Stream)
                    {
                        // 复制一份，避免 Icon 持有流影响后续使用
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            ms.Position = 0;
                            Logger.Info("托盘图标：从嵌入资源加载成功");
                            return new Icon(ms);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"从嵌入资源加载图标失败: {ex.Message}");
            }

            // ---- 方式 2：从 exe 提取（非单文件时有效）----
            try
            {
                // Environment.ProcessPath 在 .NET 6+ 和单文件发布下都可用
                string exePath = Environment.ProcessPath;

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        Logger.Info($"托盘图标：从 exe 提取成功 ({exePath})");
                        return icon;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"从 exe 提取图标失败: {ex.Message}");
            }

            // ---- 方式 3：系统默认图标 ----
            Logger.Info("托盘图标：使用系统默认图标");
            return SystemIcons.Application;
        }
    }
}