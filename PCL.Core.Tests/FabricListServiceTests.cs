using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class FabricListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private const string Versions = """{"game":[],"loader":[],"installer":[]}""";

    [Test]
    public async Task 优先BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/fabric-meta/v2/versions"] = Versions
        });
        var result = await new FabricListService(fetcher).GetAsync(0);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.IsOfficial).IsFalse();
    }

    [Test]
    public async Task 官方源缺少必要项时回退() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://meta.fabricmc.net/v2/versions"] = """{"game":[]}""",
            ["https://bmclapi2.bangbang93.com/fabric-meta/v2/versions"] = Versions
        });
        var result = await new FabricListService(fetcher).GetAsync(1);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.That(() => new FabricListService(fetcher).GetAsync(0))
            .Throws<AggregateException>();
    }

}
