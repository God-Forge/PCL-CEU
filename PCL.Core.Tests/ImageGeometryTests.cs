using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class ImageGeometryTests {

    [Test]
    public async Task 裁剪区域在范围内时原样返回() {
        var rect = ImageGeometry.ClampClip(10, 20, 100, 50, 200, 200);
        await Assert.That(rect).IsEqualTo(new ClipRect(10, 20, 100, 50));
    }

    [Test]
    public async Task 越界裁剪被钳制到图片内() {
        var rect = ImageGeometry.ClampClip(150, 150, 100, 100, 200, 200);
        await Assert.That(rect.X).IsEqualTo(150);
        await Assert.That(rect.Y).IsEqualTo(150);
        await Assert.That(rect.Width).IsEqualTo(50);
        await Assert.That(rect.Height).IsEqualTo(50);
    }

    [Test]
    public async Task 负偏移被钳制为零() {
        var rect = ImageGeometry.ClampClip(-20, -30, 100, 100, 200, 200);
        await Assert.That(rect.X).IsEqualTo(0);
        await Assert.That(rect.Y).IsEqualTo(0);
        await Assert.That(rect.Width).IsEqualTo(80);
        await Assert.That(rect.Height).IsEqualTo(70);
    }

    [Test]
    public async Task 完全越界时宽高为零() {
        var rect = ImageGeometry.ClampClip(300, 300, 50, 50, 200, 200);
        await Assert.That(rect.Width).IsEqualTo(0);
        await Assert.That(rect.Height).IsEqualTo(0);
    }

    [Test]
    public async Task 等比缩放到最大尺寸内() {
        var (width, height) = ImageGeometry.FitWithin(1920, 1080, 480, 480);
        await Assert.That(width).IsEqualTo(480);
        await Assert.That(height).IsEqualTo(270);
    }

    [Test]
    public async Task 小图不放大() {
        var (width, height) = ImageGeometry.FitWithin(64, 32, 480, 480);
        await Assert.That(width).IsEqualTo(64);
        await Assert.That(height).IsEqualTo(32);
    }

    [Test]
    public async Task 极端比例不产生零尺寸() {
        var (width, height) = ImageGeometry.FitWithin(1000, 1, 100, 100);
        await Assert.That(width).IsEqualTo(100);
        await Assert.That(height).IsEqualTo(1);
    }

    [Test]
    public async Task 识别WebP文件头() {
        var webp = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };
        await Assert.That(ImageGeometry.IsWebP(webp)).IsTrue();
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0 };
        await Assert.That(ImageGeometry.IsWebP(png)).IsFalse();
        await Assert.That(ImageGeometry.IsWebP([1, 2, 3])).IsFalse();
    }

    [Test]
    public async Task 保存扩展名归一化() {
        await Assert.That(ImageGeometry.NormalizeSaveExtension(@"C:\a\b.PNG")).IsEqualTo(".png");
        await Assert.That(ImageGeometry.NormalizeSaveExtension("x.jpeg")).IsEqualTo(".jpg");
        await Assert.That(ImageGeometry.NormalizeSaveExtension("x.unknown")).IsEqualTo(".png");
        await Assert.That(ImageGeometry.NormalizeSaveExtension(null)).IsEqualTo(".png");
    }

}
