using System.Diagnostics;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Minecraft;

namespace PCL.Core.Minecraft;

/// <summary>
/// 构建启动计划并启动 Java 进程。
/// </summary>
public static class LaunchService {

    public static Process Launch(
        string mcFolder,
        string instanceFolder,
        string versionName,
        string jsonPath,
        LoginProfile login,
        string javaExe,
        int ramMb = 4096,
        int width = 854,
        int height = 480) {
        var json = JObject.Parse(File.ReadAllText(jsonPath));
        var plan = LaunchPlanService.Build(json, mcFolder, instanceFolder, versionName, login, width, height, ramMb);
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
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => {
            var message = $"[MC] 进程退出，退出码 {process.ExitCode}";
            LaunchLogService.WriteLine(message);
            Logger.Warn(message);
        };
        process.OutputDataReceived += (_, e) => {
            if (e.Data is null) return;
            LaunchLogService.WriteLine(e.Data);
            Logger.Info("[MC] " + e.Data);
        };
        process.ErrorDataReceived += (_, e) => {
            if (e.Data is null) return;
            LaunchLogService.WriteLine(e.Data);
            Logger.Warn("[MC] " + e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        LaunchLogService.WriteLine($"[启动] 已创建游戏进程，PID {process.Id}");
        return process;
    }

}

