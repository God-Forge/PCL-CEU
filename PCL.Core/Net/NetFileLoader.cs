namespace PCL.Core.Net;

public enum NetFileState {
    Waiting,
    Downloading,
    Finished,
    Failed,
    Canceled
}

public sealed class NetFileTask {

    public string Name { get; init; } = "";
    public string TargetPath { get; init; } = "";
    public IReadOnlyList<string> Sources { get; init; } = Array.Empty<string>();
    public string? Sha256 { get; init; }
    public long? MinSize { get; init; }
    public bool UseParallel { get; init; }
    public int ParallelCount { get; init; } = 4;
    public bool SimulateBrowserHeaders { get; init; }
    public NetFileState State { get; internal set; } = NetFileState.Waiting;
    public long BytesReceived { get; internal set; }
    public long? TotalBytes { get; internal set; }
    public Exception? Error { get; internal set; }

}

public sealed class NetLoaderProgress {

    public int FilesTotal { get; init; }
    public int FilesCompleted { get; init; }
    public int FilesFailed { get; init; }
    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }

}

public sealed class NetLoaderResult {

    public IReadOnlyList<NetFileTask> Files { get; init; } = Array.Empty<NetFileTask>();
    public bool Success => Files.All(file => file.State == NetFileState.Finished);

}

/// <summary>
/// 多文件任务引擎：并发下载、逐文件多源/续传/校验、聚合进度。
/// </summary>
public sealed class NetLoader {

    private readonly IDownloadService _single;
    private readonly IDownloadService _parallel;

    public NetLoader(IDownloadService? single = null, IDownloadService? parallel = null) {
        _single = single ?? new HttpDownloadService();
        _parallel = parallel ?? new ParallelDownloadService();
    }

    public async Task<NetLoaderResult> DownloadAsync(
        IEnumerable<NetFileTask> files,
        IProgress<NetLoaderProgress>? progress = null,
        int maxConcurrency = 4,
        long speedLimitBytesPerSecond = 0,
        CancellationToken cancellationToken = default) {
        var list = files.ToList();
        if (list.Count == 0) return new NetLoaderResult { Files = list };

        var totalBytes = list.Where(file => file.TotalBytes is > 0).Sum(file => file.TotalBytes ?? 0);
        long received = 0;
        var completed = 0;
        var failed = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var gate = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        var reportLock = new object();

        void Report() {
            if (progress is null) return;
            lock (reportLock) {
                progress.Report(new NetLoaderProgress {
                    FilesTotal = list.Count,
                    FilesCompleted = completed,
                    FilesFailed = failed,
                    BytesReceived = Interlocked.Read(ref received),
                    TotalBytes = totalBytes > 0 ? totalBytes : null,
                    BytesPerSecond = Interlocked.Read(ref received) / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds)
                });
            }
        }

        await Task.WhenAll(list.Select(async file => {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Sources.Count == 0) throw new InvalidOperationException("下载源为空。");
                file.State = NetFileState.Downloading;
                Report();

                var request = new DownloadRequest {
                    Url = file.Sources[0],
                    BackupUrls = file.Sources.Skip(1).ToList(),
                    TargetPath = file.TargetPath,
                    Overwrite = true,
                    AllowResume = true,
                    Sha256 = file.Sha256,
                    MinSize = file.MinSize,
                    ParallelCount = file.UseParallel ? file.ParallelCount : 1,
                    SimulateBrowserHeaders = file.SimulateBrowserHeaders,
                    SpeedLimitBytesPerSecond = speedLimitBytesPerSecond > 0
                        ? Math.Max(1, speedLimitBytesPerSecond / Math.Max(1, maxConcurrency))
                        : 0
                };

                var fileProgress = new Progress<DownloadProgress>(value => {
                    var delta = value.BytesReceived - file.BytesReceived;
                    file.BytesReceived = value.BytesReceived;
                    file.TotalBytes = value.TotalBytes;
                    if (delta > 0) Interlocked.Add(ref received, delta);
                    Report();
                });

                var service = file.UseParallel ? _parallel : _single;
                var result = await service.DownloadAsync(request, fileProgress, cancellationToken).ConfigureAwait(false);
                file.BytesReceived = result.Bytes;
                file.TotalBytes = result.Bytes;
                file.State = NetFileState.Finished;
                Interlocked.Increment(ref completed);
                Report();
            } catch (OperationCanceledException) {
                file.State = NetFileState.Canceled;
                Interlocked.Increment(ref failed);
                Report();
            } catch (Exception ex) {
                file.Error = ex;
                file.State = NetFileState.Failed;
                Interlocked.Increment(ref failed);
                Report();
            } finally {
                gate.Release();
            }
        })).ConfigureAwait(false);

        return new NetLoaderResult { Files = list };
    }

}
