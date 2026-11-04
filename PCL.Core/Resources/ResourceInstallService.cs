using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>安装结果。</summary>
public sealed class ResourceInstallResult {

    public bool Success { get; init; }
    public string FilePath { get; init; } = "";
    public string Message { get; init; } = "";
    public ResourceVersionFile? Version { get; init; }

    public static ResourceInstallResult Failed(string message) => new() { Success = false, Message = message };

}

/// <summary>
/// 把资源项目安装到实例：查版本 → 挑选兼容版本 → 生成下载请求 → 下载。
/// 由 VB 版 ModDownloadLib 的安装流程迁移而来。
/// </summary>
public sealed class ResourceInstallService {

    private readonly IDownloadService _download;
    private readonly IStringFetcher? _fetcher;

    public ResourceInstallService(IDownloadService? download = null, IStringFetcher? fetcher = null) {
        _download = download ?? new HttpDownloadService();
        _fetcher = fetcher;
    }

    /// <summary>
    /// 安装资源。整合包需要专用流程，会直接返回失败。
    /// </summary>
    public async Task<ResourceInstallResult> InstallAsync(
        ResourceProject project,
        string instanceFolder,
        string? minecraftVersion = null,
        string? loader = null,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        if (!ResourceInstallPaths.CanInstallAsFile(project.Kind)) {
            return ResourceInstallResult.Failed("整合包需要专用安装流程，暂不支持单文件安装。");
        }

        IReadOnlyList<ResourceVersionFile> versions;
        try {
            versions = await LoadVersionsAsync(project, minecraftVersion, loader, cancellationToken).ConfigureAwait(false);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return ResourceInstallResult.Failed("获取版本列表失败：" + ex.Message);
        }
        if (versions.Count == 0) return ResourceInstallResult.Failed("该资源没有可下载的版本。");

        var version = ResourceVersionSelector.SelectLatest(versions, minecraftVersion, loader);
        if (version is null) {
            return ResourceInstallResult.Failed("没有与该游戏版本匹配的可用文件。");
        }

        var folder = ResourceInstallPaths.Resolve(instanceFolder, project.Kind);
        var request = ResourceDownloadPlanner.CreateRequest(version, folder);
        if (request is null) return ResourceInstallResult.Failed("生成下载任务失败。");

        try {
            var result = await _download.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
            if (!result.Success) {
                return ResourceInstallResult.Failed("下载失败：" + request.Url);
            }
            return new ResourceInstallResult {
                Success = true,
                FilePath = request.TargetPath,
                Message = $"已安装 {version.FileName}",
                Version = version
            };
        } catch (OperationCanceledException) {
            return ResourceInstallResult.Failed("安装已取消。");
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return ResourceInstallResult.Failed("下载失败：" + ex.Message);
        }
    }

    private Task<IReadOnlyList<ResourceVersionFile>> LoadVersionsAsync(
        ResourceProject project,
        string? minecraftVersion,
        string? loader,
        CancellationToken cancellationToken) {
        return project.Source switch {
            ResourceSource.CurseForge => new CurseForgeResourceService(
                Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), _fetcher)
                .GetFilesAsync(project.Id, project.Kind, minecraftVersion, loader, cancellationToken),
            _ => new ModrinthResourceService(_fetcher)
                .GetVersionsAsync(project.Id, project.Kind, minecraftVersion, loader, cancellationToken)
        };
    }

}
