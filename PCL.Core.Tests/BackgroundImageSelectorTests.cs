using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class BackgroundImageSelectorTests {

    private static string TempFolder() {
        var folder = Path.Combine(Path.GetTempPath(), "pcl-bg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Test]
    public async Task 空目录返回空() {
        var folder = TempFolder();
        try {
            await Assert.That(BackgroundImageSelector.Pick(folder, DateTime.Now)).IsNull();
            await Assert.That(BackgroundImageSelector.Pick(null, DateTime.Now)).IsNull();
            await Assert.That(BackgroundImageSelector.Pick(Path.Combine(folder, "missing"), DateTime.Now)).IsNull();
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 只挑选受支持的图片() {
        var folder = TempFolder();
        try {
            File.WriteAllText(Path.Combine(folder, "a.png"), "x");
            File.WriteAllText(Path.Combine(folder, "b.txt"), "x");
            File.WriteAllText(Path.Combine(folder, "c.ini"), "x");
            var images = BackgroundImageSelector.ListImages(folder);
            await Assert.That(images.Count).IsEqualTo(1);
            await Assert.That(images[0].EndsWith("a.png")).IsTrue();
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 同一天选择稳定() {
        var folder = TempFolder();
        try {
            for (var index = 0; index < 5; index++) {
                File.WriteAllText(Path.Combine(folder, $"img{index}.png"), "x");
            }
            var date = new DateTime(2026, 6, 1);
            var first = BackgroundImageSelector.Pick(folder, date);
            var second = BackgroundImageSelector.Pick(folder, date);
            await Assert.That(first).IsEqualTo(second);
            await Assert.That(first).IsNotNull();
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 不同日期可能换图() {
        var folder = TempFolder();
        try {
            for (var index = 0; index < 5; index++) {
                File.WriteAllText(Path.Combine(folder, $"img{index}.png"), "x");
            }
            var picks = Enumerable.Range(0, 5)
                .Select(offset => BackgroundImageSelector.Pick(folder, new DateTime(2026, 6, 1).AddDays(offset)))
                .Distinct()
                .Count();
            await Assert.That(picks).IsGreaterThan(1);
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 随机模式总会返回已有图片() {
        var folder = TempFolder();
        try {
            File.WriteAllText(Path.Combine(folder, "only.png"), "x");
            for (var attempt = 0; attempt < 5; attempt++) {
                var picked = BackgroundImageSelector.Pick(folder, DateTime.Now, random: true);
                await Assert.That(picked).IsNotNull();
                await Assert.That(Path.GetFileName(picked!)).IsEqualTo("only.png");
            }
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 透明度与模糊被钳制() {
        await Assert.That(BackgroundImageSelector.NormalizeOpacity(500)).IsEqualTo(0.5);
        await Assert.That(BackgroundImageSelector.NormalizeOpacity(-100)).IsEqualTo(0);
        await Assert.That(BackgroundImageSelector.NormalizeOpacity(9999)).IsEqualTo(1);
        await Assert.That(BackgroundImageSelector.NormalizeBlur(-5)).IsEqualTo(0);
        await Assert.That(BackgroundImageSelector.NormalizeBlur(999)).IsEqualTo(100);
    }

}
