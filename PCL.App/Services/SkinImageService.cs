using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using MeloongCore;

namespace PCL.App.Services;

/// <summary>
/// 皮肤图片加载，带内存缓存。网络失败时返回 <c>null</c> 并把失败结果也缓存起来，
/// 避免离线状态下反复发起请求。
/// </summary>
public static class SkinImageService {

    private static readonly Dictionary<string, BitmapSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>已缓存的条目数（含失败结果）。</summary>
    public static int Count {
        get { lock (Cache) return Cache.Count; }
    }

    /// <summary>加载图片。失败返回 <c>null</c>。</summary>
    public static async Task<BitmapSource?> LoadAsync(string? url, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(url)) return null;
        lock (Cache) {
            if (Cache.TryGetValue(url, out var cached)) return cached;
        }
        try {
            var bytes = await Client.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            lock (Cache) Cache[url] = image;
            return image;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "加载皮肤图片失败：" + url);
            lock (Cache) Cache[url] = null;
            return null;
        }
    }

    public static void Clear() {
        lock (Cache) Cache.Clear();
    }

}
