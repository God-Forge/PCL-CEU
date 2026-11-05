using System.IO;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Download;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Memory;
using PCL.Core.Modpack;
using PCL.Core.Update;
using PCL.Core.Minecraft;
using PCL.Core.Mods;
using PCL.Core.Net;
using PCL.Core.Paths;
using PCL.Core.Tools;
using PCL.Core.Worlds;

namespace PCL.App.Cli;

/// <summary>
/// 迁移期 CLI：先暴露已迁移的核心服务，后续逐项补齐与 VB CLI 对等。
/// </summary>
public static class CliCommandService {

    public static int? Run(string[] args) {
        if (args.Length == 0) return null;
        try {
            return RunCore(args);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            // 参数错误、文件损坏等不应把堆栈喷给用户
            Console.Error.WriteLine("命令执行失败：" + ex.Message);
            Logger.Error(ex, "CLI 命令失败：" + string.Join(" ", args));
            return 1;
        }
    }

    private static int? RunCore(string[] args) {
        var command = args[0].ToLowerInvariant();
        var paths = AppServices.Paths;
        switch (command) {
            case "--version":
            case "-v":
                Console.WriteLine("Plain Craft Launcher (PCL) CEU 修改版");
                Console.WriteLine("版本：0.24.0-ceu");
                Console.WriteLine("程序集：" + typeof(CliCommandService).Assembly.GetName().Version);
                return 0;
            case "--help":
            case "-h":
            case "/?":
                PrintHelp();
                return 0;
            case "--list":
                foreach (var version in MinecraftVersionScanner.Scan(paths.Base)) {
                    Console.WriteLine($"{version.Id}\t{version.Type}\t{version.LibrariesCount} libs{(version.Error is null ? "" : "\t" + version.Error)}");
                }
                return 0;
            case "--mods":
                if (args.Length < 2) return MissingArgument("--mods <实例名>");
                PrintMods(paths, args[1]);
                return 0;
            case "--payload-encode":
                if (args.Length < 3) return MissingArgument("--payload-encode <文件夹> <输出.payload> [名称]");
                Console.WriteLine("已打包：" + PCL.Core.Tools.PayloadService.Encode(
                    paths.Base, args[1], args[2], args.Length > 3 ? args[3] : null));
                return 0;
            case "--payload-decode":
                if (args.Length < 3) return MissingArgument("--payload-decode <文件.payload> <输出文件夹>");
                Console.WriteLine("已解包到：" + PCL.Core.Tools.PayloadService.Decode(paths.Base, args[1], args[2]));
                return 0;
            case "--javapkg-encode":
                if (args.Length < 3) return MissingArgument("--javapkg-encode <Java 目录> <输出.javapkg>");
                var packageInfo = PCL.Core.Java.JavaPackageService.CreatePackage(args[1], args[2]);
                Console.WriteLine($"已打包：{args[2]}（Java {packageInfo.JavaVersion}，{packageInfo.Arch}）");
                return 0;
            case "--javapkg-info":
                if (args.Length < 2) return MissingArgument("--javapkg-info <文件.javapkg>");
                var info = PCL.Core.Java.JavaPackageService.ReadInfo(args[1]);
                Console.WriteLine($"Java 版本：{info.JavaVersion}");
                Console.WriteLine($"类型：{info.Type}");
                Console.WriteLine($"架构：{info.Arch}（{info.Os}）");
                Console.WriteLine($"厂商：{info.Vendor}");
                Console.WriteLine($"大小：{info.Size / 1024 / 1024} MB");
                return 0;
            case "--crash-analyze":
                if (args.Length < 2) return MissingArgument("--crash-analyze <实例名>");
                var crashInstance = Path.Combine(paths.Versions, args[1]);
                var crashReport = PCL.Core.Minecraft.CrashAnalysisService.AnalyzeFiles(
                    PCL.Core.Minecraft.CrashAnalysisService.LocateCrashLogs(crashInstance));
                Console.WriteLine(PCL.Core.Minecraft.CrashAnalysisService.Format(crashReport));
                return crashReport.HasFindings ? 1 : 0;
            case "--health":
                if (args.Length < 2) return MissingArgument("--health <实例名>");
                Console.WriteLine(AdvancedAuditService.FormatReport(
                    Path.Combine(paths.Versions, args[1]),
                    paths.Base,
                    Path.Combine(paths.Pcl, "LaunchTiming.json")));
                return 0;
            case "--diagnostics":
                if (args.Length < 2) return MissingArgument("--diagnostics <实例名> [输出.zip]");
                CreateDiagnostics(paths, args);
                return 0;
            case "--luck":
                var luck = DailyLuck.GetToday();
                Console.WriteLine($"今日人品：{luck.Value} / 100");
                Console.WriteLine(luck.Comment);
                return 0;
            case "--memory":
                var memory = MemoryOptimizer.Optimize(MemoryOptimizationKind.CurrentProcess);
                Console.WriteLine(memory.Message);
                Console.WriteLine($"释放：{memory.FreedMB:F1} MB");
                return memory.Success ? 0 : 1;
            case "--speedtest":
                var results = new DownloadSourceSpeedTester().TestAllAsync(DownloadSourceSpeedTester.DefaultTargets()).GetAwaiter().GetResult();
                Console.WriteLine(DownloadSourceSpeedTester.Format(results));
                return 0;
            case "--snapshot":
                if (args.Length < 2) return MissingArgument("--snapshot <实例名>");
                var snapshot = InstanceSnapshotService.Create(
                    Path.Combine(paths.Versions, args[1]),
                    Path.Combine(paths.Pcl, "InstanceSnapshots"));
                Console.WriteLine("快照已创建：" + snapshot.Path);
                return 0;
            case "--snapshot-list":
                if (args.Length < 2) return MissingArgument("--snapshot-list <实例名>");
                foreach (var item in InstanceSnapshotService.List(
                             Path.Combine(paths.Versions, args[1]),
                             Path.Combine(paths.Pcl, "InstanceSnapshots"))) {
                    Console.WriteLine($"{item.Created:yyyy-MM-dd HH:mm}\t{item.Name}\t{item.Size} bytes");
                }
                return 0;
            case "--backup":
                if (args.Length < 3) return MissingArgument("--backup <实例名> <世界名>");
                var backup = new WorldBackupService().CreateBackup(
                    Path.Combine(paths.Versions, args[1], "saves", args[2]),
                    Path.Combine(paths.Pcl, "WorldBackups"),
                    args[1],
                    args[2]);
                Console.WriteLine("世界备份已创建：" + backup.Path);
                return 0;
            case "--elevated":
                if (args.Length < 2) return MissingArgument("--elevated <memory|memory:...>");
                return ElevatedTaskService.Run(args[1]);
            case "--update-check":
                var update = UpdateCheckerService.CheckAsync(
                    new HttpStringFetcher(),
                    "https://api.github.com/repos/God-Forge/PCL-CEU/releases/latest",
                    "0.24.0").GetAwaiter().GetResult();
                Console.WriteLine($"当前：{update.CurrentVersion}，最新：{update.LatestVersion}，有更新：{update.HasUpdate}");
                return 0;
            case "--login":
                if (args.Length < 2) return MissingArgument("--login <离线用户名>");
                var profile = OfflineLoginService.Create(args[1]);
                Console.WriteLine($"{profile.Name}\t{profile.Uuid}\t{profile.AccessToken}");
                return 0;
            case "--modpack":
                if (args.Length < 3) return MissingArgument("--modpack <整合包.zip> <实例名>");
                var installed = ModpackInstallService.InstallOverrides(args[1], Path.Combine(paths.Versions, args[2]));
                Console.WriteLine($"已导入 {installed} 个 overrides 文件。");
                return 0;
            case "--launch":
                if (args.Length < 2) return MissingArgument("--launch <实例名>");
                var location = AppServices.MinecraftFolders.FindInstance(args[1]);
                if (location is null) {
                    Console.WriteLine("未找到实例：" + args[1]);
                    return 1;
                }
                var java = JavaSelectionService.Resolve(location);
                if (java is null) {
                    Console.WriteLine("未找到可用 Java。");
                    return 1;
                }
                var loginProfile = AppServices.CurrentLogin ?? OfflineLoginService.Create(Environment.UserName);
                var session = LaunchService.Launch(location.Folder.Path,
                    Path.Combine(location.Folder.Versions, location.Version.Folder),
                    location.Version.Folder, location.Version.JsonPath, loginProfile, java.JavaExe,
                    LaunchParameterService.ResolveRamMb(location),
                    LaunchParameterService.ResolveWindowSize().Width,
                    LaunchParameterService.ResolveWindowSize().Height,
                    LaunchParameterService.ResolveWindowType(),
                    null,
                    LaunchParameterService.ResolveWindowTitle(location),
                    LaunchParameterService.ResolveExtraJvmArguments(location),
                    LaunchParameterService.ResolveExtraGameArguments(location));
                Console.WriteLine($"已启动 {location.Version.Id}，进程 {session.Process.Id}，Java {java.Version}");
                return 0;
            case "--page-smoke":
                return PageSmokeService.Run();
            case "--install":
                if (args.Length < 2) return MissingArgument("--install <版本>");
                var folder = new VanillaInstallService().InstallAsync(paths.Base, args[1]).GetAwaiter().GetResult();
                Console.WriteLine("安装完成：" + folder);
                return 0;
            case "--modpack-import":
                if (args.Length < 2) return MissingArgument("--modpack-import <整合包.zip> [实例名]");
                var zipPath = Path.GetFullPath(args[1]);
                var instanceName = args.Length > 2 ? args[2] : null;
                var error = ModpackInstallPlanner.Validate(paths.Versions, zipPath, instanceName);
                if (error is not null) {
                    Console.WriteLine(error);
                    return 1;
                }
                var targetFolder = ModpackInstallPlanner.ResolveTargetFolder(paths.Versions, zipPath, instanceName);
                Console.WriteLine($"正在导入整合包到：{targetFolder}");
                var modpackResult = new ModpackInstaller()
                    .InstallAsync(zipPath, targetFolder)
                    .GetAwaiter()
                    .GetResult();
                Console.WriteLine(modpackResult.Message);
                return modpackResult.Success ? 0 : 1;
            default:
                Console.WriteLine("未知命令：" + args[0]);
                PrintHelp();
                return 2;
        }
    }

