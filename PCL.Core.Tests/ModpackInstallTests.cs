using System.IO.Compression;
using PCL.Core.Modpack;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ModpackInstallTests {

    private const string ModrinthIndex = """
    {
      "formatVersion": 1,
      "game": "minecraft",
      "versionId": "1.0.0",
      "name": "测试整合包",
      "dependencies": { "minecraft": "1.20.1", "fabric-loader": "0.15.0" },
      "files": [
        {"path": "mods/sodium.jar", "hashes": {"sha1": "abc"}, "downloads": ["https://cdn.example/sodium.jar"], "fileSize": 2048},
        {"path": "config/bad.json", "hashes": {}, "downloads": [], "fileSize": 0}
      ]
    }
    """;

    private const string CurseForgeManifest = """
    {
      "manifestType": "minecraftModpack",
      "manifestVersion": 1,
      "name": "CF 整合包",
      "version": "2.0",
      "minecraft": { "version": "1.19.2", "modLoaders": [{"id": "forge-47.2.0", "primary": true}] },
      "files": [ {"projectID": 238222, "fileID": 4567, "required": true} ],
      "overrides": "overrides"
    }
    """;

    private sealed class FakeDownload(bool success) : IDownloadService {
        public List<DownloadRequest> Requests { get; } = [];

        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            Requests.Add(request);
            return Task.FromResult(new DownloadResult { Success = success, Path = request.TargetPath, SourceUrl = request.Url });
        }
    }

    private static string CreateModpackZip(string indexJson, bool withOverrides = true) {
        var path = Path.Combine(Path.GetTempPath(), "pcl-modpack-" + Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using (var writer = new StreamWriter(archive.CreateEntry("modrinth.index.json").Open())) {
            writer.Write(indexJson);
        }
        if (withOverrides) {
            var entry = archive.CreateEntry("overrides/config/test.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("hello");
        }
        return path;
    }

    [Test]
    public async Task 解析Modrinth清单() {
        var manifest = ModpackManifestParser.Parse(ModrinthIndex);
        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Format).IsEqualTo(ModpackFormat.Modrinth);
        await Assert.That(manifest.Name).IsEqualTo("测试整合包");
        await Assert.That(manifest.MinecraftVersion).IsEqualTo("1.20.1");
        await Assert.That(manifest.Files.Count).IsEqualTo(2);
        await Assert.That(manifest.Files[0].Url).IsEqualTo("https://cdn.example/sodium.jar");
        await Assert.That(manifest.Files[0].HasDownload).IsTrue();
        await Assert.That(manifest.Files[1].HasDownload).IsFalse();
    }

    [Test]
    public async Task 解析CurseForge清单() {
        var manifest = ModpackManifestParser.Parse(CurseForgeManifest);
        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest!.Format).IsEqualTo(ModpackFormat.CurseForge);
        await Assert.That(manifest.MinecraftVersion).IsEqualTo("1.19.2");
        await Assert.That(manifest.Files[0].HasDownload).IsFalse();
    }

    [Test]
    public async Task 无法识别的清单返回空() {
        await Assert.That(ModpackManifestParser.Parse("{}")).IsNull();
        await Assert.That(ModpackManifestParser.Parse("not json")).IsNull();
        await Assert.That(ModpackManifestParser.Parse(null)).IsNull();
    }

    [Test]
    public async Task 路径统一为正斜杠() {
        await Assert.That(ModpackManifestParser.NormalizeRelativePath(@"mods\a\b.jar")).IsEqualTo("mods/a/b.jar");
        await Assert.That(ModpackManifestParser.NormalizeRelativePath("/mods/a.jar")).IsEqualTo("mods/a.jar");
    }

    [Test]
    public async Task 安装整合包下载文件并解压覆盖() {
        var zip = CreateModpackZip(ModrinthIndex);
        try {
            var download = new FakeDownload(true);
            var target = Path.Combine(Path.GetTempPath(), "pcl-modpack-target-" + Guid.NewGuid().ToString("N"));
            var installer = new ModpackInstaller(download);
            var result = await installer.InstallAsync(zip, target);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Downloaded).IsEqualTo(1);
            await Assert.That(download.Requests.Count).IsEqualTo(1);
            await Assert.That(download.Requests[0].TargetPath).Contains("sodium.jar");
            await Assert.That(result.Overrides).IsEqualTo(1);
            await Assert.That(File.Exists(Path.Combine(target, "config", "test.txt"))).IsTrue();
            Directory.Delete(target, true);
        } finally {
            File.Delete(zip);
        }
    }

    [Test]
    public async Task 缺少清单时安装失败() {
        var zip = CreateModpackZip("{}");
        try {
            var result = await new ModpackInstaller(new FakeDownload(true))
                .InstallAsync(zip, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("无法识别");
        } finally {
            File.Delete(zip);
        }
    }

    [Test]
    public async Task 整合包文件不存在时失败() {
        var result = await new ModpackInstaller(new FakeDownload(true))
            .InstallAsync(Path.Combine(Path.GetTempPath(), "not-exist.zip"), Path.GetTempPath());
        await Assert.That(result.Success).IsFalse();
    }

}
