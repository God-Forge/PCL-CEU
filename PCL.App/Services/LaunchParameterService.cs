using System.IO;
using MeloongCore;
using PCL.Core.Minecraft;
using PCL.Core.Native;
using PCL.Core.Settings;

namespace PCL.App.Services;

/// <summary>
/// 启动参数解析：把设置项与实例信息换算成 <see cref="LaunchService"/> 需要的参数。
/// </summary>
public static class LaunchParameterService {

    /// <summary>设置键：0 为自动分配内存，其他为手动。</summary>
    public const string RamTypeKey = "LaunchRamType";

    /// <summary>设置键：手动模式的滑杆值。</summary>
    public const string RamCustomKey = "LaunchRamCustom";

    /// <summary>设置键：窗口类型（0 全屏 / 1 默认 / 2 跟随启动器 / 3 自定义 / 4 最大化）。</summary>
    public const string WindowTypeKey = "LaunchArgumentWindowType";

    /// <summary>设置键：自定义窗口宽度。</summary>
    public const string WindowWidthKey = "LaunchArgumentWindowWidth";

    /// <summary>设置键：自定义窗口高度。</summary>
    public const string WindowHeightKey = "LaunchArgumentWindowHeight";

    /// <summary>当前设置的窗口类型。</summary>
    public static int ResolveWindowType() => AppServices.Settings.Get<int>(WindowTypeKey);

    /// <summary>设置键：游戏窗口标题模板，支持 {date} / {time}。</summary>
    public const string WindowTitleKey = "LaunchArgumentTitle";

    /// <summary>设置键：用户自定义的 JVM 参数。</summary>
    public const string ExtraJvmKey = "LaunchAdvanceJvm";

    /// <summary>窗口标题模板，未设置时返回 <c>null</c>。实例设置优先，其次全局。</summary>
    public static string? ResolveWindowTitle(MinecraftInstanceLocation? instance = null) {
        var title = instance is null
            ? AppServices.Settings.Get<string>(WindowTitleKey)
            : ResolveInstanceSetting(instance, InstanceSettingKeys.WindowTitle,
                () => AppServices.Settings.Get<string>(WindowTitleKey));
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    /// <summary>用户自定义的 JVM 参数，按空格切分。实例设置优先，其次全局。</summary>
    public static IReadOnlyList<string> ResolveExtraJvmArguments(MinecraftInstanceLocation? instance = null) {
        var raw = instance is null
            ? AppServices.Settings.Get<string>(ExtraJvmKey)
            : ResolveInstanceSetting(instance, InstanceSettingKeys.JvmArguments,
                () => AppServices.Settings.Get<string>(ExtraJvmKey));
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return JavaArgumentParser.Split(raw);
    }

    /// <summary>
    /// 启动前要执行的命令（VersionAdvanceRun），已展开占位符。
    /// 没有配置时返回空串。
    /// </summary>
    public static string ResolvePreLaunchCommand(MinecraftInstanceLocation? instance = null) {
        if (instance is null) return "";
        var raw = ResolveInstanceSetting(instance, InstanceSettingKeys.RunCommand, () => "");
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var context = new PCL.Core.Launch.LaunchCommandContext {
            LauncherPath = AppServices.Paths.Base,
            MinecraftPath = AppServices.Paths.Minecraft,
            InstancePath = InstancePath(instance),
            PlayerName = AppServices.CurrentLogin?.Name ?? ""
        };
        return PCL.Core.Launch.LaunchCommandExpander.Expand(raw, context);
    }

    /// <summary>启动前命令是否等待执行完成（VersionAdvanceRunWait，默认等待）。</summary>
    public static bool ResolvePreLaunchWait(MinecraftInstanceLocation? instance = null) =>
        instance is null || ResolveInstanceSetting(instance, InstanceSettingKeys.RunWait, () => true);

    /// <summary>实例自定义游戏参数（VersionAdvanceGame），按空格切分。</summary>
    public static IReadOnlyList<string> ResolveExtraGameArguments(MinecraftInstanceLocation? instance = null) {
        if (instance is null) return [];
        var raw = ResolveInstanceSetting(instance, InstanceSettingKeys.GameArguments, () => "");
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return JavaArgumentParser.Split(raw);
    }

    /// <summary>实例的版本目录（实例级设置的存放位置）。</summary>
    public static string InstancePath(MinecraftInstanceLocation instance) =>
        Path.Combine(instance.Folder.Versions, instance.Version.Folder);

    /// <summary>
    /// 读取实例级设置：实例没有显式保存过时回退到全局值。
    /// 底层由 SettingsService 的 SettingSource.Instance 支持，写入 &lt;实例&gt;\PCL\Setup.ini。
    /// </summary>
    public static T ResolveInstanceSetting<T>(MinecraftInstanceLocation instance, string key, Func<T> globalFallback) {
        var path = InstancePath(instance);
        if (!AppServices.Settings.HasSaved(key, path)) return globalFallback();
        var value = AppServices.Settings.Get<T>(key, path);
        return value is null ? globalFallback() : value;
    }

    /// <summary>按设置与启动器当前尺寸算出游戏窗口尺寸。</summary>
    public static (int Width, int Height) ResolveWindowSize() {
        var settings = AppServices.Settings;
        var type = settings.Get<int>(WindowTypeKey);
        var customWidth = settings.Get<int>(WindowWidthKey);
        var customHeight = settings.Get<int>(WindowHeightKey);
        var (launcherWidth, launcherHeight, dpi) = LauncherWindowMetrics();
        var size = WindowSizeResolver.Resolve(type, customWidth, customHeight, launcherWidth, launcherHeight, dpi);
        return (size.Width, size.Height);
    }

    private static (int Width, int Height, double Dpi) LauncherWindowMetrics() {
        try {
            if (System.Windows.Application.Current?.MainWindow is { } window && window.ActualWidth > 0 && window.ActualHeight > 0) {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window).DpiScaleX;
                return ((int)window.ActualWidth, (int)window.ActualHeight, dpi);
            }
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取启动器窗口尺寸失败，按默认值计算");
        }
        return (WindowSizeResolver.DefaultWidth, WindowSizeResolver.DefaultHeight, 1);
    }

