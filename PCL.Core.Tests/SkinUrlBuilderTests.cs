using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class SkinUrlBuilderTests {

    private const string Uuid = "069a79f444e94726a5befca90e38a9f0";

    [Test]
    public async Task 头部渲染地址() {
        var url = SkinUrlBuilder.Head(Uuid, 12);
        await Assert.That(url).Contains("/renders/head/");
        await Assert.That(url).Contains("scale=12");
        await Assert.That(url).Contains("overlay");
    }

    [Test]
    public async Task 披风渲染地址() {
        var url = SkinUrlBuilder.Cape(Uuid, 10);
        await Assert.That(url).Contains("/renders/cape/");
        await Assert.That(url).Contains("scale=10");
    }

    [Test]
    public async Task 缩放被钳制() {
        await Assert.That(SkinUrlBuilder.Head(Uuid, 999)).Contains("scale=16");
        await Assert.That(SkinUrlBuilder.Cape(Uuid, 0)).Contains("scale=1");
    }

    [Test]
    public async Task 预览图默认包含头部与全身() {
        var images = SkinUrlBuilder.PreviewImages(Uuid, hasCape: false);
        await Assert.That(images.Count).IsEqualTo(2);
        await Assert.That(images[0].Name).IsEqualTo("头部");
        await Assert.That(images[1].Name).IsEqualTo("全身");
    }

    [Test]
    public async Task 有披风时附加披风() {
        var images = SkinUrlBuilder.PreviewImages(Uuid, hasCape: true);
        await Assert.That(images.Count).IsEqualTo(3);
        await Assert.That(images[2].Name).IsEqualTo("披风");
    }

    [Test]
    public async Task 非法UUID不生成预览图() {
        await Assert.That(SkinUrlBuilder.PreviewImages(null, true).Count).IsEqualTo(0);
        await Assert.That(SkinUrlBuilder.PreviewImages("abc", true).Count).IsEqualTo(0);
    }

}
