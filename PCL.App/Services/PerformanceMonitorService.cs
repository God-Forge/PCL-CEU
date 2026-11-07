using System.Diagnostics;
using MeloongCore;
using PCL.Core.Minecraft;

namespace PCL.App.Services;

/// <summary>
/// 游戏运行期间的性能采样：每 2 秒记录一次 CPU 与内存，进程退出后汇总成报告。
/// 采样在后台线程进行，不阻塞界面。
/// </summary>
public static class PerformanceMonitorService {

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private static RollingPerformanceSeries? _series;
    private static CancellationTokenSource? _cancellation;
    private static Task? _loop;

    /// <summary>是否正在采样。</summary>
    public static bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>开始采样；重复调用会先停止上一次。</summary>
    public static void Start(Process process) {
        Stop();
        var series = new RollingPerformanceSeries(120);
        var cancellation = new CancellationTokenSource();
        _series = series;
        _cancellation = cancellation;
        _loop = Task.Run(() => SampleLoopAsync(process, series, cancellation.Token));
        Logger.Info("[性能] 开始采样游戏进程");
    }

    /// <summary>停止采样并返回报告；没有数据时返回 <c>null</c>。</summary>
    public static string? Stop(LaunchTimingRecord? timing = null) {
        try {
            _cancellation?.Cancel();
        } catch {
            // 取消失败不影响结果
        }
        var series = _series;
        _series = null;
        _cancellation = null;
        _loop = null;
        if (series is null || series.Count == 0) return null;
        var report = PerformanceReportBuilder.Build(series, timing);
        Logger.Info("[性能] " + report.Replace(Environment.NewLine, " | "));
        return report;
    }

    private static async Task SampleLoopAsync(Process process, RollingPerformanceSeries series, CancellationToken token) {
        var previousCpu = TimeSpan.Zero;
        var previousAt = DateTime.UtcNow;
        while (!token.IsCancellationRequested) {
            try {
                if (process.HasExited) break;
                process.Refresh();
                var now = DateTime.UtcNow;
                var cpuTime = process.TotalProcessorTime;
                var elapsed = (now - previousAt).TotalMilliseconds;
                var cpuPercent = elapsed <= 0
                    ? 0
                    : (cpuTime - previousCpu).TotalMilliseconds / (elapsed * Math.Max(1, Environment.ProcessorCount)) * 100;
                previousCpu = cpuTime;
                previousAt = now;
                series.Add(new PerformancePoint(
                    now,
                    process.WorkingSet64 / 1024 / 1024,
                    Math.Clamp(cpuPercent, 0, 100),
                    process.Threads.Count));
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "性能采样失败");
                break;
            }
            try {
                await Task.Delay(Interval, token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                break;
            }
        }
    }

}
