namespace PCL.Core.Settings;

/// <summary>
/// 实例级设置键。对应 VB 版 Settings 中 <c>Source:=Sources.Instance</c> 的定义，
/// 这些键按实例独立存储，不进入全局设置。
/// </summary>
public static class InstanceSettingKeys {

    /// <summary>自定义 JVM 参数。</summary>
    public const string JvmArguments = "VersionAdvanceJvm";

    /// <summary>自定义游戏参数。</summary>
    public const string GameArguments = "VersionAdvanceGame";

    /// <summary>窗口标题模板。</summary>
    public const string WindowTitle = "VersionArgumentTitle";

    /// <summary>Java 选择模式（0/1 自动、2 版本文件夹、3 强制指定）。</summary>
    public const string JavaSelection = "VersionArgumentJavaV2";

    /// <summary>强制指定模式下使用的 Java 路径。</summary>
    public const string JavaSelect = "VersionArgumentJavaSelect";

    /// <summary>「跟随全局设置」的哨兵值，与 VB 保持一致。</summary>
    public const string JavaSelectGlobal = "使用全局设置";

    /// <summary>内存分配方式（0 自动、其他手动）。</summary>
    public const string RamType = "VersionRamType";

    /// <summary>手动内存滑杆值。</summary>
    public const string RamCustom = "VersionRamCustom";

    /// <summary>启动前执行的命令。</summary>
    public const string RunCommand = "VersionAdvanceRun";

    /// <summary>GC 参数预设。</summary>
    public const string Gc = "VersionAdvanceGC";

    /// <summary>是否禁用 JWJGL 安全模式。</summary>
    public const string DisableJwjgl = "VersionAdvanceDisableJLW";

    /// <summary>全部实例级键。</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal) {
        JvmArguments,
        GameArguments,
        WindowTitle,
        JavaSelection,
        JavaSelect,
        RamType,
        RamCustom,
        RunCommand,
        Gc,
        DisableJwjgl
    };

    public static bool IsInstanceLevel(string key) => !string.IsNullOrEmpty(key) && All.Contains(key);

    /// <summary>
    /// 注册所需的键与默认值，默认值取自 VB 版 Settings 的定义。
    /// </summary>
    public static IReadOnlyList<(string Key, object DefaultValue)> Definitions { get; } = [
        (JvmArguments, ""),
        (GameArguments, ""),
        (WindowTitle, ""),
        (JavaSelection, 0),
        (JavaSelect, "使用全局设置"),
        (RamType, 2),
        (RamCustom, 15),
        (RunCommand, ""),
        (Gc, 0),
        (DisableJwjgl, false)
    ];

}
