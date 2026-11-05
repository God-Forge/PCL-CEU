using PCL.Core.Download;

namespace PCL.Core.Tests;

public class DownloadCacheServiceTests {

    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "pcl-cache-" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task 相同地址得到相同缓存路径() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            var first = cache.GetCachePath("https://cdn.example/a.jar");
            var second = cache.GetCachePath("https://cdn.example/a.jar");
            await Assert.That(first).IsEqualTo(second);
            await Assert.That(first.EndsWith(".jar")).IsTrue();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 不同地址得到不同路径() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            await Assert.That(cache.GetCachePath("https://a/x.jar"))
                .IsNotEqualTo(cache.GetCachePath("https://b/x.jar"));
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 未命中时返回失败() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            await Assert.That(cache.TryGet("https://cdn.example/missing.jar", out _)).IsFalse();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 存入后可以命中且内容一致() {
        var folder = TempFolder();
        try {
            var source = Path.Combine(Path.GetTempPath(), "pcl-src-" + Guid.NewGuid().ToString("N") + ".jar");
            File.WriteAllText(source, "hello-cache");
            var cache = new DownloadCacheService(folder);
            var stored = cache.Store("https://cdn.example/a.jar", source);
            await Assert.That(File.Exists(stored)).IsTrue();
            await Assert.That(cache.TryGet("https://cdn.example/a.jar", out var hit)).IsTrue();
            await Assert.That(File.ReadAllText(hit)).IsEqualTo("hello-cache");
            File.Delete(source);
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 空文件不算命中() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            var path = cache.GetCachePath("https://cdn.example/empty.jar");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, []);
            await Assert.That(cache.TryGet("https://cdn.example/empty.jar", out _)).IsFalse();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 统计缓存大小() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            var source = Path.Combine(Path.GetTempPath(), "pcl-src-" + Guid.NewGuid().ToString("N") + ".zip");
            File.WriteAllBytes(source, new byte[100]);
            cache.Store("https://cdn.example/big.zip", source);
            await Assert.That(cache.GetSize()).IsEqualTo(100);
            File.Delete(source);
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 超过容量时按时间清理() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            var source = Path.Combine(Path.GetTempPath(), "pcl-src-" + Guid.NewGuid().ToString("N") + ".bin");
            File.WriteAllBytes(source, new byte[200]);
            var old = cache.Store("https://cdn.example/old.bin", source);
            File.SetLastAccessTimeUtc(old, DateTime.UtcNow.AddDays(-2));
            File.WriteAllBytes(source, new byte[200]);
            cache.Store("https://cdn.example/new.bin", source);
            var removed = cache.Prune(250);
            await Assert.That(removed).IsEqualTo(1);
            await Assert.That(File.Exists(old)).IsFalse();
            await Assert.That(cache.GetSize()).IsLessThan(250);
            File.Delete(source);
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 空目录与异常参数() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            await Assert.That(cache.GetSize()).IsEqualTo(0);
            await Assert.That(cache.Prune(1000)).IsEqualTo(0);
            await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(cache.GetCachePath("")));
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 没有合法扩展名时不附加后缀() {
        var folder = TempFolder();
        try {
            var cache = new DownloadCacheService(folder);
            var path = cache.GetCachePath("https://cdn.example/download");
            await Assert.That(Path.GetExtension(path)).IsEqualTo("");
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

}
