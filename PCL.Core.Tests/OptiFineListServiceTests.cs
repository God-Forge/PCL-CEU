using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class OptiFineListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private static string Html() {
        return string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"<td class='colForge'>Forge #47.{i}</td><td class='colDate'>2024.1.{i + 1}</td><a href=\"OptiFine_1.20.1_HD_U_I{i}.jar\">v</a>"));
    }

    private const string Bmclapi = """
    [{ "mcversion": "1.20.1", "type": "HD_U_I6", "patch": "", "filename": "OptiFine_1.20.1_HD_U_I6.jar", "forge": "Forge #47.2.0" }]
    """;

    [Test]
    public async Task 优先BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/optifine/versionList"] = Bmclapi
        });
        var result = await new OptiFineListService(fetcher).GetAsync(0);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.Versions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 官方失败时回退BMCLAPI() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/optifine/versionList"] = Bmclapi
        });
        var result = await new OptiFineListService(fetcher).GetAsync(1);
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
    }

    [Test]
    public async Task 官方源解析HTML() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://optifine.net/downloads"] = Html()
        });
        var result = await new OptiFineListService(fetcher).GetAsync(1);
        await Assert.That(result.IsOfficial).IsTrue();
        await Assert.That(result.Versions.Count).IsEqualTo(10);
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.ThrowsAsync<AggregateException>(() => new OptiFineListService(fetcher).GetAsync(0));
    }

}
