using PCL.Core.Login;

namespace PCL.Core.Tests;

public class OfflineLoginServiceTests {

    [Test]
    public async Task 生成稳定离线UUID() {
        var first = OfflineLoginService.Create("Steve");
        var second = OfflineLoginService.Create("Steve");
        await Assert.That(first.Uuid).IsEqualTo(second.Uuid);
        await Assert.That(first.Uuid.Length).IsEqualTo(36);
        await Assert.That(first.AccessToken).IsNotEqualTo(second.AccessToken);
    }

    [Test]
    public async Task 空用户名被拒绝() {
        await Assert.That(() => OfflineLoginService.Create("")).Throws<ArgumentException>();
    }

}
