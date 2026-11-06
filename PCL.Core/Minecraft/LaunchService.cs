using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Java;
using PCL.Core.Launch;
using PCL.Core.Login;
using PCL.Core.Minecraft;

namespace PCL.Core.Minecraft;

/// <summary>
/// 构建启动计划、启动 Java 进程并建立日志与窗口监视。
/// </summary>
public static class LaunchService {

    public static LaunchSession Launch(
        string mcFolder,
        string instanceFolder,
        string versionName,
        string jsonPath,
        LoginProfile login,
        string javaExe,
        int ramMb = 4096,
        int width = 854,
        int height = 480,
        int windowType = WindowSizeResolver.TypeDefault,
        GameProcessWatcherOptions? watcherOptions = null,
        string? windowTitle = null,
        IReadOnlyList<string>? extraJvmArguments = null,
        IReadOnlyList<string>? extraGameArguments = null,
        string? preLaunchCommand = null,
        bool waitForPreLaunch = true) {
        var json = JObject.Parse(File.ReadAllText(jsonPath));
        // 启动前命令：在真正拉起游戏之前执行
        if (!string.IsNullOrWhiteSpace(preLaunchCommand)) {
            var commandResult = LaunchCommandRunner
                .RunAsync(preLaunchCommand, instanceFolder, waitForPreLaunch)
                .GetAwaiter()
                .GetResult();
            if (!commandResult.Success) {
                Logger.Warn($"启动前命令执行失败：{commandResult.Error}");
            } else if (commandResult.Waited && commandResult.ExitCode != 0) {
                Logger.Warn($"启动前命令退出码 {commandResult.ExitCode}");
            }
        }
        var plan = LaunchPlanService.Build(json, mcFolder, instanceFolder, versionName, login, width, height, ramMb, windowType, extraJvmArguments, extraGameArguments);
        var startInfo = JavaProcessLauncher.BuildStartInfo(
            javaExe,
            Path.GetDirectoryName(javaExe) ?? "",
            mcFolder,
            instanceFolder,
            plan.Arguments,
            Encoding.UTF8);
        Logger.Info("[MC] 启动参数：" + LaunchArgumentFormatter.JoinForDisplay(plan.Arguments));
        Logger.Info("[MC] classpath：" + plan.Classpath);
        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动 Java 进程。");
        LaunchLogService.WriteLine($"[启动] 已创建游戏进程，PID {process.Id}");
        var watcher = new GameProcessWatcher(process, watcherOptions ?? BuildWatcherOptions(json, windowTitle));
        var session = new LaunchSession(process, watcher);
        process.Exited += (_, _) => {
            var message = $"[MC] 进程退出，退出码 {process.ExitCode}";
            LaunchLogService.WriteLine(message);
            Logger.Warn(message);
        };
        watcher.Start();
        return session;
    }

    /// <summary>
    /// 从版本 JSON 推断默认监视配置：用 releaseTime 的年份区分旧版退出码行为。
    /// </summary>
    public static GameProcessWatcherOptions BuildWatcherOptions(JObject json, string? windowTitle = null) {
        int? releaseYear = null;
        if (json["releaseTime"]?.Value<string>() is { Length: > 0 } releaseText
            && DateTime.TryParse(releaseText, out var releaseTime)) {
            releaseYear = releaseTime.Year;
        }
        return new GameProcessWatcherOptions {
            ReleaseYear = releaseYear,
            WindowTitle = string.IsNullOrWhiteSpace(windowTitle) ? null : windowTitle.Trim()
        };
    }

}
