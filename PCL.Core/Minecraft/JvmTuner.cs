namespace PCL.Core.Minecraft;

/// <summary>一条 JVM 调优建议。</summary>
public sealed class JvmRecommendation {

    /// <summary>推荐的游戏内存（MB）。</summary>
    public int RamMb { get; init; }

    /// <summary>写入 VersionAdvanceGC 的值（0 表示跟随默认）。</summary>
    public int GcOption { get; init; }

    /// <summary>GC 的可读名称。</summary>
    public string GcName { get; init; } = "";

    /// <summary>给用户看的结论。</summary>
    public string Summary { get; init; } = "";

}

/// <summary>
/// 按机器条件推荐 JVM 参数。GC 选项沿用 <see cref="JvmMemoryArgumentBuilder"/> 的语义：
/// 存储值 = setupType + 1，其中 3 表示不添加 GC 参数（交给 JVM 自行选择）。
/// </summary>
public static class JvmTuner {

    public const int MinRamMb = 1024;
    public const int MaxRamMb = 8192;
    public const int ReserveMb = 2048;

    /// <summary>按核心数、物理内存与 Java 主版本给出建议。</summary>
    public static JvmRecommendation Recommend(int cpuCores, long totalMemoryMb, int javaMajor = 17) {
        var cores = Math.Max(1, cpuCores);
        var total = Math.Max((long)MinRamMb, totalMemoryMb);
        var ramMb = RecommendRamMb(total);
        var (gcOption, gcName) = RecommendGc(cores, ramMb, javaMajor);
        return new JvmRecommendation {
            RamMb = ramMb,
            GcOption = gcOption,
            GcName = gcName,
            Summary = $"{cores} 核 / {total / 1024} GB 内存 / Java {javaMajor}"
                      + $" → 建议分配 {ramMb} MB，{gcName}"
        };
    }

    /// <summary>内存建议：取物理内存一半，夹在 [1G, 8G]，并给系统留出余量。</summary>
    public static int RecommendRamMb(long totalMemoryMb) {
        var half = totalMemoryMb / 2;
        var available = totalMemoryMb - ReserveMb;
        var ram = Math.Min(half, available);
        ram = Math.Min(ram, MaxRamMb);
        ram = Math.Max(ram, MinRamMb);
        // 极端小内存机器上不要超过总量
        return (int)Math.Min(ram, totalMemoryMb);
    }

    /// <summary>
    /// GC 建议：多核大内存走 G1 增强；Java 21+ 且内存充足时交给 JVM 默认；
    /// 其余使用标准 G1。
    /// </summary>
    public static (int Option, string Name) RecommendGc(int cpuCores, int ramMb, int javaMajor) {
        if (javaMajor >= 21 && ramMb >= 8192) {
            return (4, "交给 JVM 默认选择");
        }
        if (cpuCores >= 8 && ramMb >= 4096) {
            return (5, "G1 增强调优");
        }
        return (3, "G1");
    }

    /// <summary>把建议转换成可直接写入实例设置的一组键值。</summary>
    public static IReadOnlyDictionary<string, object> ToInstanceSettings(JvmRecommendation recommendation) {
        ArgumentNullException.ThrowIfNull(recommendation);
        return new Dictionary<string, object> {
            // VersionRamType = 2 表示「自定义」
            [PCL.Core.Settings.InstanceSettingKeys.RamType] = 2,
            [PCL.Core.Settings.InstanceSettingKeys.RamCustom] = recommendation.RamMb,
            [PCL.Core.Settings.InstanceSettingKeys.Gc] = recommendation.GcOption
        };
    }

}
