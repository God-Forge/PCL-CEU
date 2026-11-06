namespace PCL.Core.Minecraft;

/// <summary>
/// 从 OptiFine 安装器文件名推导安装后生成的版本目录名。
/// 例如 <c>OptiFine_1.20.1_HD_U_I6.jar</c> → <c>1.20.1_OptiFine_HD_U_I6</c>。
/// </summary>
public static class OptiFineInstallerName {

    private const string Prefix = "OptiFine_";
    private const string PreviewPrefix = "preview_";

    /// <summary>
    /// 推导版本目录名。无法识别时返回 <c>null</c>。
    /// </summary>
    public static string? BuildVersionName(string? installerFileName, string? minecraftVersion) {
        if (string.IsNullOrWhiteSpace(installerFileName)) return null;
        if (string.IsNullOrWhiteSpace(minecraftVersion)) return null;
        var name = Path.GetFileNameWithoutExtension(installerFileName.Trim());
        if (name.Length == 0) return null;
        if (name.StartsWith(PreviewPrefix, StringComparison.OrdinalIgnoreCase)) {
            name = name[PreviewPrefix.Length..];
        }
        if (!name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        name = name[Prefix.Length..];
        var versionPrefix = minecraftVersion.Trim() + "_";
        if (name.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase)) {
            name = name[versionPrefix.Length..];
        }
        if (string.IsNullOrWhiteSpace(name)) return null;
        return $"{minecraftVersion.Trim()}_OptiFine_{name}";
    }

    /// <summary>构造官方下载地址（用于手动安装或后续自动下载）。</summary>
    public static string? BuildDownloadUrl(string? installerFileName, string? officialBase = null) {
        if (string.IsNullOrWhiteSpace(installerFileName)) return null;
        var baseUrl = string.IsNullOrWhiteSpace(officialBase) ? "https://optifine.net/adloadx?f=" : officialBase;
        return baseUrl + Uri.EscapeDataString(Path.GetFileName(installerFileName.Trim()));
    }

}
