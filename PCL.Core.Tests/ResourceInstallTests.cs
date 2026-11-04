using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceInstallTests {

    private const string VersionsJson = """
    [
      {"id":"v1","project_id":"ABC","name":"Sodium 1.2.3","version_number":"1.2.3",
       "game_versions":["1.20.1"],"loaders":["fabric"],"release_type":"release",
       "date_published":"2024-05-01T10:00:00Z",
       "files":[{"url":"https://cdn.example/sodium.jar","filename":"sodium-1.2.3.jar","primary":true,"size":2048,
                 "hashes":{"sha1":"abc"}}],
       "dependencies":[]}
    ]
    """;

    private sealed class FakeFetcher(string response) : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class FakeDownload(bool success) : IDownloadService {
        public DownloadRequest? LastRequest { get; private set; }

        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request,
            IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            LastRequest = request;
            return Task.FromResult(new DownloadResult {
                Success = success,
                Path = request.TargetPath,
                SourceUrl = request.Url
            });
        }
    }

    private static ResourceProject Project(ResourceKind kind = ResourceKind.Mod, string id = "ABC") => new() {
        Source = ResourceSource.Modrinth,
        Kind = kind,
        Id = id,
        Slug = "sodium",
        Title = "Sodium"
    };

    [Test]
    public async Task 资源类型映射到实例子目录() {
        await Assert.That(ResourceInstallPaths.FolderName(ResourceKind.Mod)).IsEqualTo("mods");
        await Assert.That(ResourceInstallPaths.FolderName(ResourceKind.ResourcePack)).IsEqualTo("resourcepacks");
        await Assert.That(ResourceInstallPaths.FolderName(ResourceKind.ShaderPack)).IsEqualTo("shaderpacks");
        await Assert.That(ResourceInstallPaths.FolderName(ResourceKind.DataPack)).IsEqualTo("datapacks");
    }

    [Test]
    public async Task 计算安装目录() {
        await Assert.That(ResourceInstallPaths.Resolve(@"C:\mc\1.20.1", ResourceKind.Mod))
            .IsEqualTo(@"C:\mc\1.20.1\mods");
        await Assert.That(ResourceInstallPaths.Resolve(@"C:\mc\1.20.1", ResourceKind.ShaderPack))
            .IsEqualTo(@"C:\mc\1.20.1\shaderpacks");
    }

    [Test]
    public async Task 整合包不走单文件安装() {
        await Assert.That(ResourceInstallPaths.CanInstallAsFile(ResourceKind.Modpack)).IsFalse();
        var service = new ResourceInstallService(new FakeDownload(true), new FakeFetcher("[]"));
        var result = await service.InstallAsync(Project(ResourceKind.Modpack), @"C:\mc\1.20.1");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("整合包");
    }

    [Test]
    public async Task 安装成功后返回目标路径() {
        var download = new FakeDownload(true);
        var service = new ResourceInstallService(download, new FakeFetcher(VersionsJson));
        var result = await service.InstallAsync(Project(), @"C:\mc\1.20.1", "1.20.1", "fabric");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.FilePath).IsEqualTo(@"C:\mc\1.20.1\mods\sodium-1.2.3.jar");
        await Assert.That(download.LastRequest).IsNotNull();
        await Assert.That(download.LastRequest!.Url).IsEqualTo("https://cdn.example/sodium.jar");
        await Assert.That(result.Version!.FileName).IsEqualTo("sodium-1.2.3.jar");
    }

    [Test]
    public async Task 下载失败时返回失败结果() {
        var service = new ResourceInstallService(new FakeDownload(false), new FakeFetcher(VersionsJson));
        var result = await service.InstallAsync(Project(), @"C:\mc\1.20.1", "1.20.1", "fabric");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("下载失败");
    }

    [Test]
    public async Task 没有兼容版本时失败() {
        var service = new ResourceInstallService(new FakeDownload(true), new FakeFetcher(VersionsJson));
        var result = await service.InstallAsync(Project(), @"C:\mc\1.20.1", "1.19.2", "fabric");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("没有与该游戏版本匹配");
    }

    [Test]
    public async Task 版本列表为空时失败() {
        var service = new ResourceInstallService(new FakeDownload(true), new FakeFetcher("[]"));
        var result = await service.InstallAsync(Project(), @"C:\mc\1.20.1");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("没有可下载的版本");
    }

    [Test]
    public async Task 实例目录为空时抛出参数异常() {
        var service = new ResourceInstallService(new FakeDownload(true), new FakeFetcher(VersionsJson));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InstallAsync(Project(), " ", "1.20.1", "fabric"));
    }

}
