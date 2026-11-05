namespace PCL.Core.Modpack;

/// <summary>
/// 整合包导入的目标路径规划：从 zip 文件名或指定实例名推导安装目录，并做冲突检查。
/// </summary>
public static class ModpackInstallPlanner {

    /// <summary>
    /// 推导实例安装目录。未指定实例名时使用 zip 文件名（去掉扩展名）。
    /// </summary>
    public static string ResolveTargetFolder(string versionsFolder, string zipPath, string? instanceName = null) {
        if (string.IsNullOrWhiteSpace(versionsFolder)) {
            throw new ArgumentException("版本目录不能为空", nameof(versionsFolder));
        }
        if (string.IsNullOrWhiteSpace(zipPath)) {
            throw new ArgumentException("整合包路径不能为空", nameof(zipPath));
        }
        var name = string.IsNullOrWhiteSpace(instanceName)
            ? Path.GetFileNameWithoutExtension(zipPath)
            : instanceName.Trim();
        name = SanitizeInstanceName(name);
        return Path.Combine(versionsFolder, name);
    }

    /// <summary>清理实例名中的非法字符，避免生成越出版本目录的路径。</summary>
    public static string SanitizeInstanceName(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(character => invalid.Contains(character) || character is '/' or '\\' ? '_' : character)
            .ToArray()).Trim();
        return cleaned is "" or "." or ".." ? "整合包" : cleaned;
    }

    /// <summary>检查导入是否可行。可行时返回 <c>null</c>，否则返回错误说明。</summary>
    public static string? Validate(string versionsFolder, string zipPath, string? instanceName = null) {
        if (!File.Exists(zipPath)) return "整合包文件不存在。";
        var target = ResolveTargetFolder(versionsFolder, zipPath, instanceName);
        if (Directory.Exists(target)) return $"目标实例已存在：{Path.GetFileName(target)}";
        return null;
    }

}
