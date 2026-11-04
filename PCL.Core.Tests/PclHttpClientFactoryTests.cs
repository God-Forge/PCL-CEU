using System.Net;
using System.Net.Http;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class PclHttpClientFactoryTests {

    private sealed class FakeHandler : HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

    }

    [Test]
    public async Task 创建客户端并设置请求头() {
        using var client = PclHttpClientFactory.Create(new FakeHandler(), TimeSpan.FromSeconds(5), "PCL-Test/1.0");
        await Assert.That(client.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(client.DefaultRequestHeaders.UserAgent.ToString()).Contains("PCL-Test");
        using var response = await client.GetAsync("https://example.com/");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

}
