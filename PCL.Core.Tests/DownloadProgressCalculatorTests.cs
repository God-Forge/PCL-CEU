using PCL.Core.Net;

namespace PCL.Core.Tests;

public class DownloadProgressCalculatorTests {

    [Test]
    public async Task 普通文件按权重计算() {
        var progress = DownloadProgressCalculator.Calculate([
            (0.5, false),
            (1.0, false)
        ]);
        await Assert.That(progress).IsEqualTo(0.75);
    }

    [Test]
    public async Task 复制文件权重为02() {
        var progress = DownloadProgressCalculator.Calculate([
            (1.0, true),
            (0.0, false)
        ]);
        await Assert.That(progress).IsEqualTo(0.2 / 1.2);
    }

    [Test]
    public async Task 空列表返回零() {
        await Assert.That(DownloadProgressCalculator.Calculate([])).IsEqualTo(0);
    }

}
