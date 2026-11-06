using System.Threading;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Update;

namespace PCL.App.Services;

/// <summary>
/// 启动后自动检查更新，并把最新版本说明作为公告展示。
/// 只在发现新版本时提示，已是最新版或检查失败都静默处理。
/// </summary>
public static class UpdateAnnouncementService {

    // 启动后延迟，避免与界面初始化争抢资源
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(6);

    /// <summary>在后台启动检查；调用方不需要等待。</summary>
    public static void StartBackgroundCheck() {
        _ = Task.Run(RunAsync);
    }

    private static async Task RunAsync() {
        try {
            await Task.Delay(StartupDelay).ConfigureAwait(false);
            var announcement = await CheckOnceAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(announcement)) return;
            Logger.Info("[更新] " + announcement);
            var application = System.Windows.Application.Current;
            if (application is null) return;
            await application.Dispatcher.InvokeAsync(() => HintService.Show(announcement));
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "自动检查更新失败");
        }
    }

    /// <summary>
    /// 立即检查一次并返回公告文本；没有更新或检查失败时返回 <c>null</c>。
    /// </summary>
    public static async Task<string?> CheckOnceAsync(CancellationToken cancellationToken = default) {
        using var fetcher = new PCL.Core.Net.HttpStringFetcher();
        var info = await UpdateCheckerService
            .CheckAsync(fetcher,
                PCL.App.Views.Migrated.PageSetupOtherSettings.UpdateApiUrl,
                PCL.App.Views.Migrated.PageSetupOtherSettings.CurrentVersion,
                cancellationToken)
            .ConfigureAwait(false);
        return AnnouncementFormatter.Build(info);
    }

}