    /// <summary>当前可用物理内存（GB）。</summary>
    public static double AvailableMemoryGigabytes() {
        try {
            var status = NativeMethods.GetMemoryStatus();
            return status.AvailablePhysical / 1024.0 / 1024 / 1024;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取可用内存失败，按 4 GB 计算");
            return 4;
        }
    }

    /// <summary>统计实例 mods 目录下的 Mod 数量（含禁用）。</summary>
    public static int CountMods(string instanceFolder) {
        try {
            var modsFolder = Path.Combine(instanceFolder, "mods");
            if (!Directory.Exists(modsFolder)) return 0;
            return Directory.EnumerateFiles(modsFolder, "*", SearchOption.TopDirectoryOnly)
                .Count(file => {
                    var extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension == ".disabled") {
                        extension = Path.GetExtension(Path.GetFileNameWithoutExtension(file)).ToLowerInvariant();
                    }
                    return extension is ".jar" or ".zip" or ".litemod";
                });
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "统计 Mod 数量失败");
            return 0;
        }
    }

    /// <summary>按当前设置与实例算出分配给游戏的内存（MB）。实例设置优先，其次全局。</summary>
    public static int ResolveRamMb(MinecraftInstanceLocation instance) {
        var ramType = ResolveInstanceSetting(instance, InstanceSettingKeys.RamType,
            () => AppServices.Settings.Get<int>(RamTypeKey));
        var customValue = ResolveInstanceSetting(instance, InstanceSettingKeys.RamCustom,
            () => AppServices.Settings.Get<int>(RamCustomKey));
        var instanceFolder = InstancePath(instance);
        var modCount = CountMods(instanceFolder);
        return MemoryAllocator.ResolveRamMb(
            ramType,
            customValue,
            AvailableMemoryGigabytes(),
            modCount,
            modable: modCount > 0);
    }

}
