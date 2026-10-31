using System.Net.Http;
using PCL.Core.Minecraft;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class FabricInstallServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private sealed class FakeDownloadService : IDownloadService {

        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) {
            var content = System.Text.Encoding.UTF8.GetBytes(request.TargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(request.TargetPath)!);
            File.WriteAllBytes(request.TargetPath, content);
            progress?.Report(new DownloadProgress { BytesReceived = content.Length, TotalBytes = content.Length });
            return Task.FromResult(new DownloadResult {
                Success = true,
                Path = request.TargetPath,
                Bytes = content.Length,
                SourceUrl = request.Url
            });
        }

    }

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-fabric-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 安装Fabric并下载依赖() {
        var root = CreateRoot();
        try {
            var vanillaFolder = Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(vanillaFolder);
            File.WriteAllText(Path.Combine(vanillaFolder, "1.20.1.json"), "{}");

            const string profileUrl = "https://meta.fabricmc.net/v2/versions/loader/1.20.1/0.15.0/profile/json";
            var profile = """
            {
              "id": "fabric-loader-0.15.0-1.20.1",
              "inheritsFrom": "1.20.1",
              "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
              "libraries": [
                {
                  "name": "net.fabricmc:fabric-loader:0.15.0",
                  "url": "https://maven.fabricmc.net/",
                  "downloads": {
                    "artifact": {
                      "path": "net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar",
                      "url": "https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.15.0/fabric-loader-0.15.0.jar"
                    }
                  }
                }
              ]
            }
            """;
            var fetcher = new FakeFetcher(new Dictionary<string, string> { [profileUrl] = profile });
            var downloader = new FakeDownloadService();
            var folder = await new FabricInstallService(fetcher, downloader, downloader)
                .InstallAsync(root, "1.20.1", "0.15.0");

            await Assert.That(folder).IsEqualTo(Path.Combine(root, "versions", "fabric-loader-0.15.0-1.20.1"));
            await Assert.That(File.Exists(Path.Combine(folder, "fabric-loader-0.15.0-1.20.1.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(root, "libraries", "net", "fabricmc", "fabric-loader", "0.15.0", "fabric-loader-0.15.0.jar"))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
