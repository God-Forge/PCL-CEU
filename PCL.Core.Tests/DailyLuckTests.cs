using PCL.Core.Tools;

namespace PCL.Core.Tests;

public class DailyLuckTests {

    [Test]
    public async Task GetToday_范围与日期正确() {
        var result = DailyLuck.GetToday();
        await Assert.That(result.Value >= 0).IsTrue();
        await Assert.That(result.Value <= 100).IsTrue();
        await Assert.That(result.Date).IsEqualTo(DateTime.Now.Date);
        await Assert.That(string.IsNullOrWhiteSpace(result.Comment)).IsFalse();
    }

    [Test]
    public async Task GetToday_同一天结果固定() {
        var first = DailyLuck.GetToday();
        var second = DailyLuck.GetToday();
        await Assert.That(second.Value).IsEqualTo(first.Value);
        await Assert.That(second.Comment).IsEqualTo(first.Comment);
    }

}
