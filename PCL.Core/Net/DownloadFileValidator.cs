namespace PCL.Core.Net;

/// <summary>
/// 校验下载任务中的 URL 与本地文件路径。
/// </summary>
public static class DownloadFileValidator {

    public static bool IsHttpUrl(string? url) {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public static string NormalizeLocalPath(string path) {
        var normalized = path.Replace('/', '\\');
        if (normalized.IndexOf(":\\", StringComparison.Ordinal) < 0) {
            throw new ArgumentException("输入的本地文件地址不正确：" + normalized);
        }
        if (normalized.EndsWith("\\", StringComparison.Ordinal)) {
            throw new ArgumentException("请输入含文件名的完整文件路径：" + normalized);
        }
        return normalized;
    }

}
