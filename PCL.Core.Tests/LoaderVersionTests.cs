using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class LoaderVersionTests {

    private sealed class FakeFetcher : IStringFetcher {

        private readonly Dictionary<string, string> _data;

        public FakeFetcher(Dictionary<string, string> data) {
            _data = data;
        }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(_data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    [Test]
    public async Task Forge_优先推荐版本() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/promos"] = "[{\"name\":\"1.20.1-recommended\",\"build\":{\"version\":\"47.4.0\"}}]"
        });
        var info = await new LoaderVersionService(fetcher).FindForgeAsync("1.20.1");
        await Assert.That(info.VersionName).IsEqualTo("47.4.0");
        await Assert.That(info.IsRecommended).IsTrue();
    }

    [Test]
    public async Task Forge_无推荐时选择最高版本() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/promos"] = "[]",
            ["https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1"] = "[{\"version\":\"47.4.8\"},{\"version\":\"47.4.10\"}]"
        });
        var info = await new LoaderVersionService(fetcher).FindForgeAsync("1.20.1");
        await Assert.That(info.VersionName).IsEqualTo("47.4.10");
    }

    [Test]
    public async Task NeoForge_按版本与稳定度选择() {
        const string latest = "{\"versions\":[\"20.4.30-beta\",\"20.4.30\"]}";
        const string legacy = "{\"versions\":[\"1.20.1-47.1.99\",\"1.20.1-47.1.82\"]}";
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"] = latest,
            ["https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge"] = legacy
        });
        var service = new LoaderVersionService(fetcher);
        var modern = await service.FindNeoForgeAsync("1.20.4");
        await Assert.That(modern.VersionName).IsEqualTo("20.4.30");
        await Assert.That(modern.IsBeta).IsFalse();

        var legacyInfo = await service.FindNeoForgeAsync("1.20.1");
        await Assert.That(legacyInfo.VersionName).IsEqualTo("1.20.1-47.1.99");
    }

    [Test]
    public async Task Fabric_优先稳定Loader() {
        const string json = "{\"game\":[{\"version\":\"1.20.1\"}],\"loader\":[{\"version\":\"0.16.10\",\"stable\":false},{\"version\":\"0.16.9\",\"stable\":true}]}";
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://meta.fabricmc.net/v2/versions"] = json
        });
        var info = await new LoaderVersionService(fetcher).FindFabricAsync("1.20.1");
        await Assert.That(info.VersionName).IsEqualTo("0.16.9");
    }

}
