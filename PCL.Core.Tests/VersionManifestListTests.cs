using System.Net.Http;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class VersionManifestListTests {

    private sealed class FakeFetcher(string response) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(response);

    }

    [Test]
    public async Task 读取版本列表() {
        var service = new VersionManifestService(new FakeFetcher("""
        {"versions":[{"id":"1.20.1","type":"release","url":"https://example.com/1.20.1.json"}]}
        """));
        var versions = await service.GetVersionsAsync();
        await Assert.That(versions.Count).IsEqualTo(1);
        await Assert.That(versions[0].Id).IsEqualTo("1.20.1");
    }

}
