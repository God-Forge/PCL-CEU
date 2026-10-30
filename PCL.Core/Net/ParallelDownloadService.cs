using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace PCL.Core.Net;

/// <summary>
/// 多分片下载。服务器不支持 Range 时自动退化为单线程 HttpDownloadService。
/// </summary>
public sealed class ParallelDownloadService : IDownloadService, IDisposable {

    private readonly HttpDownloadService _single = new();
    private readonly HttpClient _client;

    public ParallelDownloadService() {
        var handler = new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.None,
            AllowAutoRedirect = true
        };
        _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CEU/1.0");
    }

    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        if (request.ParallelCount <= 1) {
            return await _single.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
        }

        var target = System.IO.Path.GetFullPath(request.TargetPath);
        var parent = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        if (!request.Overwrite && File.Exists(target)) {
            return new DownloadResult { Success = true, Path = target, Bytes = new FileInfo(target).Length };
        }

        long? total = null;
        try {
            total = await ProbeLengthAsync(request.Url, cancellationToken).ConfigureAwait(false);
        } catch {
            total = null;
        }
        if (total is null || total.Value < Math.Max(1, request.MinPartSize) * 2) {
            return await _single.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
        }

        var parts = Math.Min(request.ParallelCount, (int)Math.Max(1, total.Value / Math.Max(1, request.MinPartSize)));
        var partFiles = new string[parts];
        var partRanges = new (long Start, long End)[parts];
        var segment = total.Value / parts;
        for (var i = 0; i < parts; i++) {
            var start = i * segment;
            var end = i == parts - 1 ? total.Value - 1 : (i + 1) * segment - 1;
            partRanges[i] = (start, end);
            partFiles[i] = target + ".part" + i;
        }

        long completed = 0;
        var reportWatch = Stopwatch.StartNew();
        var progressLock = new object();
        var speedWatch = Stopwatch.StartNew();

        try {
            await Task.WhenAll(partRanges.Select((range, index) => DownloadPartAsync(
                request.Url, partFiles[index], range.Start, range.End, request,
                read => {
                    var value = Interlocked.Add(ref completed, read);
                    if (progress is null || reportWatch.ElapsedMilliseconds < 100) return;
                    lock (progressLock) {
                        if (reportWatch.ElapsedMilliseconds < 100) return;
                        reportWatch.Restart();
                        progress.Report(new DownloadProgress {
                            BytesReceived = value,
                            TotalBytes = total,
                            BytesPerSecond = value / Math.Max(0.001, speedWatch.Elapsed.TotalSeconds)
                        });
                    }
                }, cancellationToken))).ConfigureAwait(false);

            var partial = target + ".part";
            using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true)) {
                foreach (var part in partFiles) {
                    using var input = File.OpenRead(part);
                    await input.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
                }
            }
            Verify(partial, request);
            if (File.Exists(target)) File.Delete(target);
            File.Move(partial, target);
            progress?.Report(new DownloadProgress { BytesReceived = total.Value, TotalBytes = total, BytesPerSecond = total.Value / Math.Max(0.001, speedWatch.Elapsed.TotalSeconds) });
            return new DownloadResult {
                Success = true,
                Path = target,
                Bytes = new FileInfo(target).Length,
                SourceUrl = request.Url
            };
        } catch (Exception ex) when (ex is not OperationCanceledException) {
            foreach (var part in partFiles) TryDelete(part);
            TryDelete(target + ".part");
            return await _single.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
        } finally {
            foreach (var part in partFiles) TryDelete(part);
        }
    }

    private async Task<long?> ProbeLengthAsync(string url, CancellationToken cancellationToken) {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        message.Headers.Range = new RangeHeaderValue(0, 0);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.PartialContent) return null;
        return response.Content.Headers.ContentRange?.Length;
    }

    private async Task DownloadPartAsync(
        string url,
        string path,
        long start,
        long end,
        DownloadRequest request,
        Action<int> onRead,
        CancellationToken cancellationToken) {
        Exception? last = null;
        for (var attempt = 0; attempt <= request.MaxRetries; attempt++) {
            try {
                using var message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.Range = new RangeHeaderValue(start, end);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, request.TimeoutSeconds)));
                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.StatusCode != HttpStatusCode.PartialContent) throw new HttpRequestException("服务器不支持分片下载。");
                response.EnsureSuccessStatusCode();
                using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                long written = 0;
                while (true) {
                    var read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false);
                    if (read <= 0) break;
                    await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false);
                    written += read;
                    onRead(read);
                }
                if (written != end - start + 1) throw new IOException("分片长度不一致。");
                return;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                last = ex;
                if (attempt < request.MaxRetries) await Task.Delay(300 * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
        }
        throw new HttpRequestException("分片下载失败。", last);
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

    public void Dispose() {
        _single.Dispose();
        _client.Dispose();
    }

}
