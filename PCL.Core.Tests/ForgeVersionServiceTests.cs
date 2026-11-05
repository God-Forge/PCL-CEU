using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ForgeVersionServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    [Test]
    public async Task 优先BMCLAPI并解析推荐版本() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1"] = """
            [{"version":"47.2.0","modified":"2024-01-01T00:00:00Z","files":[{"category":"installer","format":"jar","hash":"abc"}]}]
            """
        });
        var versions = await new ForgeVersionService(fetcher).GetVersionsAsync("1.20.1", 0, "47.2.0");
        await Assert.That(versions.Count).IsEqualTo(1);
        await Assert.That(versions[0].IsRecommended).IsTrue();
        await Assert.That(versions[0].Category).IsEqualTo("installer");
    }

    [Test]
    public async Task 官方失败时回退BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/minecraft/1.20.1"] = """
            [{"version":"47.2.0","modified":"2024-01-01T00:00:00Z","files":[{"category":"client","format":"zip","hash":"abc"}]}]
            """
        });
        var versions = await new ForgeVersionService(fetcher).GetVersionsAsync("1.20.1", 1);
        await Assert.That(versions.Count).IsEqualTo(1);
        await Assert.That(versions[0].Category).IsEqualTo("client");
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.ThrowsAsync<AggregateException>(() => new ForgeVersionService(fetcher).GetVersionsAsync("1.20.1", 0));
    }

}
