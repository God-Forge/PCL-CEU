using System.Net.Http;
using PCL.Core.Net;
using PCL.Core.Update;

namespace PCL.Core.Tests;

public class UpdateCheckerServiceTests {

    private sealed class FakeFetcher(string response) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(response);
        }

    }

    [Test]
    public async Task 检测新版本() {
        var info = await UpdateCheckerService.CheckAsync(
            new FakeFetcher("""{"tag_name":"v0.10.5-ceu","html_url":"https://example.com","prerelease":false}"""),
            "https://example.com", "0.10.4");
        await Assert.That(info.HasUpdate).IsTrue();
        await Assert.That(info.LatestVersion).IsEqualTo("v0.10.5-ceu");
    }

    [Test]
    public async Task 相同版本不提示更新() {
        var info = await UpdateCheckerService.CheckAsync(
            new FakeFetcher("""{"tag_name":"0.10.4-ceu","html_url":"https://example.com","prerelease":false}"""),
            "https://example.com", "0.10.4");
        await Assert.That(info.HasUpdate).IsFalse();
    }

}
