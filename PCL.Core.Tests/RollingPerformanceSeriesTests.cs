using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class RollingPerformanceSeriesTests {

    private static readonly DateTime Base = new(2026, 6, 1, 12, 0, 0);

    private static PerformancePoint Point(int second, long mb, double cpu, int threads = 8) =>
        new(Base.AddSeconds(second), mb, cpu, threads);

    [Test]
    public async Task 空曲线统计为零() {
        var series = new RollingPerformanceSeries();
        await Assert.That(series.Count).IsEqualTo(0);
        await Assert.That(series.Latest).IsNull();
        await Assert.That(series.AverageCpu).IsEqualTo(0);
        await Assert.That(series.PeakWorkingSetMb).IsEqualTo(0);
        await Assert.That(series.Duration).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task 保留最近N个采样() {
        var series = new RollingPerformanceSeries(3);
        for (var i = 0; i < 5; i++) series.Add(Point(i, 100 + i, i));
        await Assert.That(series.Count).IsEqualTo(3);
        await Assert.That(series.Points[0].WorkingSetMb).IsEqualTo(102);
        await Assert.That(series.Latest!.Value.WorkingSetMb).IsEqualTo(104);
    }

    [Test]
    public async Task 平均值与峰值() {
        var series = new RollingPerformanceSeries();
        series.Add(Point(0, 1000, 10));
        series.Add(Point(1, 3000, 40));
        series.Add(Point(2, 2000, 25));
        await Assert.That(series.AverageCpu).IsEqualTo(25);
        await Assert.That(series.PeakCpu).IsEqualTo(40);
        await Assert.That(series.AverageWorkingSetMb).IsEqualTo(2000);
        await Assert.That(series.PeakWorkingSetMb).IsEqualTo(3000);
    }

    [Test]
    public async Task 时间跨度按首尾计算() {
        var series = new RollingPerformanceSeries();
        series.Add(Point(0, 100, 10));
        series.Add(Point(10, 100, 10));
        await Assert.That(series.Duration.TotalSeconds).IsEqualTo(10);
    }

    [Test]
    public async Task 清空后回到初始状态() {
        var series = new RollingPerformanceSeries();
        series.Add(Point(0, 100, 10));
        series.Clear();
        await Assert.That(series.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 容量有下限() {
        var series = new RollingPerformanceSeries(0);
        await Assert.That(series.Capacity).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task 报告包含启动耗时与阶段() {
        var timing = new LaunchTimingRecord {
            Instance = "26.3",
            TotalMilliseconds = 8400,
            Marks = new Dictionary<string, long> { ["Java 检测"] = 1200, ["参数构建"] = 800 }
        };
        var report = PerformanceReportBuilder.Build(null, timing);
        await Assert.That(report).Contains("启动耗时：8.4 秒");
        await Assert.That(report).Contains("Java 检测");
    }

    [Test]
    public async Task 报告包含性能统计() {
        var series = new RollingPerformanceSeries();
        series.Add(Point(0, 3200, 35));
        series.Add(Point(60, 4100, 78));
        var report = PerformanceReportBuilder.Build(series);
        await Assert.That(report).Contains("采样 2 次");
        await Assert.That(report).Contains("峰值 78.0%");
        await Assert.That(report).Contains("峰值 4100 MB");
    }

    [Test]
    public async Task 没有采样时说明原因() {
        var report = PerformanceReportBuilder.Build(new RollingPerformanceSeries());
        await Assert.That(report).Contains("没有采集到性能数据");
    }

}
