using System;
using System.IO;
using System.Diagnostics;

namespace ShortcutOrganizer
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string _logDir;
        private static bool _inited = false;

        public static string LogDirectory
        {
            get
            {
                if (_logDir == null)
                {
                    _logDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ShortcutOrganizer", "logs");
                }
                return _logDir;
            }
        }

        public static void Init()
        {
            if (_inited) return;
            try
            {
                Directory.CreateDirectory(LogDirectory);
                CleanOldLogs(7);
                _inited = true;
                Info("===== 应用启动 =====");
            }
            catch { }
        }

        public static void Info(string msg) => Write("INFO ", msg);
        public static void Warn(string msg) => Write("WARN ", msg);

        public static void Error(string msg) => Write("ERROR", msg);

        public static void Error(Exception ex, string context = null)
        {
            string msg = string.IsNullOrEmpty(context)
                ? ex.Message
                : $"{context}: {ex.Message}";
            Write("ERROR", $"{msg}{Environment.NewLine}{ex}");
        }

        private static void Write(string level, string msg)
        {
            try
            {
                if (_logDir == null) _logDir = LogDirectory;
                Directory.CreateDirectory(_logDir);

                string file = Path.Combine(_logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
                string line = $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}";

                lock (_lock)
                {
                    File.AppendAllText(file, line + Environment.NewLine);
                }
                Debug.WriteLine(line);
            }
            catch { }
        }

        private static void CleanOldLogs(int keepDays)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-keepDays);
                foreach (var f in Directory.GetFiles(LogDirectory, "log_*.txt"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) < cutoff)
                            File.Delete(f);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}