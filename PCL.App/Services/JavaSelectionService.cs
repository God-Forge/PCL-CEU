using System.IO;
using MeloongCore;
using PCL.Core.Java;
using PCL.Core.Minecraft;
using PCL.Core.Settings;

namespace PCL.App.Services;

/// <summary>
/// 按设置选择启动用的 Java。对应 VB 版 VersionArgumentJavaV2 的三种模式。
/// </summary>
public static class JavaSelectionService {

    /// <summary>设置键：Java 选择模式。</summary>
    public const string ModeKey = "VersionArgumentJavaV2";

    /// <summary>选择模式：实例设置优先，其次全局。</summary>
    public static JavaSelectionMode ResolveMode(MinecraftInstanceLocation? instance = null) {
        var raw = instance is null
            ? AppServices.Settings.Get<int>(ModeKey)
            : LaunchParameterService.ResolveInstanceSetting(instance, InstanceSettingKeys.JavaSelection,
                () => AppServices.Settings.Get<int>(ModeKey));
        return JavaSelectionModeResolver.Resolve(raw);
    }

    /// <summary>
    /// 为实例挑选 Java。返回 <c>null</c> 表示没找到可用的 Java。
    /// 强制指定模式尚未支持，会回退到自动选择。
    /// </summary>
    public static JavaInstallation? Resolve(MinecraftInstanceLocation instance) {
        var mode = ResolveMode(instance);
        if (mode == JavaSelectionMode.Forced) {
            var configured = LaunchParameterService.ResolveInstanceSetting(instance, InstanceSettingKeys.JavaSelect, () => "");
            if (string.IsNullOrWhiteSpace(configured) || configured == InstanceSettingKeys.JavaSelectGlobal) {
                Logger.Warn($"实例 {instance.Version.Folder} 设置了强制指定 Java，但未选择具体路径，回退到自动选择");
            } else {
                var forced = JavaScanner.Inspect(configured);
                if (forced is not null) return forced;
                Logger.Warn($"实例 {instance.Version.Folder} 强制指定的 Java 不可用（{configured}），回退到自动选择");
            }
        }
        if (JavaSelectionModeResolver.RequiresVersionFolderScan(mode)) {
            var versionFolder = Path.Combine(instance.Folder.Versions, instance.Version.Folder);
            var local = JavaScanner.Scan([versionFolder]).FirstOrDefault();
            if (local is not null) return local;
            Logger.Warn($"版本 {instance.Version.Folder} 设置使用版本文件夹内的 Java，但未找到，回退到自动选择");
        }
        var roots = JavaScanner.DefaultRoots(
            AppServices.Paths.Base,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        return JavaScanner.FindBest(roots, instance.Version.JavaVersionMajor);
    }

}
