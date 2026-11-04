using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MeloongCore;
using PCL.App.Services;

namespace PCL.App;

public static class Program {

    [STAThread]
    public static int Main(string[] args) {
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        AppServices.Initialize();
        var logPath = Path.Combine(AppServices.Paths.Logs, "PCL-CSharp.log");
        Logger.Instance = new FileLogger(logPath);
        Logger.Instance.MinLevel = LogLevel.Info;
        Logger.Info("C# / .NET 8 主程序启动");
        Logger.Info("PCL 数据目录：" + AppServices.Paths.Pcl);

        AppDomain.CurrentDomain.UnhandledException += (_, e) => {
            if (e.ExceptionObject is Exception ex) Logger.Error(ex, "未处理的 AppDomain 异常");
        };
        TaskScheduler.UnobservedTaskException += (_, e) => {
            if (e.Exception is not null) Logger.Error(e.Exception, "未观察的任务异常");
            e.SetObserved();
        };

        if (args.Length > 0) {
            AttachConsole(-1);
            var exitCode = PCL.App.Cli.CliCommandService.Run(args);
            if (exitCode is not null) return exitCode.Value;
        }

        Logger.Info("初始化单实例");
        using var singleInstance = new SingleInstanceService();
        if (!singleInstance.TryAcquire()) {
            System.Windows.MessageBox.Show("PCL CEU 已经在运行。", "PCL CEU", MessageBoxButton.OK, MessageBoxImage.Information);
            return 0;
        }

        var app = new App();
        app.InitializeComponent();
        ThemeService.Apply(app);
        app.DispatcherUnhandledException += (_, e) => {
            Logger.Error(e.Exception, "未处理的 UI 异常");
            System.Windows.MessageBox.Show("发生未处理的错误，详情已写入日志。", "PCL CEU", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        Logger.Info("创建主窗口");
        var window = new MainWindow();
        var tray = new TrayService();
        tray.Initialize(window);
        app.Exit += (_, _) => tray.Dispose();
        Logger.Info("进入消息循环");
        app.Run(window);
        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

}




































