namespace PCL.Core.Minecraft;

/// <summary>自动分配内存时的四档参考值（单位：GB）。</summary>
public readonly record struct MemoryTargets(double Minimum, double Target1, double Target2, double Target3);

/// <summary>
/// 启动内存分配。由 VB 版 PageSetupLaunch.GetRam 迁移而来：
/// 自动模式按可用内存分阶段分配，手动模式把设置滑杆映射为 GB。
/// </summary>
public static class MemoryAllocator {

    /// <summary>滑杆上限（对应约 150 GB）。</summary>
    public const int CustomSliderMaximum = 100;

    /// <summary>手动模式：设置滑杆值映射为 GB。</summary>
    public static double FromCustomSlider(int value) {
        var v = Math.Clamp(value, 0, CustomSliderMaximum);
        var gigabytes = v switch {
            <= 12 => v * 0.1 + 0.3,
            <= 25 => (v - 12) * 0.5 + 1.5,
            <= 33 => (v - 25) * 1.0 + 8,
            _ => (v - 33) * 2.0 + 16
        };
        return Math.Round(gigabytes, 1);
    }

    /// <summary>把 GB 转成启动参数需要的 MB。</summary>
    public static int ToMegabytes(double gigabytes) => (int)Math.Round(Math.Max(0, gigabytes) * 1024);

    /// <summary>
    /// 根据设置决定启动内存：<paramref name="ramType"/> 为 0 时自动分配，否则按滑杆值换算。
    /// </summary>
    public static int ResolveRamMb(
        int ramType,
        int customValue,
        double availableGigabytes,
        int modCount = 0,
        bool modable = false,
        bool hasOptiFine = false) {
        var gigabytes = ramType == 0
            ? CalculateAutoGigabytes(availableGigabytes, modCount, modable, hasOptiFine)
            : FromCustomSlider(customValue);
        return ToMegabytes(gigabytes);
    }

    /// <summary>
    /// 按版本类型与 Mod 数量估算四档内存需求。
    /// </summary>
    public static MemoryTargets EstimateTargets(int modCount, bool modable, bool hasOptiFine) {
        var count = Math.Max(0, modCount);
        if (modable) {
            return new MemoryTargets(
                0.5 + count / 150.0,
                1.5 + count / 90.0,
                2.7 + count / 50.0,
                4.5 + count / 25.0);
        }
        return hasOptiFine
            ? new MemoryTargets(0.5, 1.5, 3, 5)
            : new MemoryTargets(0.5, 1.5, 2.5, 4);
    }

    /// <summary>
    /// 自动分配：按可用内存分阶段填充，
    /// 第一档全给、第二档 70%、第三档 40%、第四档 15%，最后不低于最低需求。
    /// </summary>
    public static double CalculateAutoGigabytes(
        double availableGigabytes,
        int modCount = 0,
        bool modable = false,
        bool hasOptiFine = false) {
        if (availableGigabytes <= 0) return 0;
        var available = Math.Round(availableGigabytes * 10) / 10;
        var targets = EstimateTargets(modCount, modable, hasOptiFine);
        var give = 0.0;

        give += Math.Min(available, targets.Target1);
        available -= targets.Target1;
        if (available >= 0.1) {
            var delta = targets.Target2 - targets.Target1;
            give += Math.Min(available * 0.7, delta);
            available -= delta / 0.7;
        }
        if (available >= 0.1) {
            var delta = targets.Target3 - targets.Target2;
            give += Math.Min(available * 0.4, delta);
            available -= delta / 0.4;
        }
        if (available >= 0.1) {
            give += Math.Min(available * 0.15, targets.Target3);
        }
        return Math.Round(Math.Max(give, targets.Minimum), 1);
    }

}
