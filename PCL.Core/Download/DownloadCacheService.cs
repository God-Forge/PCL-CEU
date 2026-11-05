using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.Download;

/// <summary>
/// 下载文件的本地缓存：按 URL 生成稳定文件名，命中时直接复用，
/// 避免整合包与依赖安装过程中重复下载同一文件。
/// 对应 VB 版 ModNet 里借助 CacheCow 实现的缓存行为。
/// </summary>
public sealed class DownloadCacheService {

    private readonly string _folder;

    public DownloadCacheService(string cacheFolder) {
        if (string.IsNullOrWhiteSpace(cacheFolder)) {
            throw new ArgumentException("缓存目录不能为空", nameof(cacheFolder));
        }
        _folder = cacheFolder;
    }

    public string Folder => _folder;

    /// <summary>把 URL 映射成缓存文件路径。同名 URL 始终得到相同路径。</summary>
    public string GetCachePath(string? url) {
        if (string.IsNullOrWhiteSpace(url)) {
            throw new ArgumentException("地址不能为空", nameof(url));
        }
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim().ToLowerInvariant())))
            .ToLowerInvariant();
        return Path.Combine(_folder, hash[..32] + SafeExtension(url));
    }

    /// <summary>缓存命中时返回路径，否则返回 <c>false</c>。</summary>
    public bool TryGet(string? url, out string path) {
        path = "";
        if (string.IsNullOrWhiteSpace(url)) return false;
        try {
            var candidate = GetCachePath(url);
            if (!File.Exists(candidate) || new FileInfo(candidate).Length == 0) return false;
            path = candidate;
            return true;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "检查下载缓存失败：" + url);
            return false;
        }
    }

    /// <summary>把已下载的文件存入缓存，返回缓存路径。</summary>
    public string Store(string url, string sourcePath) {
        if (!File.Exists(sourcePath)) {
            throw new FileNotFoundException("待缓存的文件不存在", sourcePath);
        }
        var target = GetCachePath(url);
        Directory.CreateDirectory(_folder);
        var temp = target + ".tmp";
        File.Copy(sourcePath, temp, true);
        // 先写临时文件再替换，避免并发下载读到写了一半的缓存
        if (File.Exists(target)) File.Delete(target);
        File.Move(temp, target);
        return target;
    }

    /// <summary>当前缓存占用的总字节数。</summary>
    public long GetSize() {
        if (!Directory.Exists(_folder)) return 0;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(_folder, "*", SearchOption.TopDirectoryOnly)) {
            if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
            try {
                total += new FileInfo(file).Length;
            } catch {
                // 文件可能被并发删除
            }
        }
        return total;
    }

    /// <summary>
    /// 清理缓存到不超过 <paramref name="maxBytes"/>：按最后访问时间从旧到新删除。
    /// 返回删除的文件数。
    /// </summary>
    public int Prune(long maxBytes) {
        if (maxBytes < 0) maxBytes = 0;
        if (!Directory.Exists(_folder)) return 0;
        var files = Directory.EnumerateFiles(_folder, "*", SearchOption.TopDirectoryOnly)
            .Where(file => !file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .Select(file => new FileInfo(file))
            .OrderBy(info => info.LastAccessTimeUtc)
            .ToList();
        var total = files.Sum(info => info.Length);
        var removed = 0;
        foreach (var info in files) {
            if (total <= maxBytes) break;
            try {
                var length = info.Length;
                info.Delete();
                total -= length;
                removed++;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "清理缓存文件失败：" + info.FullName);
            }
        }
        return removed;
    }

    private static string SafeExtension(string url) {
        try {
            var path = new Uri(url).AbsolutePath;
            var extension = Path.GetExtension(path);
            if (extension.Length is < 2 or > 12) return "";
            return extension.All(character => char.IsLetterOrDigit(character) || character == '.') ? extension : "";
        } catch {
            return "";
        }
    }

}
