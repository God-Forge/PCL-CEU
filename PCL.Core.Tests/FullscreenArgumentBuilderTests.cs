using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class FullscreenArgumentBuilderTests {

    [Test]
    public async Task 全屏窗口类型生成参数() {
        await Assert.That(FullscreenArgumentBuilder.Build(0).Single()).IsEqualTo("--fullscreen");
    }

    [Test]
    public async Task 非全屏不生成参数() {
        await Assert.That(FullscreenArgumentBuilder.Build(1).Count).IsEqualTo(0);
        await Assert.That(FullscreenArgumentBuilder.Build(3).Count).IsEqualTo(0);
    }

}
