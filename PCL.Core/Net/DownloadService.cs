using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using PCL.Core.Tasks;

namespace PCL.Core.Net;

public sealed class DownloadRequest {

    public string Url { get; init; } = "";
    public IReadOnlyList<string> BackupUrls { get; init; } = Array.Empty<string>();
    public string TargetPath { get; init; } = "";
    public bool Overwrite { get; init; } = true;
    public bool AllowResume { get; init; } = true;
    public bool SimulateBrowserHeaders { get; init; }
    public string? Sha256 { get; init; }
    public long? MinSize { get; init; }
    public int MaxRetries { get; init; } = 2;
    public int TimeoutSeconds { get; init; } = 30;
    public long SpeedLimitBytesPerSecond { get; init; }
    public int ParallelCount { get; init; } = 1;
    public long MinPartSize { get; init; } = 1024 * 1024;

}

public sealed class DownloadProgress {

    public long BytesReceived { get; init; }
    public long? TotalBytes { get; init; }
    public double BytesPerSecond { get; init; }
    public double Percent => TotalBytes is > 0 ? Math.Min(100d, BytesReceived * 100d / TotalBytes.Value) : 0d;

}

public sealed class DownloadResult {

    public bool Success { get; init; }
    public string Path { get; init; } = "";
    public long Bytes { get; init; }
    public string SourceUrl { get; init; } = "";

}

public interface IDownloadService {
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 单文件下载：多源回退、断点续传、重试、限速、SHA-256 校验。
/// </summary>
public sealed class HttpDownloadService : IDownloadService, IDisposable {

    private readonly HttpClient _client;

    public HttpDownloadService() {
        var handler = new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true
        };
        _client = new HttpClient(handler) {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CEU/1.0");
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) {        if (string.IsNullOrWhiteSpace(request.Url)) throw new ArgumentException("下载 URL 不能为空。", nameof(request));
        if (string.IsNullOrWhiteSpace(request.TargetPath)) throw new ArgumentException("下载目标路径不能为空。", nameof(request));

        var target = System.IO.Path.GetFullPath(request.TargetPath);
        var parent = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        if (!request.Overwrite && File.Exists(target)) {
            return new DownloadResult { Success = true, Path = target, Bytes = new FileInfo(target).Length };
        }

        var partial = target + ".part";
        if (!request.AllowResume) TryDelete(partial);

        var urls = new[] { request.Url }.Concat(request.BackupUrls ?? Array.Empty<string>())
            .Where(url => !string.IsNullOrWhiteSpace(url)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Exception? lastError = null;

        foreach (var url in urls) {
            for (var attempt = 0; attempt <= request.MaxRetries; attempt++) {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    await DownloadFromSourceAsync(url, partial, request, progress, cancellationToken).ConfigureAwait(false);
                    Verify(partial, request);
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(partial, target);
                    return new DownloadResult {
                        Success = true,
                        Path = target,
                        Bytes = new FileInfo(target).Length,
                        SourceUrl = url
                    };
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) {
                    lastError = ex;
                    if (attempt < request.MaxRetries) {
                        await Task.Delay(300 * (attempt + 1), cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }

        throw new HttpRequestException("文件下载失败：" + request.Url, lastError);
    }

    /// <summary>
    /// 使用统一的 C# 任务状态对象执行下载。
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, ProgressTask task) {
        task.Begin();
        try {
            var result = await DownloadAsync(
                request,
                new Progress<DownloadProgress>(value => task.Report(value.Percent / 100d)),
                task.CancellationToken).ConfigureAwait(false);
            task.Report(1);
            task.Complete();
            return result;
        } catch (OperationCanceledException) {
            task.MarkCanceled();
            throw;
        } catch (Exception ex) {
            task.FailTask(ex);
            throw;
        }
    }

    private async Task DownloadFromSourceAsync(
        string url,
        string partial,
        DownloadRequest request,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken) {        var offset = request.AllowResume && File.Exists(partial) ? new FileInfo(partial).Length : 0;
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        if (offset > 0) message.Headers.Range = new RangeHeaderValue(offset, null);
        if (request.SimulateBrowserHeaders) {
            message.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            message.Headers.TryAddWithoutValidation("Referer", "https://www.minecraft.net/");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds)));
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) {
            TryDelete(partial);
            throw new HttpRequestException("服务器拒绝了断点续传范围。");
        }
        if (offset > 0 && response.StatusCode != HttpStatusCode.PartialContent) {
            offset = 0;
            TryDelete(partial);
        }
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength is { } length ? offset + length : (long?)null;
        var mode = offset > 0 ? FileMode.Append : FileMode.Create;
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new FileStream(partial, mode, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        var speedWatch = Stopwatch.StartNew();
        var reportWatch = Stopwatch.StartNew();
        long received = offset;
        long receivedThisCall = 0;

        while (true) {
            var read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
            if (read <= 0) break;
            await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
            received += read;
            receivedThisCall += read;

            if (request.SpeedLimitBytesPerSecond > 0) {
                var expectedMs = receivedThisCall * 1000d / request.SpeedLimitBytesPerSecond;
                var delay = expectedMs - speedWatch.Elapsed.TotalMilliseconds;
                if (delay > 0) await Task.Delay((int)Math.Min(delay, 1000), cancellationToken).ConfigureAwait(false);
            }

            if (progress is not null && reportWatch.ElapsedMilliseconds >= 100) {
                reportWatch.Restart();
                progress.Report(new DownloadProgress {
                    BytesReceived = received,
                    TotalBytes = total,
                    BytesPerSecond = receivedThisCall / Math.Max(0.001, speedWatch.Elapsed.TotalSeconds)
                });
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        if (total is not null && received != total.Value) throw new IOException("下载长度与服务器声明不一致。");
        progress?.Report(new DownloadProgress {
            BytesReceived = received,
            TotalBytes = total ?? received,
            BytesPerSecond = receivedThisCall / Math.Max(0.001, speedWatch.Elapsed.TotalSeconds)
        });
    }

    private static void Verify(string path, DownloadRequest request) {
        var info = new FileInfo(path);
        if (request.MinSize is { } min && info.Length < min) throw new InvalidDataException($"文件大小不足：{info.Length} < {min}");
        if (string.IsNullOrWhiteSpace(request.Sha256)) return;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (!string.Equals(hash, request.Sha256, StringComparison.OrdinalIgnoreCase)) {
            TryDelete(path);
            throw new InvalidDataException("文件 SHA-256 校验失败。");
        }
    }

    private static void TryDelete(string path) {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public void Dispose() => _client.Dispose();

}
