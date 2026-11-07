using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>一次资源安装的请求。</summary>
public sealed class ResourceInstallRequest {

    public ResourceVersionFile Root { get; init; } = new();
    public string InstanceFolder { get; init; } = "";

    /// <summary>用于筛选兼容版本的游戏版本。</summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>加载器（fabric/forge/neoforge/quilt）。</summary>
    public string? Loader { get; init; }

    /// <summary>是否一并安装必需依赖。</summary>
    public bool IncludeDependencies { get; init; } = true;

    /// <summary>并发下载上限。</summary>
    public int MaxConcurrency { get; init; } = 3;

}

/// <summary>安装报告。</summary>
public sealed class ResourceInstallReport {

    public bool Success { get; init; }
    public IReadOnlyList<string> Installed { get; init; } = [];
    public IReadOnlyList<ResourceBatchFailure> Failed { get; init; } = [];

    /// <summary>未能解析的依赖项目 ID。</summary>
    public IReadOnlyList<string> Unresolved { get; init; } = [];

    /// <summary>因循环或深度限制被跳过的项目 ID。</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    public string Message { get; init; } = "";

}

/// <summary>
/// 完整的资源安装流程：解析依赖 → 批量下载 → 汇总报告。
/// 对应 VB 版 ModDownloadLib 的安装主流程。
/// </summary>
public sealed class ResourceInstaller {

    private readonly IResourceVersionSource _source;
    private readonly IDownloadService _download;

    public ResourceInstaller(IResourceVersionSource? source = null, IDownloadService? download = null) {
        _source = source ?? new ModrinthResourceService();
        _download = download ?? new HttpDownloadService();
    }

    /// <summary>
    /// 直接从资源项目安装：先查版本并挑选兼容的最新文件，再走完整安装流程。
    /// </summary>
    /// <summary>
    /// 只解析依赖并返回计划，不下载任何文件。用于展示依赖图与安装前预览。
    /// </summary>
    public async Task<ResourceDependencyPlan?> PreviewDependenciesAsync(
        ResourceProject project,
        string? minecraftVersion = null,
        string? loader = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        IReadOnlyList<ResourceVersionFile> versions;
        try {
            versions = await _source
                .GetVersionsAsync(project.Id, project.Kind, minecraftVersion, loader, cancellationToken)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "获取版本列表失败，无法解析依赖");
            return null;
        }
        var latest = ResourceVersionSelector.SelectLatest(versions, minecraftVersion, loader);
        if (latest is null) return null;
        return await ResourceDependencyResolver
            .ResolveAsync(latest, _source, minecraftVersion, loader, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ResourceInstallReport> InstallProjectAsync(
        ResourceProject project,
        string instanceFolder,
        string? minecraftVersion = null,
        string? loader = null,
        bool includeDependencies = true,
        int maxConcurrency = 3,
        IProgress<ResourceBatchProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(project);
        IReadOnlyList<ResourceVersionFile> versions;
        try {
            versions = await _source
                .GetVersionsAsync(project.Id, project.Kind, minecraftVersion, loader, cancellationToken)
                .ConfigureAwait(false);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return new ResourceInstallReport { Success = false, Message = "获取版本列表失败：" + ex.Message };
        }
        var latest = ResourceVersionSelector.SelectLatest(versions, minecraftVersion, loader);
        if (latest is null) {
            return new ResourceInstallReport { Success = false, Message = "没有与该游戏版本匹配的可用文件。" };
        }
        return await InstallAsync(new ResourceInstallRequest {
            Root = latest,
            InstanceFolder = instanceFolder,
            MinecraftVersion = minecraftVersion,
            Loader = loader,
            IncludeDependencies = includeDependencies,
            MaxConcurrency = maxConcurrency
        }, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ResourceInstallReport> InstallAsync(
        ResourceInstallRequest request,
        IProgress<ResourceBatchProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.InstanceFolder)) {
            throw new ArgumentException("实例目录不能为空", nameof(request.InstanceFolder));
        }
        if (!ResourceInstallPaths.CanInstallAsFile(request.Root.Kind)) {
            return new ResourceInstallReport { Success = false, Message = "整合包需要专用安装流程。" };
        }

        IReadOnlyList<ResourceVersionFile> files;
        IReadOnlyList<string> unresolved = [];
        IReadOnlyList<string> skipped = [];
        if (request.IncludeDependencies) {
            var plan = await ResourceDependencyResolver.ResolveAsync(
                request.Root, _source, request.MinecraftVersion, request.Loader,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            files = plan.Files;
            unresolved = plan.Unresolved;
            skipped = plan.Skipped;
        } else {
            files = [request.Root];
        }

        var result = await new ResourceBatchDownloader(_download)
            .DownloadAsync(files, request.InstanceFolder, request.MaxConcurrency, progress, cancellationToken)
            .ConfigureAwait(false);

        var message = $"已安装 {result.Succeeded.Count} 个文件";
        if (result.Failed.Count > 0) message += $"，失败 {result.Failed.Count} 个";
        if (unresolved.Count > 0) message += $"，{unresolved.Count} 个依赖未能解析";
        return new ResourceInstallReport {
            Success = result.Failed.Count == 0 && unresolved.Count == 0,
            Installed = result.Succeeded,
            Failed = result.Failed,
            Unresolved = unresolved,
            Skipped = skipped,
            Message = message
        };
    }

}
