using PCL.Core.Mods;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class LocalResourceTests {

    private static ModInfo Mod(string fileName, string modId, string version, bool enabled = true) => new() {
        FilePath = @"C:\mc\mods\" + fileName,
        FileName = fileName,
        ModId = modId,
        Name = modId,
        Version = version,
        Loader = "fabric",
        Enabled = enabled,
        Sha256 = "hash-" + modId
    };

    private static string TempIndexPath() =>
        Path.Combine(Path.GetTempPath(), "pcl-local-res-" + Guid.NewGuid().ToString("N") + ".json");

    [Test]
    public async Task 扫描结果转换为本地条目() {
        var scan = new ModScanResult { Mods = [Mod("sodium.jar", "sodium", "1.2.3")] };
        var entries = LocalResourceIndexBuilder.FromScan(scan);
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].ModId).IsEqualTo("sodium");
        await Assert.That(entries[0].Version).IsEqualTo("1.2.3");
        await Assert.That(entries[0].Enabled).IsTrue();
        await Assert.That(entries[0].HasProject).IsFalse();
    }

    [Test]
    public async Task 同名启用与禁用文件只保留启用的() {
        var entries = LocalResourceIndexBuilder.Deduplicate([
            Mod("sodium.jar", "sodium", "1.2.3", enabled: true).ToEntry(),
            Mod("sodium.jar.disabled", "sodium", "1.1.0", enabled: false).ToEntry()
        ]);
        await Assert.That(entries.Count).IsEqualTo(1);
        await Assert.That(entries[0].FileName).IsEqualTo("sodium.jar");
        await Assert.That(entries[0].Enabled).IsTrue();
    }

    [Test]
    public async Task 索引可保存并重新读取() {
        var path = TempIndexPath();
        try {
            var index = new LocalResourceIndex();
            index.Upsert(Mod("sodium.jar", "sodium", "1.2.3").ToEntry()
                .WithProject(ResourceSource.Modrinth, "AABB", "sodium"));
            index.Save(path);

            var loaded = LocalResourceIndex.Load(path);
            await Assert.That(loaded.Count).IsEqualTo(1);
            var entry = loaded.Entries[0];
            await Assert.That(entry.ModId).IsEqualTo("sodium");
            await Assert.That(entry.Source).IsEqualTo(ResourceSource.Modrinth);
            await Assert.That(entry.ProjectSlug).IsEqualTo("sodium");
            await Assert.That(entry.HasProject).IsTrue();
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task 损坏的索引被视为空() {
        var path = TempIndexPath();
        try {
            File.WriteAllText(path, "{ 这不是 JSON");
            var loaded = LocalResourceIndex.Load(path);
            await Assert.That(loaded.Count).IsEqualTo(0);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task 版本不匹配的索引被弃用() {
        var path = TempIndexPath();
        try {
            File.WriteAllText(path, """{"version":999,"entries":[{"path":"C:\\mc\\mods\\a.jar"}]}""");
            var loaded = LocalResourceIndex.Load(path);
            await Assert.That(loaded.Count).IsEqualTo(0);
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task 移除磁盘上已不存在的记录() {
        var index = new LocalResourceIndex();
        index.Upsert(Mod("a.jar", "a", "1").ToEntry());
        index.Upsert(Mod("b.jar", "b", "1").ToEntry());
        index.RemoveMissing(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { @"C:\mc\mods\a.jar" });
        await Assert.That(index.Count).IsEqualTo(1);
        await Assert.That(index.Entries[0].ModId).IsEqualTo("a");
    }

    [Test]
    public async Task 选择最新且兼容的版本() {
        var versions = new List<ResourceVersionFile> {
            new() { Id = "1", FileName = "a.jar", DownloadUrls = ["u1"], GameVersions = ["1.20.1"], Loaders = ["fabric"],
                    ReleaseDate = DateTimeOffset.Parse("2024-01-01") },
            new() { Id = "2", FileName = "b.jar", DownloadUrls = ["u2"], GameVersions = ["1.20.1"], Loaders = ["fabric"],
                    ReleaseDate = DateTimeOffset.Parse("2024-06-01") },
            new() { Id = "3", FileName = "c.jar", DownloadUrls = ["u3"], GameVersions = ["1.19.2"], Loaders = ["fabric"],
                    ReleaseDate = DateTimeOffset.Parse("2024-09-01") }
        };
        var latest = ResourceVersionSelector.SelectLatest(versions, "1.20.1", "fabric");
        await Assert.That(latest!.Id).IsEqualTo("2");
    }

    [Test]
    public async Task 没有兼容版本时返回空() {
        var versions = new List<ResourceVersionFile> {
            new() { Id = "1", FileName = "a.jar", DownloadUrls = ["u1"], GameVersions = ["1.19.2"], Loaders = ["forge"] }
        };
        await Assert.That(ResourceVersionSelector.SelectLatest(versions, "1.20.1", "fabric")).IsNull();
    }

    [Test]
    public async Task 缺少下载地址的版本被跳过() {
        var versions = new List<ResourceVersionFile> {
            new() { Id = "1", FileName = "a.jar", GameVersions = ["1.20.1"], Loaders = ["fabric"], ReleaseDate = DateTimeOffset.Parse("2024-06-01") },
            new() { Id = "2", FileName = "b.jar", DownloadUrls = ["u"], GameVersions = ["1.20.1"], Loaders = ["fabric"], ReleaseDate = DateTimeOffset.Parse("2024-01-01") }
        };
        var latest = ResourceVersionSelector.SelectLatest(versions, "1.20.1", "fabric");
        await Assert.That(latest!.Id).IsEqualTo("2");
    }

    [Test]
    public async Task 标准版本号按数字比较() {
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("1.2.3", "1.2.10")).IsTrue();
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("1.3.0", "1.2.9")).IsFalse();
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("1.2.3", "1.2.3")).IsFalse();
    }

    [Test]
    public async Task 非标准版本号不同即视为可能更新() {
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("1.2.3-beta", "1.2.3-rc1")).IsTrue();
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("", "1.0")).IsTrue();
        await Assert.That(ResourceVersionSelector.IsUpdateAvailable("1.0", "")).IsFalse();
    }

}

internal static class ModInfoTestExtensions {

    public static LocalResourceEntry ToEntry(this ModInfo mod) => new() {
        FilePath = mod.FilePath,
        FileName = mod.FileName,
        Enabled = mod.Enabled,
        Loader = mod.Loader,
        ModId = mod.ModId,
        Name = mod.Name,
        Version = mod.Version,
        Sha256 = mod.Sha256
    };

}
