using PCL.Core.Login;

namespace PCL.Core.Tests;

public class LoginCacheTests {

    private static LoginProfile Profile() => new() {
        Name = "Steve",
        Uuid = "069a79f444e94726a5befca90e38a9f0",
        AccessToken = "token-abc",
        ClientId = "client-1",
        Xuid = "xuid-9",
        UserType = "Microsoft"
    };

    [Test]
    public async Task 序列化后可完整还原() {
        var json = LoginCache.Serialize(Profile());
        var restored = LoginCache.Deserialize(json);
        await Assert.That(restored).IsNotNull();
        await Assert.That(restored!.Name).IsEqualTo("Steve");
        await Assert.That(restored.Uuid).IsEqualTo("069a79f444e94726a5befca90e38a9f0");
        await Assert.That(restored.AccessToken).IsEqualTo("token-abc");
        await Assert.That(restored.ClientId).IsEqualTo("client-1");
        await Assert.That(restored.Xuid).IsEqualTo("xuid-9");
        await Assert.That(restored.UserType).IsEqualTo("Microsoft");
    }

    [Test]
    public async Task 空登录序列化为空串() {
        await Assert.That(LoginCache.Serialize(null)).IsEqualTo("");
    }

    [Test]
    public async Task 空内容还原为空() {
        await Assert.That(LoginCache.Deserialize(null)).IsNull();
        await Assert.That(LoginCache.Deserialize("")).IsNull();
        await Assert.That(LoginCache.Deserialize("   ")).IsNull();
    }

    [Test]
    public async Task 损坏的JSON返回空() {
        await Assert.That(LoginCache.Deserialize("{ 不是 JSON")).IsNull();
    }

    [Test]
    public async Task 缺少用户名视为无效() {
        await Assert.That(LoginCache.Deserialize("""{"uuid":"abc"}""")).IsNull();
        await Assert.That(LoginCache.Deserialize("""{"name":""}""")).IsNull();
    }

    [Test]
    public async Task 缺少用户类型时回落legacy() {
        var restored = LoginCache.Deserialize("""{"name":"Alex"}""");
        await Assert.That(restored!.UserType).IsEqualTo("legacy");
    }

}
