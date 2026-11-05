namespace PCL.Core.Modpack;

/// <summary>
/// 实例导出的范围选项。对应 VB 版 PageInstanceExport 里的一组勾选项。
/// </summary>
public sealed class ModpackExportOptions {

    /// <summary>是否包含 Mods。</summary>
    public bool IncludeMods { get; init; } = true;

    /// <summary>是否包含配置目录。</summary>
    public bool IncludeConfig { get; init; } = true;

    /// <summary>是否包含存档。</summary>
    public bool IncludeSaves { get; init; } = true;

    /// <summary>是否包含截图。</summary>
    public bool IncludeScreenshots { get; init; }

    /// <summary>是否包含日志与崩溃报告（默认不含，体积大且无意义）。</summary>
    public bool IncludeLogs { get; init; }

    /// <summary>是否包含游戏库（libraries）。</summary>
    public bool IncludeLibraries { get; init; } = true;

    /// <summary>是否包含资源文件（assets）。</summary>
    public bool IncludeAssets { get; init; } = true;

}

/// <summary>
/// 决定实例内某个相对路径是否应被打包。纯函数，便于测试。
/// </summary>
public static class ModpackExportFilter {

    /// <summary>
    /// 判断相对路径是否包含在导出范围内。<paramref name="relativePath"/> 使用正斜杠或反斜杠均可。
    /// </summary>
    public static bool ShouldInclude(string? relativePath, ModpackExportOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(relativePath)) return true;
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var separator = normalized.IndexOf('/');
        var top = separator < 0 ? normalized : normalized.Substring(0, separator);
        return top.ToLowerInvariant() switch {
            "logs" or "crash-reports" or "debug" => options.IncludeLogs,
            "screenshots" => options.IncludeScreenshots,
            "saves" or "backups" => options.IncludeSaves,
            "mods" => options.IncludeMods,
            "config" or "defaultconfigs" => options.IncludeConfig,
            _ => true
        };
    }

}
