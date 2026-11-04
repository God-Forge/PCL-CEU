using System.IO;
using MeloongCore;
using PCL.Core.Minecraft;
using PCL.Core.Native;

namespace PCL.App.Services;

/// <summary>
/// 启动参数解析：把设置项与实例信息换算成 <see cref="LaunchService"/> 需要的参数。
/// </summary>
public static class LaunchParameterService {

    /// <summary>设置键：0 为自动分配内存，其他为手动。</summary>
    public const string RamTypeKey = "LaunchRamType";

    /// <summary>设置键：手动模式的滑杆值。</summary>
    public const string RamCustomKey = "LaunchRamCustom";

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

    /// <summary>按当前设置与实例算出分配给游戏的内存（MB）。</summary>
    public static int ResolveRamMb(MinecraftInstanceLocation instance) {
        var settings = AppServices.Settings;
        var ramType = settings.Get<int>(RamTypeKey);
        var customValue = settings.Get<int>(RamCustomKey);
        var instanceFolder = Path.Combine(instance.Folder.Versions, instance.Version.Folder);
        var modCount = CountMods(instanceFolder);
        return MemoryAllocator.ResolveRamMb(
            ramType,
            customValue,
            AvailableMemoryGigabytes(),
            modCount,
            modable: modCount > 0);
    }

}
