using System;
using System.Windows;
using System.Windows.Threading;

namespace ShortcutOrganizer
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Logger.Init();

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Logger.Error(args.ExceptionObject as Exception, "AppDomain 未处理异常");
            };

            // 命令行 --add 模式：不参与单实例限制
            bool isAddMode = e.Args != null && e.Args.Length >= 2 &&
                string.Equals(e.Args[0], "--add", StringComparison.OrdinalIgnoreCase);

            if (isAddMode)
            {
                string file = e.Args[1];
                Logger.Info($"命令行模式：--add {file}");

                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                try
                {
                    QuickAddService.HandleAdd(file);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "命令行添加失败");
                }
                Shutdown();
                return;
            }

            // 正常模式：单实例限制
            if (!SingleInstanceHelper.TryAcquireOrActivate())
            {
                Logger.Info("已有实例在运行，退出当前进程");
                Shutdown();
                return;
            }

            // 主窗口
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var mw = new MainWindow();
            MainWindow = mw;
            mw.Show();

            // 退出时释放 Mutex
            Exit += (s, args) => SingleInstanceHelper.Release();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Error(e.Exception, "UI 线程未处理异常");
            MessageBox.Show(
                $"发生未处理异常：{e.Exception.Message}\n\n详细信息已记录到日志。",
                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}