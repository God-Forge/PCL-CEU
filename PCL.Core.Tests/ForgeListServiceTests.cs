using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ForgeListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private static string Html() {
        return string.Concat(Enumerable.Range(0, 10).Select(i => $"<a href=\"index_1.{i}.2.html\">v</a>"));
    }

    [Test]
    public async Task 优先BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/minecraft"] = """["1.20.1","1.19.2","1.18.2","1.17.1","1.16.5","1.15.2","1.14.4","1.12.2","1.8.9","1.7.10"]"""
        });
        var result = await new ForgeListService(fetcher).GetAsync(0);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.Names.Count).IsEqualTo(10);
    }

    [Test]
    public async Task 官方失败时回退BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/forge/minecraft"] = """["1.20.1","1.19.2","1.18.2","1.17.1","1.16.5","1.15.2","1.14.4","1.12.2","1.8.9","1.7.10"]"""
        });
        var result = await new ForgeListService(fetcher).GetAsync(1);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
    }

    [Test]
    public async Task 官方源解析HTML() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html"] = Html()
        });
        var result = await new ForgeListService(fetcher).GetAsync(1);
        await Assert.That(result.IsOfficial).IsTrue();
        await Assert.That(result.Names.Contains("1.2.4")).IsTrue();
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.ThrowsAsync<AggregateException>(() => new ForgeListService(fetcher).GetAsync(0));
    }

}
