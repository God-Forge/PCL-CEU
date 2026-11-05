using System.IO;
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
        Console.WriteLine("  --update-check                   检查 GitHub 更新");
        Console.WriteLine("  --elevated <任务>                提权 helper 白名单任务");
    }

}


























