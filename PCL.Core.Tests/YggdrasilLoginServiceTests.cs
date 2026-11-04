using System.Net;
using System.Net.Http;
using PCL.Core.Login;

namespace PCL.Core.Tests;

public class YggdrasilLoginServiceTests {

    private sealed class FakeHandler : HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var path = request.RequestUri!.AbsolutePath;
            var body = path.EndsWith("/validate")
                ? ""
                : """{"accessToken":"access","clientToken":"client","selectedProfile":{"name":"Steve","id":"uuid"}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }

    }

    [Test]
    public async Task 登录并验证Yggdrasil账号() {
        using var client = new HttpClient(new FakeHandler(), false);
        var service = new YggdrasilLoginService(client);
        var result = await service.LoginAsync("https://example.com/api/yggdrasil", "Steve", "password");
        await Assert.That(result.Name).IsEqualTo("Steve");
        await Assert.That(result.AccessToken).IsEqualTo("access");
        await service.ValidateAsync("https://example.com/api/yggdrasil", "access", "client");
    }

}
