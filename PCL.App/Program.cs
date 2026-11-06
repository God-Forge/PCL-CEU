using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MeloongCore;
using PCL.App.Audio;
using PCL.App.Services;
using PCL.Core.Audio;

namespace PCL.App;

public static class Program {

    [STAThread]
    public static int Main(string[] args) {
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        AppServices.Initialize();
        var logPath = Path.Combine(AppServices.Paths.Logs, "PCL-CSharp.log");
        Logger.Instance = new FileLogger(logPath);
        // 日志就绪后再跑设置键自检，确保结果能被记录下来
        AppServices.VerifySettingKeys();
        Logger.Instance.MinLevel = LogLevel.Info;
        // 恢复上次的登录状态（需在日志初始化之后）
        LoginSessionService.Restore();
        // 过期账号在后台续期，不阻塞启动
        _ = LoginSessionService.RefreshExpiredAccountsAsync();
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
        // 界面就绪后在后台检查更新，有新版本时以公告形式提示
        UpdateAnnouncementService.StartBackgroundCheck();
        var tray = new TrayService();
        tray.Initialize(window);
        app.Exit += (_, _) => tray.Dispose();
        var music = CreateMusicPlayer();
        AppServices.Music = music;
        app.Exit += (_, _) => music.Dispose();
        Logger.Info("进入消息循环");
        app.Run(window);
        return 0;
    }

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    /// <summary>
    /// 按设置扫描 PCL/Musics 并启动背景音乐；未开启自动播放时返回未播放的播放器。
    /// </summary>
    private static MusicPlayerService CreateMusicPlayer() {
        var settings = AppServices.Settings;
        var player = new MusicPlayerService { Volume = settings.Get<int>("UiMusicVolume") };
        if (settings.Get<bool>("UiMusicAuto") != true) return player;
        try {
            var folder = Path.Combine(AppServices.Paths.Pcl, "Musics");
            var playlist = MusicPlaylist.FromFolder(folder, settings.Get<bool>("UiMusicRandom") == true);
            if (playlist.IsEmpty) {
                Logger.Info("未找到背景音乐，已跳过播放");
                return player;
            }
            player.Load(playlist);
            Logger.Info($"已开始播放背景音乐，共 {playlist.AllFiles.Count} 首");        } catch (Exception ex) {
            Logger.Warn(ex, "初始化背景音乐失败");
        }
        return player;
    }

}




































