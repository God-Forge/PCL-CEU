namespace PCL.Core.Ui;

/// <summary>
/// 从背景图目录中挑选一张用于主界面。
/// 支持「按天轮换」与「随机」两种方式，保证同一天多次启动看到的是同一张。
/// </summary>
public static class BackgroundImageSelector {

    /// <summary>可识别的图片扩展名。</summary>
    public static readonly IReadOnlyList<string> SupportedExtensions =
        [".png", ".jpg", ".jpeg", ".bmp", ".webp", ".gif"];

    /// <summary>
    /// 挑选一张背景图。<paramref name="random"/> 为真时随机，否则按天轮换。
    /// 目录不存在或没有图片时返回 <c>null</c>。
    /// </summary>
    public static string? Pick(string? folder, DateTime now, bool random = false) {
        var files = ListImages(folder);
        if (files.Count == 0) return null;
        if (random) {
            return files[Random.Shared.Next(files.Count)];
        }
        // 按天轮换：同一天总是同一张
        var index = now.Date.DayOfYear % files.Count;
        return files[index];
    }

    /// <summary>列出目录里所有受支持的图片，按路径排序保证结果稳定。</summary>
    public static IReadOnlyList<string> ListImages(string? folder) {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return [];
        try {
            return Directory
                .EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToList();
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取背景图目录失败：" + folder);
            return [];
        }
    }

    /// <summary>把透明度设置（千分比）换算成 0~1。</summary>
    public static double NormalizeOpacity(int thousandths) =>
        Math.Clamp(thousandths, 0, 1000) / 1000.0;

    /// <summary>模糊半径：设置值越大模糊越强，限制在合理区间内。</summary>
    public static double NormalizeBlur(int blur) => Math.Clamp(blur, 0, 100);

}
