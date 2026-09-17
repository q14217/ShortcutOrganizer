using System;
using System.Collections.Generic;

namespace ShortcutOrganizer
{
    /// <summary>
    /// 路径变量化：保存时把常见的绝对路径前缀替换为环境变量，
    /// 加载时反向展开。这样 .scbackup 可以跨机器使用。
    /// </summary>
    public static class PathHelper
    {
        private static readonly List<KeyValuePair<string, string>> _vars =
            new List<KeyValuePair<string, string>>();

        static PathHelper()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                    _vars.Add(new KeyValuePair<string, string>(userProfile, "%USERPROFILE%"));
            }
            catch { }

            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrEmpty(desktop))
                    _vars.Add(new KeyValuePair<string, string>(desktop, "%DESKTOP%"));
            }
            catch { }

            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                    _vars.Add(new KeyValuePair<string, string>(docs, "%DOCUMENTS%"));
            }
            catch { }

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appData))
                    _vars.Add(new KeyValuePair<string, string>(appData, "%APPDATA%"));
            }
            catch { }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                    _vars.Add(new KeyValuePair<string, string>(localAppData, "%LOCALAPPDATA%"));
            }
            catch { }

            try
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(programs))
                    _vars.Add(new KeyValuePair<string, string>(programs, "%PROGRAMFILES%"));
            }
            catch { }

            try
            {
                string programsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(programsX86))
                    _vars.Add(new KeyValuePair<string, string>(programsX86, "%PROGRAMFILES(X86)%"));
            }
            catch { }

            try
            {
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(windir))
                    _vars.Add(new KeyValuePair<string, string>(windir, "%WINDIR%"));
            }
            catch { }

            // 按路径长度降序排列，优先匹配更具体的路径
            _vars.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }

        /// <summary>
        /// 把绝对路径压缩为带变量的形式（保存时调用）。
        /// 例：C:\Users\张三\Desktop\x.lnk → %USERPROFILE%\Desktop\x.lnk
        /// </summary>
        public static string Collapse(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            foreach (var kv in _vars)
            {
                if (path.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // 只替换"路径前缀"的整个目录边界，避免误伤（比如 C:\Users\张 匹配到 C:\Users\张三）
                    if (path.Length == kv.Key.Length ||
                        path[kv.Key.Length] == '\\' ||
                        path[kv.Key.Length] == '/')
                    {
                        return kv.Value + path.Substring(kv.Key.Length);
                    }
                }
            }
            return path;
        }

        /// <summary>
        /// 把变量展开为绝对路径（加载时调用）。
        /// 例：%USERPROFILE%\Desktop\x.lnk → C:\Users\李四\Desktop\x.lnk
        /// </summary>
        public static string Expand(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            try
            {
                // 先处理我们自己定义的别名
                if (path.StartsWith("%DESKTOP%", StringComparison.OrdinalIgnoreCase))
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    return desktop + path.Substring("%DESKTOP%".Length);
                }
                if (path.StartsWith("%DOCUMENTS%", StringComparison.OrdinalIgnoreCase))
                {
                    string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    return docs + path.Substring("%DOCUMENTS%".Length);
                }

                // 其它交给系统 ExpandEnvironmentVariables（%USERPROFILE% / %APPDATA% 等）
                return Environment.ExpandEnvironmentVariables(path);
            }
            catch
            {
                return path;
            }
        }
    }
}