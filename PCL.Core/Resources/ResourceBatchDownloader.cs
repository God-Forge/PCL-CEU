using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>批量下载进度。</summary>
public readonly record struct ResourceBatchProgress(int Completed, int Total, string CurrentFile) {

    public double Percent => Total <= 0 ? 100 : Completed * 100.0 / Total;

}

/// <summary>单个文件的失败原因。</summary>
public sealed record ResourceBatchFailure(string FileName, string Message);

/// <summary>批量下载结果。</summary>
public sealed class ResourceBatchDownloadResult {

    public IReadOnlyList<string> Succeeded { get; init; } = [];
    public IReadOnlyList<ResourceBatchFailure> Failed { get; init; } = [];

    public int Total => Succeeded.Count + Failed.Count;
    public bool Success => Failed.Count == 0;

}

/// <summary>
/// 批量下载资源文件（含依赖）：按资源类型分目录，限制并发，单文件失败只记录不中断。
/// 对应 VB 版 ModDownloadLib 的批量下载部分。
/// </summary>
public sealed class ResourceBatchDownloader {

    private readonly IDownloadService _download;
    private readonly DownloadCacheService? _cache;

    public ResourceBatchDownloader(IDownloadService? download = null, DownloadCacheService? cache = null) {
        _download = download ?? new HttpDownloadService();
        _cache = cache;
    }

    /// <summary>
    /// 下载一组资源版本到实例目录。
    /// </summary>
    /// <param name="versions">待下载版本，通常来自依赖解析结果。</param>
    /// <param name="instanceFolder">实例根目录。</param>
    /// <param name="maxConcurrency">并发上限，至少为 1。</param>
    public async Task<ResourceBatchDownloadResult> DownloadAsync(
        IEnumerable<ResourceVersionFile> versions,
        string instanceFolder,
        int maxConcurrency = 3,
        IProgress<ResourceBatchProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(versions);
        var list = versions.ToList();
        if (list.Count == 0) return new ResourceBatchDownloadResult();

        var succeeded = new List<string>();
        var failed = new List<ResourceBatchFailure>();
        var gate = new object();
        var completed = 0;
        var total = list.Count;
        using var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));

        var tasks = list.Select(async version => {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                cancellationToken.ThrowIfCancellationRequested();
                var folder = ResourceInstallPaths.Resolve(instanceFolder, version.Kind);
                var request = ResourceDownloadPlanner.CreateRequest(version, folder);
                if (request is null) {
                    lock (gate) failed.Add(new ResourceBatchFailure(version.FileName, "无法生成下载任务（缺少地址或文件名）"));
                    return;
                }
                // 缓存命中时直接复用，不再请求网络
                if (_cache is not null && _cache.TryGet(request.Url, out var cached)) {
                    try {
                        var parent = Path.GetDirectoryName(Path.GetFullPath(request.TargetPath));
                        if (parent is not null) Directory.CreateDirectory(parent);
                        File.Copy(cached, request.TargetPath, true);
                        lock (gate) succeeded.Add(version.FileName);
                        return;
                    } catch (Exception ex) when (ex is not OutOfMemoryException) {
                        Logger.Warn(ex, "使用下载缓存失败，改为重新下载：" + request.Url);
                    }
                }
                DownloadResult result;
                try {
                    result = await _download.DownloadAsync(request, null, cancellationToken).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) when (ex is not OutOfMemoryException) {
                    lock (gate) failed.Add(new ResourceBatchFailure(version.FileName, ex.Message));
                    return;
                }
                lock (gate) {
                    if (result.Success) {
                        succeeded.Add(version.FileName);
                        if (_cache is not null && File.Exists(request.TargetPath)) {
                            try {
                                _cache.Store(request.Url, request.TargetPath);
                            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                                Logger.Warn(ex, "写入下载缓存失败：" + request.Url);
                            }
                        }
                    } else {
                        failed.Add(new ResourceBatchFailure(version.FileName, "下载失败：" + request.Url));
                    }
                }
            } finally {
                var current = Interlocked.Increment(ref completed);
                progress?.Report(new ResourceBatchProgress(current, total, version.FileName));
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return new ResourceBatchDownloadResult {
            Succeeded = succeeded,
            Failed = failed
        };
    }

}
