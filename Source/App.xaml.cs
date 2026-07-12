using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace CodexWidget;

public partial class App : System.Windows.Application
{
    private Mutex? instanceMutex;
    private DispatcherTimer? trimTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instanceMutex = new Mutex(true, "Local\\AIUsageDesktopWidget", out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUmaruWidget");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"), $"{DateTime.Now:u} {args.Exception}\r\n");
            }
            catch { }
            System.Windows.MessageBox.Show("挂件遇到异常并已安全停止。可重新启动；若仍有问题请查看本机 crash.log。", "AI 额度桌面挂件");
            args.Handled = true;
            Shutdown();
        };

        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // 启动稳定后先修剪一次内存，之后每 10 分钟低频维护
        var warmup = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        warmup.Tick += (_, _) => { warmup.Stop(); TrimMemory(); };
        warmup.Start();
        trimTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        trimTimer.Tick += (_, _) => TrimMemory();
        trimTimer.Start();
    }

    /// <summary>深度回收托管堆并把闲置工作集归还系统，压低常驻内存。</summary>
    private static void TrimMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            EmptyWorkingSet(GetCurrentProcess());
        }
        catch { }
    }

    [DllImport("psapi.dll")] private static extern bool EmptyWorkingSet(IntPtr hProcess);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();

    protected override void OnExit(ExitEventArgs e)
    {
        trimTimer?.Stop();
        instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
