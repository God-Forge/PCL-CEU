namespace PCL.Core.Java;

/// <summary>
/// Java 选择模式，对应设置项 VersionArgumentJavaV2。
/// </summary>
public enum JavaSelectionMode {
    /// <summary>按版本要求自动选择。</summary>
    Automatic,
    /// <summary>使用版本文件夹内的 Java。</summary>
    VersionFolder,
    /// <summary>使用版本设置中强制指定的 Java。</summary>
    Forced
}

/// <summary>
/// 把设置值解析为 Java 选择模式。VB 版取值：0/1 自动、2 版本文件夹、3 强制指定。
/// </summary>
public static class JavaSelectionModeResolver {

    public static JavaSelectionMode Resolve(int setting) => setting switch {
        2 => JavaSelectionMode.VersionFolder,
        3 => JavaSelectionMode.Forced,
        _ => JavaSelectionMode.Automatic
    };

    /// <summary>该模式是否需要扫描版本文件夹。</summary>
    public static bool RequiresVersionFolderScan(JavaSelectionMode mode) => mode == JavaSelectionMode.VersionFolder;

}
