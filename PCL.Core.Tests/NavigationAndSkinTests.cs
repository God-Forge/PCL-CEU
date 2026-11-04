using PCL.Core.Minecraft;
using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class NavigationAndSkinTests {

    [Test]
    public async Task 导航历史入栈与返回() {
        var history = new NavigationHistory();
        await Assert.That(history.CanGoBack).IsFalse();
        history.Push("launch");
        history.Push("login");
        await Assert.That(history.CanGoBack).IsTrue();
        await Assert.That(history.Current).IsEqualTo("login");
        await Assert.That(history.Pop()).IsEqualTo("launch");
        await Assert.That(history.CanGoBack).IsFalse();
        await Assert.That(history.Pop()).IsNull();
    }

    [Test]
    public async Task 连续相同页面不重复入栈() {
        var history = new NavigationHistory();
        history.Push("launch");
        history.Push("launch");
        await Assert.That(history.Entries.Count).IsEqualTo(1);
        history.Push("login");
        await Assert.That(history.Entries.Count).IsEqualTo(2);
    }

    [Test]
    public async Task 空页面被忽略() {
        var history = new NavigationHistory();
        history.Push("");
        history.Push(null);
        await Assert.That(history.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 历史深度有上限() {
        var history = new NavigationHistory();
        for (var i = 0; i < NavigationHistory.MaxDepth + 10; i++) {
            history.Push("page" + i);
        }
        await Assert.That(history.Entries.Count).IsEqualTo(NavigationHistory.MaxDepth);
        await Assert.That(history.Current).IsEqualTo("page" + (NavigationHistory.MaxDepth + 9));
    }

    [Test]
    public async Task 重置历史() {
        var history = new NavigationHistory();
        history.Push("login");
        history.Reset("launch");
        await Assert.That(history.Entries.Count).IsEqualTo(1);
        await Assert.That(history.Current).IsEqualTo("launch");
    }

    [Test]
    public async Task UUID规范化() {
        await Assert.That(SkinUrlBuilder.Normalize("069A79F4-44E9-4726-A5BE-FCA90E38A9F0"))
            .IsEqualTo("069a79f444e94726a5befca90e38a9f0");
        await Assert.That(SkinUrlBuilder.Normalize("  abc  ")).IsEqualTo("abc");
    }

    [Test]
    public async Task UUID合法性判断() {
        await Assert.That(SkinUrlBuilder.IsValidUuid("069a79f444e94726a5befca90e38a9f0")).IsTrue();
        await Assert.That(SkinUrlBuilder.IsValidUuid("069A79F4-44E9-4726-A5BE-FCA90E38A9F0")).IsTrue();
        await Assert.That(SkinUrlBuilder.IsValidUuid("abc")).IsFalse();
        await Assert.That(SkinUrlBuilder.IsValidUuid(null)).IsFalse();
    }

    [Test]
    public async Task 皮肤地址包含尺寸且被钳制() {
        var url = SkinUrlBuilder.Avatar("069a79f4-44e9-4726-a5be-fca90e38a9f0", 128);
        await Assert.That(url).Contains("069a79f444e94726a5befca90e38a9f0");
        await Assert.That(url).Contains("size=128");
        var clamped = SkinUrlBuilder.Avatar("abc", 9999);
        await Assert.That(clamped).Contains("size=512");
    }

    [Test]
    public async Task 全身渲染地址() {
        var url = SkinUrlBuilder.Render("069a79f444e94726a5befca90e38a9f0", 4);
        await Assert.That(url).Contains("renders/body/");
        await Assert.That(url).Contains("scale=4");
    }

}
