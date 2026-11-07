namespace PCL.Core.Minecraft;

/// <summary>一次性能采样。</summary>
public readonly record struct PerformancePoint(DateTime Time, long WorkingSetMb, double CpuPercent, int Threads);

/// <summary>
/// 滚动性能曲线：保留最近若干个采样点，并给出平均值与峰值。
/// 只做数据聚合，不涉及定时器与界面。
/// </summary>
public sealed class RollingPerformanceSeries {

    private readonly Queue<PerformancePoint> _points;

    public RollingPerformanceSeries(int capacity = 60) {
        Capacity = Math.Max(2, capacity);
        _points = new Queue<PerformancePoint>(Capacity);
    }

    public int Capacity { get; }

    public IReadOnlyList<PerformancePoint> Points => _points.ToList();

    public int Count => _points.Count;

    public PerformancePoint? Latest => _points.Count == 0 ? null : _points.Last();

    /// <summary>加入新采样；超过容量时丢弃最旧的。</summary>
    public void Add(PerformancePoint point) {
        _points.Enqueue(point);
        while (_points.Count > Capacity) _points.Dequeue();
    }

    public void Clear() => _points.Clear();

    public double AverageCpu => _points.Count == 0 ? 0 : _points.Average(point => point.CpuPercent);

    public double PeakCpu => _points.Count == 0 ? 0 : _points.Max(point => point.CpuPercent);

    public double AverageWorkingSetMb => _points.Count == 0 ? 0 : _points.Average(point => point.WorkingSetMb);

    public long PeakWorkingSetMb => _points.Count == 0 ? 0 : _points.Max(point => point.WorkingSetMb);

    /// <summary>首尾采样的时间跨度。</summary>
    public TimeSpan Duration => _points.Count < 2
        ? TimeSpan.Zero
        : _points.Last().Time - _points.First().Time;

}

/// <summary>
/// 把性能曲线与启动计时拼成一段可读报告。
/// </summary>
public static class PerformanceReportBuilder {

    public static string Build(RollingPerformanceSeries? series, LaunchTimingRecord? timing = null) {
        var lines = new List<string>();
        if (timing is not null) {
            lines.Add($"启动耗时：{timing.TotalMilliseconds / 1000.0:0.0} 秒");
            foreach (var (stage, milliseconds) in timing.Marks.OrderByDescending(pair => pair.Value).Take(5)) {
                lines.Add($"  · {stage}：{milliseconds / 1000.0:0.0} 秒");
            }
        }
        if (series is null || series.Count == 0) {
            lines.Add("运行期间没有采集到性能数据。");
            return string.Join(Environment.NewLine, lines);
        }
        lines.Add($"采样 {series.Count} 次，跨度 {FormatDuration(series.Duration)}");
        lines.Add($"CPU：平均 {series.AverageCpu:0.0}%，峰值 {series.PeakCpu:0.0}%");
        lines.Add($"内存：平均 {series.AverageWorkingSetMb:0} MB，峰值 {series.PeakWorkingSetMb} MB");
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatDuration(TimeSpan duration) {
        if (duration.TotalMinutes >= 1) return $"{duration.TotalMinutes:0.0} 分钟";
        return $"{duration.TotalSeconds:0} 秒";
    }

}
