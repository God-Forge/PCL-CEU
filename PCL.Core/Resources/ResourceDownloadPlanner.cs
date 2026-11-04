using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>
/// 把资源版本转换为下载请求。由 VB 版 <c>ResourceVersion.ToNetFile</c> 迁移而来。
/// </summary>
public static class ResourceDownloadPlanner {

    /// <summary>
    /// 生成下载请求。资源缺少下载地址或文件名时返回 <c>null</c>。
    /// </summary>
    /// <param name="version">资源版本。</param>
    /// <param name="targetFolder">下载目标目录（通常是实例的 mods/resourcepacks 等）。</param>
    /// <param name="fileNameOverride">覆盖文件名，为空则使用资源自带文件名。</param>
    public static DownloadRequest? CreateRequest(ResourceVersionFile version, string targetFolder, string? fileNameOverride = null) {
        ArgumentNullException.ThrowIfNull(version);
        if (string.IsNullOrWhiteSpace(targetFolder)) {
            throw new ArgumentException("目标目录不能为空", nameof(targetFolder));
        }
        if (!version.DownloadAvailable) return null;
        var fileName = SanitizeFileName(string.IsNullOrWhiteSpace(fileNameOverride) ? version.FileName : fileNameOverride);
        if (fileName.Length == 0) return null;
        var urls = version.DownloadUrls.Where(url => !string.IsNullOrWhiteSpace(url)).ToList();
        if (urls.Count == 0) return null;
        return new DownloadRequest {
            Url = urls[0],
            BackupUrls = urls.Skip(1).ToList(),
            TargetPath = Path.Combine(targetFolder, fileName),
            Overwrite = true,
            AllowResume = true,
            MinSize = version.Size > 0 ? version.Size : null,
            MaxRetries = 3
        };
    }

    /// <summary>
    /// 清理文件名中的非法字符，防止路径穿越。
    /// </summary>
    public static string SanitizeFileName(string? name) {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        if (cleaned is "." or "..") return "";
        // 即使用户文件名里带了路径分隔符，也已被替换为下划线
        return cleaned;
    }

}
