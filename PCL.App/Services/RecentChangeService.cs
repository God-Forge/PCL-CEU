using System.IO;
using PCL.Core.Minecraft;

namespace PCL.App.Services;

/// <summary>
/// 「最近改动」的统一入口：记录 Mod 安装/更新与设置修改，崩溃时用于定位可疑改动。
/// </summary>
public static class RecentChangeService {

    /// <summary>默认回溯窗口：崩溃时只看最近两小时。</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromHours(2);

    public static string FilePath => Path.Combine(AppServices.Paths.Pcl, "RecentChanges.json");

    /// <summary>记录一条改动；失败只记日志，不影响主流程。</summary>
    public static void Record(RecentChangeKind kind, string target, string detail = "") {
        if (string.IsNullOrWhiteSpace(target)) return;
        RecentChangeLog.Record(FilePath, new RecentChange {
            Time = DateTime.Now,
            Kind = kind,
            Target = target,
            Detail = detail
        });
    }

    /// <summary>取窗口期内的改动描述；没有时返回空串。</summary>
    public static string DescribeRecent(TimeSpan? window = null) {
        var all = RecentChangeLog.Load(FilePath);
        var recent = RecentChangeLog.Within(all, window ?? DefaultWindow);
        return RecentChangeLog.Describe(recent);
    }

}
