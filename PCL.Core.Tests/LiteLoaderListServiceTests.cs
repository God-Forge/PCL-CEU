using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class LiteLoaderListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private const string Versions = """
    {
      "versions": {
        "1.12.2": { "snapshots": { "com.mumfrey:liteloader": { "latest": { "stream": "snapshot", "md5": "b", "timestamp": "2021-01-01T00:00:00Z" } } } }
      }
    }
    """;

    [Test]
    public async Task 优先BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json"] = Versions
        });
        var result = await new LiteLoaderListService(fetcher).GetAsync(0);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.IsOfficial).IsFalse();
        await Assert.That(result.Versions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 官方失败时回退() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json"] = Versions
        });
        var result = await new LiteLoaderListService(fetcher).GetAsync(1);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.ThrowsAsync<AggregateException>(() => new LiteLoaderListService(fetcher).GetAsync(0));
    }

}
