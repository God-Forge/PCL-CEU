using System.IO;
using MeloongCore;

namespace PCL.App.Services;

/// <summary>
/// 皮肤图本地缓存：按 UUID 存原始皮肤 PNG，断网时据此本地渲染。
/// </summary>
public static class SkinCacheService {

    private const string FolderName = "Skins";

    /// <summary>缓存文件路径。</summary>
    public static string GetPath(string pclRoot, string? uuid) =>
        Path.Combine(pclRoot, "PCL", "Cache", FolderName, Normalize(uuid) + ".png");

    /// <summary>读取缓存的皮肤；不存在或损坏时返回 <c>null</c>。</summary>
    public static byte[]? Load(string pclRoot, string? uuid) {
        var normalized = Normalize(uuid);
        if (normalized.Length == 0) return null;
        try {
            var path = GetPath(pclRoot, uuid);
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            return bytes.Length == 0 ? null : bytes;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取皮肤缓存失败");
            return null;
        }
    }

    /// <summary>写入缓存；内容为空时直接忽略。</summary>
    public static void Save(string pclRoot, string? uuid, byte[]? png) {
        var normalized = Normalize(uuid);
        if (normalized.Length == 0 || png is null || png.Length == 0) return;
        try {
            var path = GetPath(pclRoot, uuid);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, png);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "写入皮肤缓存失败");
        }
    }

    private static string Normalize(string? uuid) =>
        (uuid ?? "").Replace("-", "").Trim().ToLowerInvariant();

    /// <summary>
    /// 下载原始皮肤图并写入缓存。优先用缓存，网络失败时返回 <c>null</c>。
    /// </summary>
    public static async Task<byte[]?> DownloadAsync(string pclRoot, string? uuid, CancellationToken cancellationToken = default) {
        var normalized = Normalize(uuid);
        if (normalized.Length != 32) return null;
        var cached = Load(pclRoot, uuid);
        if (cached is not null) return cached;
        try {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CEU/1.0");
            var bytes = await client
                .GetByteArrayAsync("https://crafatar.com/skins/" + normalized, cancellationToken)
                .ConfigureAwait(false);
            if (bytes.Length == 0) return null;
            Save(pclRoot, uuid, bytes);
            return bytes;
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "下载皮肤失败：" + normalized);
            return null;
        }
    }

}
