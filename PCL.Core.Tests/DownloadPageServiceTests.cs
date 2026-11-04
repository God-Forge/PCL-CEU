using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class DownloadPageServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> responses) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return responses.TryGetValue(url, out var value)
                ? Task.FromResult(value)
                : throw new HttpRequestException("Unexpected URL: " + url);
        }

    }

    [Test]
    public async Task 版本列表在首个源失败后回退() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"] = """
            {"versions":[{"id":"1.20.1","type":"release","url":"https://example.com/1.20.1.json"}]}
            """
        });

        var result = await new VersionManifestService(fetcher).GetVersionsAsync();

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Id).IsEqualTo("1.20.1");
        await Assert.That(result[0].Type).IsEqualTo("release");
    }

    [Test]
    public async Task Fabric_优先选择稳定加载器() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://meta.fabricmc.net/v2/versions"] = """
            {
              "game": [ { "version": "1.20.1", "stable": true } ],
              "loader": [
                { "version": "0.16.0", "stable": false },
                { "version": "0.15.0", "stable": true }
              ],
              "installer": [ { "version": "1.0.0", "stable": true } ]
            }
            """
        });

        var result = await new LoaderVersionService(fetcher).FindFabricAsync("1.20.1");

        await Assert.That(result.Loader).IsEqualTo("fabric");
        await Assert.That(result.VersionName).IsEqualTo("0.15.0");
        await Assert.That(result.Inherit).IsEqualTo("1.20.1");
    }

    [Test]
    public async Task NeoForge_按_Minecraft_版本选择稳定加载器() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"] =
                """{"versions":["20.4.237","20.4.237-beta"]}""",
            ["https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge"] =
                """{"versions":["1.20.1-47.1.106"]}"""
        });

        var result = await new LoaderVersionService(fetcher).FindNeoForgeAsync("1.20.4");

        await Assert.That(result.Loader).IsEqualTo("neoforge");
        await Assert.That(result.VersionName).IsEqualTo("20.4.237");
        await Assert.That(result.IsBeta).IsFalse();
    }

    [Test]
    public async Task Forge_镜像源返回可安装版本() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1"] = """
            [
              {
                "version": "47.2.0",
                "modified": "2023-08-01T00:00:00Z",
                "files": [ { "category": "installer", "format": "jar", "hash": "abc" } ]
              }
            ]
            """
        });

        var result = await new ForgeVersionService(fetcher).GetVersionsAsync(
            "1.20.1", 0, "47.2.0");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].VersionName).IsEqualTo("47.2.0");
        await Assert.That(result[0].Category).IsEqualTo("installer");
        await Assert.That(result[0].IsRecommended).IsTrue();
    }

}
