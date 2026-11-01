using PCL.Core.Net;

namespace PCL.Core.Tests;

public class DownloadSpeedTrackerTests {

    [Test]
    public async Task 计算加权速度() {
        var tracker = new DownloadSpeedTracker();
        tracker.Record(1000, 1000);
        await Assert.That(tracker.Speed).IsEqualTo(1000);
        tracker.Record(3000, 1000);
        await Assert.That(tracker.Speed).IsEqualTo(1666);
    }

    [Test]
    public async Task 零时间间隔不更新() {
        var tracker = new DownloadSpeedTracker();
        tracker.Record(1000, 0);
        await Assert.That(tracker.Speed).IsEqualTo(0);
    }

    [Test]
    public async Task 十个样本后产生速度下限() {
        var tracker = new DownloadSpeedTracker();
        for (var i = 1; i <= 10; i++) {
            tracker.Record(i * 1000L, 1000);
        }
        await Assert.That(tracker.Limit).IsGreaterThan(0);
        await Assert.That(tracker.Speed).IsGreaterThan(0);
    }

}
