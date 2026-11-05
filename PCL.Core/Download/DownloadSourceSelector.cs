namespace PCL.Core.Download;

/// <summary>
/// 下载源选择：按测速结果排序、挑选最优源，并生成失败时的尝试顺序。
/// 对应 VB 版 ModNet 中「自动选择下载源」的行为。
/// </summary>
public static class DownloadSourceSelector {

    /// <summary>
    /// 按可用性排序：成功的源按耗时升序排在前，失败的按原顺序排在后。
    /// </summary>
    public static IReadOnlyList<DownloadSpeedResult> Rank(IEnumerable<DownloadSpeedResult>? results) {
        if (results is null) return [];
        return results
            .OrderBy(result => result.Success ? 0 : 1)
            .ThenBy(result => result.Success ? result.ElapsedMilliseconds : long.MaxValue)
            .ToList();
    }

    /// <summary>选出最优源；没有任何可用源时返回 <c>null</c>。</summary>
    public static DownloadSpeedResult? PickBest(IEnumerable<DownloadSpeedResult>? results) {
        var ranked = Rank(results);
        return ranked.FirstOrDefault(result => result.Success);
    }

    /// <summary>
    /// 生成下载地址的尝试顺序：偏好源优先，其余作为备选。
    /// <paramref name="mirror"/> 为空时只尝试原始地址。
    /// </summary>
    public static IReadOnlyList<string> BuildAttemptOrder(string? original, string? mirror, bool preferMirror) {
        var order = new List<string>();
        void Add(string? url) {
            if (string.IsNullOrWhiteSpace(url)) return;
            var trimmed = url.Trim();
            if (!order.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) order.Add(trimmed);
        }
        if (preferMirror) {
            Add(mirror);
            Add(original);
        } else {
            Add(original);
            Add(mirror);
        }
        return order;
    }

    /// <summary>
    /// 根据保存的偏好名，决定是否优先使用镜像。
    /// 偏好为空或在测速结果中不可用时回落到原地址优先。
    /// </summary>
    public static bool ShouldPreferMirror(string? preferredSourceName, IEnumerable<DownloadSpeedResult>? results) {
        if (string.IsNullOrWhiteSpace(preferredSourceName)) return false;
        if (results is null) return false;
        foreach (var result in results) {
            if (!string.Equals(result.Name, preferredSourceName, StringComparison.OrdinalIgnoreCase)) continue;
            return result.Success;
        }
        return false;
    }

}
