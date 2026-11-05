using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class NeoForgeListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private static string Json(string versions) => $"{{\"versions\":[{versions}]}}" + new string(' ', 100);

    [Test]
    public async Task 优先BMCLAPI并返回版本名() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge"] = Json("\"21.1.1\",\"21.1.2\""),
            ["https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge"] = Json("\"1.20.1-47.1.99\"")
        });
        var result = await new NeoForgeListService(fetcher).GetAsync(0);
        await Assert.That(result.IsOfficial).IsFalse();
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.VersionNames.Contains("21.1.1")).IsTrue();
        await Assert.That(result.VersionNames.Contains("1.20.1-47.1.99")).IsTrue();
    }

    [Test]
    public async Task 官方失败时回退BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge"] = Json("\"21.1.1\""),
            ["https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge"] = Json("\"1.20.1-47.1.99\"")
        });
        var result = await new NeoForgeListService(fetcher).GetAsync(1);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.ThrowsAsync<AggregateException>(() => new NeoForgeListService(fetcher).GetAsync(0));
    }

}
