using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Drawing;

namespace ShortcutOrganizer
{
    public partial class MainWindow
    {
        // ==================== LNK 解析 ====================

        private string GetLnkTargetPath(string lnkPath)
        {
            try
            {
                if (!File.Exists(lnkPath)) return string.Empty;
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return string.Empty;
                dynamic shell = Activator.CreateInstance(t);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                return shortcut.TargetPath;
            }
            catch (Exception ex)
            {
                Logger.Warn($"解析 lnk 目标失败: {ex.Message}");
                return string.Empty;
            }
        }

        private string GetLnkArguments(string lnkPath)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                dynamic shortcut = shell.CreateShortcut(lnkPath);
                return shortcut.Arguments ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warn($"解析 lnk 参数失败: {ex.Message}");
                return "";
            }
        }

        // ==================== 图标 ====================

        private BitmapSource GetCachedIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return GetDefaultIcon();

            // LRU 内部已线程安全
            if (_iconCache.TryGetValue(path, out var cached))
                return cached;

            var icon = ExtractIcon(path);
            _iconCache.Add(path, icon);
            return icon;
        }

        private BitmapSource ExtractIcon(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return GetDefaultIcon();

                using (Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath))
                {
                    if (icon != null)
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle, Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        if (bmp.CanFreeze) bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"提取图标失败 [{filePath}]: {ex.Message}");
            }
            return GetDefaultIcon();
        }

        private BitmapSource GetDefaultIcon()
        {
            try
            {
                string p = Path.Combine(Environment.SystemDirectory, "shell32.dll");
                if (File.Exists(p))
                {
                    using (Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(p))
                    {
                        if (icon != null)
                        {
                            var bmp = Imaging.CreateBitmapSourceFromHIcon(
                                icon.Handle, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            if (bmp.CanFreeze) bmp.Freeze();
                            return bmp;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"提取默认图标失败: {ex.Message}");
            }
            return CreateDefaultBitmapSource();
        }

        private BitmapSource CreateDefaultBitmapSource()
        {
            int size = 32;
            int stride = size * 4;
            byte[] pixels = new byte[size * stride];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 200; pixels[i + 1] = 200; pixels[i + 2] = 200; pixels[i + 3] = 255;
            }

            var bmp = BitmapSource.Create(size, size, 96, 96,
                PixelFormats.Bgra32, null, pixels, stride);
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }
    }
}