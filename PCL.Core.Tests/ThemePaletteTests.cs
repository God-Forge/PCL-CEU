using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class ThemePaletteTests {

    [Test]
    public async Task HSL转RGB基础色() {
        var red = RgbColor.FromHsl(0, 100, 50);
        await Assert.That(red).IsEqualTo(new RgbColor(255, 0, 0));
        var green = RgbColor.FromHsl(120, 100, 50);
        await Assert.That(green).IsEqualTo(new RgbColor(0, 255, 0));
        var blue = RgbColor.FromHsl(240, 100, 50);
        await Assert.That(blue).IsEqualTo(new RgbColor(0, 0, 255));
    }

    [Test]
    public async Task 零饱和度得到灰色() {
        var gray = RgbColor.FromHsl(210, 0, 50);
        await Assert.That(gray.R).IsEqualTo(gray.G);
        await Assert.That(gray.G).IsEqualTo(gray.B);
    }

    [Test]
    public async Task 色相可以环绕() {
        await Assert.That(RgbColor.FromHsl(360, 100, 50)).IsEqualTo(RgbColor.FromHsl(0, 100, 50));
        await Assert.That(RgbColor.FromHsl(-90, 100, 50)).IsEqualTo(RgbColor.FromHsl(270, 100, 50));
    }

    [Test]
    public async Task 混合颜色按权重插值() {
        var black = new RgbColor(0, 0, 0);
        var white = new RgbColor(255, 255, 255);
        await Assert.That(RgbColor.Mix(black, white, 0)).IsEqualTo(black);
        await Assert.That(RgbColor.Mix(black, white, 1)).IsEqualTo(white);
        var middle = RgbColor.Mix(black, white, 0.5);
        await Assert.That(middle.R).IsEqualTo((byte)128);
    }

    [Test]
    public async Task 默认色板为主色调蓝色() {
        var palette = ThemePalette.Create();
        await Assert.That(palette.Hue).IsEqualTo(ThemePalette.DefaultHue);
        // 蓝色主色的蓝通道应明显高于红通道
        await Assert.That(palette.Color2.B).IsGreaterThan(palette.Color2.R);
        await Assert.That(palette.Color3.B).IsGreaterThan(palette.Color3.R);
    }

    [Test]
    public async Task 色板包含全部资源键() {
        var palette = ThemePalette.Create();
        await Assert.That(palette.BrushColors.Count).IsEqualTo(18);
        await Assert.That(palette.BrushColors.ContainsKey("ColorBrush1")).IsTrue();
        await Assert.That(palette.BrushColors.ContainsKey("ColorBrushBg1")).IsTrue();
        await Assert.That(palette.BrushColors.ContainsKey("ColorBrushGray8")).IsTrue();
    }

    [Test]
    public async Task 灰色系为固定值() {
        var palette = ThemePalette.Create();
        await Assert.That(palette.Gray1).IsEqualTo(new RgbColor(64, 64, 64));
        await Assert.That(palette.Gray8).IsEqualTo(new RgbColor(245, 245, 245));
    }

    [Test]
    public async Task 亮度越高颜色越浅() {
        var palette = ThemePalette.Create();
        await Assert.That(palette.Color8.R).IsGreaterThan(palette.Color2.R);
        await Assert.That(palette.Color7.R).IsGreaterThan(palette.Color4.R);
    }

}
