using System.Net.Http;
using PCL.Core.Login;

namespace PCL.Core.Tests;

public class LoginErrorTranslatorTests {

    [Test]
    public async Task 网络异常提示检查网络() {
        var message = LoginErrorTranslator.Translate(new HttpRequestException("connection refused"));
        await Assert.That(message).Contains("无法连接登录服务器");
    }

    [Test]
    public async Task 超时提示重试() {
        var message = LoginErrorTranslator.Translate(new TaskCanceledException());
        await Assert.That(message).Contains("超时");
    }

    [Test]
    public async Task 取消提示已取消() {
        var message = LoginErrorTranslator.Translate(new OperationCanceledException());
        await Assert.That(message).Contains("已取消");
    }

    [Test]
    public async Task 业务异常直接展示消息() {
        var message = LoginErrorTranslator.Translate(new InvalidOperationException("尚未配置登录服务器地址。"));
        await Assert.That(message).IsEqualTo("尚未配置登录服务器地址。");
    }

    [Test]
    public async Task 地址格式错误() {
        var message = LoginErrorTranslator.Translate(new UriFormatException("bad uri"));
        await Assert.That(message).Contains("地址格式不正确");
    }

    [Test]
    public async Task 其他异常带原文() {
        var message = LoginErrorTranslator.Translate(new Exception("boom"));
        await Assert.That(message).Contains("boom");
        await Assert.That(LoginErrorTranslator.Translate(null)).IsNotNull();
    }

}
