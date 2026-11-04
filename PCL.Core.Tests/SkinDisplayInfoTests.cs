using PCL.Core.Login;

namespace PCL.Core.Tests;

public class SkinDisplayInfoTests {

    private static LoginProfile Profile(string name, string uuid) => new() {
        Name = name,
        Uuid = uuid,
        AccessToken = "token",
        UserType = "Microsoft"
    };

    [Test]
    public async Task 未登录时显示占位() {
        var info = SkinDisplayInfo.From(null);
        await Assert.That(info.HasSkin).IsFalse();
        await Assert.That(info.StatusText).IsEqualTo("未登录");
        await Assert.That(info.AvatarUrl).IsEqualTo("");
    }

    [Test]
    public async Task 合法UUID生成皮肤地址() {
        var info = SkinDisplayInfo.From(Profile("Steve", "069a79f4-44e9-4726-a5be-fca90e38a9f0"));
        await Assert.That(info.HasSkin).IsTrue();
        await Assert.That(info.Uuid).IsEqualTo("069a79f444e94726a5befca90e38a9f0");
        await Assert.That(info.AvatarUrl).Contains("crafatar.com/avatars/069a79f444e94726a5befca90e38a9f0");
        await Assert.That(info.RenderUrl).Contains("renders/body/");
        await Assert.That(info.StatusText).IsEqualTo("Steve");
    }

    [Test]
    public async Task 非法UUID降级但保留名字() {
        var info = SkinDisplayInfo.From(Profile("Alex", "not-a-uuid"));
        await Assert.That(info.HasSkin).IsFalse();
        await Assert.That(info.Name).IsEqualTo("Alex");
        await Assert.That(info.AvatarUrl).IsEqualTo("");
    }

    [Test]
    public async Task UUID大小写与横线被规范化() {
        var info = SkinDisplayInfo.From(Profile("Steve", "069A79F4-44E9-4726-A5BE-FCA90E38A9F0"));
        await Assert.That(info.HasSkin).IsTrue();
        await Assert.That(info.Uuid).IsEqualTo("069a79f444e94726a5befca90e38a9f0");
    }

}
