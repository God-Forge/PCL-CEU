using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class WindowSizeResolverTests {

    [Test]
    public async Task 默认与全屏使用固定尺寸() {
        var expected = new LaunchWindowSize(854, 480);
        await Assert.That(WindowSizeResolver.Resolve(WindowSizeResolver.TypeDefault)).IsEqualTo(expected);
        await Assert.That(WindowSizeResolver.Resolve(WindowSizeResolver.TypeFullscreen)).IsEqualTo(expected);
        await Assert.That(WindowSizeResolver.Resolve(WindowSizeResolver.TypeMaximized)).IsEqualTo(expected);
    }

    [Test]
    public async Task 全屏与最大化判定() {
        await Assert.That(WindowSizeResolver.IsFullscreen(0)).IsTrue();
        await Assert.That(WindowSizeResolver.IsFullscreen(1)).IsFalse();
        await Assert.That(WindowSizeResolver.IsMaximized(4)).IsTrue();
        await Assert.That(WindowSizeResolver.IsMaximized(1)).IsFalse();
    }

    [Test]
    public async Task 跟随启动器尺寸时扣除标题栏() {
        var size = WindowSizeResolver.Resolve(WindowSizeResolver.TypeFollowLauncher, launcherWidth: 1280, launcherHeight: 720);
        await Assert.That(size.Width).IsEqualTo(1280);
        await Assert.That(size.Height).IsEqualTo(690);
    }

    [Test]
    public async Task DPI缩放影响标题栏扣除() {
        var size = WindowSizeResolver.Resolve(WindowSizeResolver.TypeFollowLauncher, launcherWidth: 1280, launcherHeight: 720, dpiScale: 1.5);
        await Assert.That(size.Height).IsEqualTo(675);
    }

    [Test]
    public async Task 自定义尺寸生效() {
        var size = WindowSizeResolver.Resolve(WindowSizeResolver.TypeCustom, 1024, 768);
        await Assert.That(size).IsEqualTo(new LaunchWindowSize(1024, 768));
    }

    [Test]
    public async Task 自定义尺寸有下限() {
        var size = WindowSizeResolver.Resolve(WindowSizeResolver.TypeCustom, 10, -50);
        await Assert.That(size).IsEqualTo(new LaunchWindowSize(100, 100));
    }

    [Test]
    public async Task 未知类型回落到默认() {
        await Assert.That(WindowSizeResolver.Resolve(99)).IsEqualTo(new LaunchWindowSize(854, 480));
    }

}
