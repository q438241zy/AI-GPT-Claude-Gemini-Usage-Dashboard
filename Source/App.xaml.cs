using System.Threading;
using System.IO;
using System.Windows;

namespace CodexWidget;

public partial class App : System.Windows.Application
{
    private Mutex? instanceMutex;

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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
