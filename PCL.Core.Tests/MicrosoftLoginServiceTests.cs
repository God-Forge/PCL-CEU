using System.Net;
using System.Net.Http;
using PCL.Core.Login;

namespace PCL.Core.Tests;

public class MicrosoftLoginServiceTests {

    private sealed class FakeHandler : HttpMessageHandler {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            var body = request.RequestUri!.AbsolutePath.Contains("devicecode")
                ? """{"device_code":"device","user_code":"ABC","verification_uri":"https://example.com","expires_in":900,"interval":5}"""
                : """{"Token":"xbl-token"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(body)
            });
        }

    }

    [Test]
    public async Task 开始设备码登录并认证Xbox() {
        using var client = new HttpClient(new FakeHandler(), false);
        var service = new MicrosoftLoginService("client-id", client);
        var code = await service.BeginDeviceCodeAsync();
        await Assert.That(code.UserCode).IsEqualTo("ABC");
        await Assert.That(code.VerificationUri).IsEqualTo("https://example.com");
        var token = await service.AuthenticateXboxLiveAsync("access-token");
        await Assert.That(token).IsEqualTo("xbl-token");
    }

}