    private static void PrintMods(PclPathService paths, string instanceName) {
        var result = ModScanner.Scan(Path.Combine(paths.Versions, instanceName, "mods"));
        foreach (var mod in result.Mods) {
            Console.WriteLine($"{mod.FileName}\t{mod.Loader}\t{mod.ModId}\t{mod.Version}\t{(mod.Enabled ? "启用" : "禁用")}");
        }
        foreach (var issue in result.Issues) {
            Console.WriteLine($"[{issue.Severity}] {issue.FileName}: {issue.Message}");
        }
    }

    private static void CreateDiagnostics(PclPathService paths, string[] args) {
        var instanceFolder = Path.Combine(paths.Versions, args[1]);
        var output = args.Length >= 3
            ? args[2]
            : Path.Combine(paths.Pcl, "Diagnostics", args[1] + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
        var result = DiagnosticPackageService.Create(instanceFolder, paths.Base, output);
        Console.WriteLine("诊断包已生成：" + result.Path);
        Console.WriteLine($"文件数：{result.FileCount}，大小：{result.Size} bytes");
    }

    private static int MissingArgument(string usage) {
        Console.WriteLine("缺少参数，用法：" + usage);
        return 2;
    }

    private static void PrintHelp() {
        Console.WriteLine("PCL CEU C# / .NET 8 CLI");
        Console.WriteLine("  --version, -v                    显示版本");
        Console.WriteLine("  --help, -h, /?                   显示帮助");
        Console.WriteLine("  --list                           列出实例");
        Console.WriteLine("  --mods <实例名>                  扫描 Mod");
        Console.WriteLine("  --health <实例名>                实例体检");
        Console.WriteLine("  --crash-analyze <实例名>          分析实例崩溃日志");
        Console.WriteLine("  --payload-encode <文件夹> <输出>   打包为 .payload");
        Console.WriteLine("  --payload-decode <文件> <文件夹>   解开 .payload");
        Console.WriteLine("  --javapkg-encode <Java 目录> <输出> 打包 Java");
        Console.WriteLine("  --javapkg-info <文件>             查看 Java 包信息");
        Console.WriteLine("  --diagnostics <实例名> [输出]    导出诊断包");
        Console.WriteLine("  --luck                           今日人品");
        Console.WriteLine("  --memory                         清理当前进程工作集");
        Console.WriteLine("  --speedtest                      下载源测速");
        Console.WriteLine("  --snapshot <实例名>              创建实例快照");
        Console.WriteLine("  --snapshot-list <实例名>         列出实例快照");
        Console.WriteLine("  --backup <实例名> <世界名>       备份世界");
        Console.WriteLine("  --page-smoke                     页面加载冒烟");
        Console.WriteLine("  --install <版本>                 安装原版");
        Console.WriteLine("  --launch <实例名>                启动实例");
        Console.WriteLine("  --login <用户名>                 离线登录信息");
        Console.WriteLine("  --modpack <zip> <实例名>         导入整合包 overrides");
        Console.WriteLine("  --modpack-import <zip> [实例名]  完整导入整合包（含文件下载）");
        Console.WriteLine("  --update-check                   检查 GitHub 更新");
        Console.WriteLine("  --elevated <任务>                提权 helper 白名单任务");
    }

}


























