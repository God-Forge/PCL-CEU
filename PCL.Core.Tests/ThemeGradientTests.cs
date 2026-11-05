using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class ThemeGradientTests {

    [Test]
    public async Task 顶部条渐变三段且中间最亮() {
        var stops = ThemePalette.TopbarGradient(210, 85, 0, 10);
        await Assert.That(stops.Count).IsEqualTo(3);
        // 中间段亮度 54，两侧 48，因此中间应更亮
        var middle = stops[1];
        var side = stops[0];
        await Assert.That(middle.R + middle.G + middle.B).IsGreaterThan(side.R + side.G + side.B);
    }

    [Test]
    public async Task 顶部条色相偏移对称() {
        var withDelta = ThemePalette.TopbarGradient(210, 85, 0, 12);
        var withoutDelta = ThemePalette.TopbarGradient(210, 85, 0, 0);
        // delta 为 0 时两侧与中间色相相同，两侧应同色
        await Assert.That(withoutDelta[0]).IsEqualTo(withoutDelta[2]);
        // delta 不为 0 时两侧色相不同
        await Assert.That(withDelta[0] == withDelta[2]).IsFalse();
    }

    [Test]
    public async Task 顶部条亮度调整生效() {
        var normal = ThemePalette.TopbarGradient(210, 85, 0, 10);
        var lighter = ThemePalette.TopbarGradient(210, 85, 10, 10);
        await Assert.That(lighter[1].R + lighter[1].G + lighter[1].B)
            .IsGreaterThan(normal[1].R + normal[1].G + normal[1].B);
    }

    [Test]
    public async Task 主背景渐变三段() {
        var stops = ThemePalette.FormGradient(210, 85);
        await Assert.That(stops.Count).IsEqualTo(3);
        // 亮度 91 属于浅色
        await Assert.That(stops.All(stop => stop.R + stop.G + stop.B > 600)).IsTrue();
    }

    [Test]
    public async Task 主背景按开关切换() {
        var colorful = ThemePalette.FormBackground(true, 210, 85);
        var plain = ThemePalette.FormBackground(false, 210, 85);
        await Assert.That(plain).IsEqualTo(new RgbColor(245, 245, 245));
        await Assert.That(colorful == plain).IsFalse();
    }

    [Test]
    public async Task 极端参数不越界() {
        var stops = ThemePalette.TopbarGradient(720, 200, 100, 60);
        await Assert.That(stops.All(stop => stop.R <= 255 && stop.G <= 255 && stop.B <= 255)).IsTrue();
        var form = ThemePalette.FormGradient(-45, -20);
        await Assert.That(form.Count).IsEqualTo(3);
    }

}
