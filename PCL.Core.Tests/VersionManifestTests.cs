using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class VersionManifestTests {

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
    public async Task 获取版本JSON() {
        const string manifest = "{\"versions\":[{\"id\":\"1.20.1\",\"url\":\"https://example.com/1.20.1.json\"}]}";
        const string json = "{\"id\":\"1.20.1\",\"mainClass\":\"net.minecraft.client.main.Main\"}";
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"] = manifest,
            ["https://example.com/1.20.1.json"] = json
        });
        var result = await new VersionManifestService(fetcher).GetVersionJsonAsync("1.20.1");
        await Assert.That(result).IsEqualTo(json);
        var url = await new VersionManifestService(fetcher).GetVersionUrlAsync("1.20.1");
        await Assert.That(url).IsEqualTo("https://example.com/1.20.1.json");
    }

    [Test]
    public async Task 未找到版本时报错() {
        var fetcher = new FakeFetcher(new Dictionary<string, string> {
            ["https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"] = "{\"versions\":[]}",
            ["https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"] = "{\"versions\":[]}"
        });
        var threw = false;
        try {
            await new VersionManifestService(fetcher).GetVersionJsonAsync("0.0");
        } catch (InvalidOperationException) {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
    }

}
