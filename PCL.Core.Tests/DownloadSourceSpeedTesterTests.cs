using System.Net;
using System.Net.Http;
using PCL.Core.Download;

namespace PCL.Core.Tests;

public class DownloadSourceSpeedTesterTests {

    private sealed class FakeHandler : HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

    }

    [Test]
    public async Task 测试默认下载源() {
        var tester = new DownloadSourceSpeedTester(new FakeHandler());
        var results = await tester.TestAllAsync(DownloadSourceSpeedTester.DefaultTargets(), 5000);
        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results.All(result => result.Success)).IsTrue();
        await Assert.That(DownloadSourceSpeedTester.Format(results)).Contains("Mojang");
    }

}
