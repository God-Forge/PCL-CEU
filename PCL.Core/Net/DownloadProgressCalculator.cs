namespace PCL.Core.Net;

/// <summary>
/// 计算下载任务的加权总进度。
/// </summary>
public static class DownloadProgressCalculator {

    public static double Calculate(IEnumerable<(double Progress, bool IsCopy)> files) {
        var current = 0d;
        var total = 0d;
        foreach (var file in files) {
            if (file.IsCopy) {
                current += file.Progress * 0.2;
                total += 0.2;
            } else {
                current += file.Progress;
                total += 1;
            }
        }
        return total > 0 && !double.IsNaN(total) ? current / total : 0;
    }

}
