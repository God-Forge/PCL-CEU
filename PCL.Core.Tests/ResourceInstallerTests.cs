using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceInstallerTests {

    private sealed class FakeSource(Dictionary<string, IReadOnlyList<ResourceVersionFile>> map) : IResourceVersionSource {
        public Task<IReadOnlyList<ResourceVersionFile>> GetVersionsAsync(
            string projectId, ResourceKind kind, string? minecraftVersion = null, string? loader = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(map.TryGetValue(projectId, out var versions)
                ? versions
                : (IReadOnlyList<ResourceVersionFile>)[]);
    }

    private sealed class FakeDownload(Func<DownloadRequest, bool>? shouldSucceed = null) : IDownloadService {
        public List<DownloadRequest> Requests { get; } = [];

        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            Requests.Add(request);
            var ok = shouldSucceed?.Invoke(request) ?? true;
            return Task.FromResult(new DownloadResult { Success = ok, Path = request.TargetPath, SourceUrl = request.Url });
        }
    }

    private static ResourceVersionFile Version(string id, string fileName, params string[] dependencies) => new() {
        Id = id,
        Kind = ResourceKind.Mod,
        FileName = fileName,
        DownloadUrls = ["https://cdn.example/" + fileName],
        GameVersions = ["1.20.1"],
        Loaders = ["fabric"],
        Dependencies = dependencies
    };

    private static ResourceInstallRequest Request(ResourceVersionFile root, bool dependencies = true) => new() {
        Root = root,
        InstanceFolder = @"C:\mc\1.20.1",
        MinecraftVersion = "1.20.1",
        Loader = "fabric",
        IncludeDependencies = dependencies
    };

    [Test]
    public async Task 无依赖时只安装根资源() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>>());
        var download = new FakeDownload();
        var report = await new ResourceInstaller(source, download)
            .InstallAsync(Request(Version("root", "root.jar")));
        await Assert.That(report.Success).IsTrue();
        await Assert.That(report.Installed.Count).IsEqualTo(1);
        await Assert.That(report.Message).Contains("已安装 1 个文件");
    }

    [Test]
    public async Task 一并安装必需依赖() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar")]
        });
        var download = new FakeDownload();
        var report = await new ResourceInstaller(source, download)
            .InstallAsync(Request(Version("root", "root.jar", "dep1")));
        await Assert.That(report.Installed.Count).IsEqualTo(2);
        await Assert.That(download.Requests.Count).IsEqualTo(2);
        await Assert.That(report.Success).IsTrue();
    }

    [Test]
    public async Task 关闭依赖安装时只下根资源() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar")]
        });
        var download = new FakeDownload();
        var report = await new ResourceInstaller(source, download)
            .InstallAsync(Request(Version("root", "root.jar", "dep1"), dependencies: false));
        await Assert.That(report.Installed.Count).IsEqualTo(1);
        await Assert.That(download.Requests.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 依赖未解析时报告里体现() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> { ["dep1"] = [] });
        var report = await new ResourceInstaller(source, new FakeDownload())
            .InstallAsync(Request(Version("root", "root.jar", "dep1")));
        await Assert.That(report.Success).IsFalse();
        await Assert.That(report.Unresolved).Contains("dep1");
        await Assert.That(report.Message).Contains("未能解析");
    }

    [Test]
    public async Task 下载失败时报告里体现() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>>());
        var download = new FakeDownload(request => !request.TargetPath.EndsWith("bad.jar", StringComparison.OrdinalIgnoreCase));
        var report = await new ResourceInstaller(source, download)
            .InstallAsync(Request(Version("root", "bad.jar")));
        await Assert.That(report.Success).IsFalse();
        await Assert.That(report.Failed.Count).IsEqualTo(1);
        await Assert.That(report.Message).Contains("失败 1 个");
    }

    [Test]
    public async Task 整合包类型直接失败() {
        var root = Version("pack", "pack.zip");
        var packRequest = new ResourceInstallRequest {
            Root = new ResourceVersionFile {
                Id = root.Id, Kind = ResourceKind.Modpack, FileName = root.FileName, DownloadUrls = root.DownloadUrls
            },
            InstanceFolder = @"C:\mc\1.20.1"
        };
        var report = await new ResourceInstaller(new FakeSource(new()), new FakeDownload()).InstallAsync(packRequest);
        await Assert.That(report.Success).IsFalse();
        await Assert.That(report.Message).Contains("整合包");
    }

    [Test]
    public async Task 实例目录为空时抛出参数异常() {
        var request = new ResourceInstallRequest { Root = Version("root", "root.jar"), InstanceFolder = " " };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ResourceInstaller(new FakeSource(new()), new FakeDownload()).InstallAsync(request));
    }

}
