using System.Net.Http;
using Newtonsoft.Json.Linq;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ClientListServiceTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private static string Manifest() {
        var versions = new JArray(Enumerable.Range(0, 200).Select(i => new JObject { ["id"] = "1." + i }));
        return new JObject {
            ["latest"] = new JObject { ["release"] = "1.199", ["snapshot"] = "1.200" },
            ["versions"] = versions
        }.ToString();
    }

    [Test]
    public async Task 优先BMCLAPI并校验必需版本() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"] = Manifest()
        });
        var result = await new ClientListService(fetcher).GetAsync(0, "1.10");
        await Assert.That(result.SourceName).IsEqualTo("BMCLAPI");
        await Assert.That(result.IsOfficial).IsFalse();
        await Assert.That(result.Value["versions"]!.Count()).IsEqualTo(200);
    }

    [Test]
    public async Task 镜像缺少版本时回退官方() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"] = Manifest(),
            ["https://launchermeta.mojang.com/mc/game/version_manifest.json"] = Manifest()
        });
        var result = await new ClientListService(fetcher).GetAsync(0, "missing");
        await Assert.That(result.IsOfficial).IsTrue();
    }

    [Test]
    public async Task 两个源都失败时抛出聚合异常() {
        var fetcher = new FakeFetcher(new Dictionary<string, string>());
        await Assert.That(() => new ClientListService(fetcher).GetAsync(0))
            .Throws<AggregateException>();
    }

}
