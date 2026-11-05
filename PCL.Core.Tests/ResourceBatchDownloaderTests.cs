using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceBatchDownloaderTests {

    private sealed class FakeDownload(Func<DownloadRequest, bool>? shouldSucceed = null) : IDownloadService {
        private readonly object _gate = new();
        private int _running;
        public List<DownloadRequest> Requests { get; } = [];
        public int MaxConcurrency { get; private set; }

        public async Task<DownloadResult> DownloadAsync(
            DownloadRequest request, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            lock (_gate) {
                Requests.Add(request);
                _running++;
                if (_running > MaxConcurrency) MaxConcurrency = _running;
            }
            try {
                await Task.Delay(10, cancellationToken);
            } finally {
                lock (_gate) _running--;
            }
            var ok = shouldSucceed?.Invoke(request) ?? true;
            return new DownloadResult { Success = ok, Path = request.TargetPath, SourceUrl = request.Url };
        }
    }

    private sealed class ThrowingDownload : IDownloadService {
        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("boom");
    }

    private static ResourceVersionFile Version(string name, ResourceKind kind = ResourceKind.Mod, bool withUrl = true) => new() {
        Id = name,
        Kind = kind,
        FileName = name,
        DownloadUrls = withUrl ? ["https://cdn.example/" + name] : [],
        Size = 100
    };

    [Test]
    public async Task 全部成功时返回成功列表() {
        var download = new FakeDownload();
        var result = await new ResourceBatchDownloader(download)
            .DownloadAsync([Version("a.jar"), Version("b.jar")], @"C:\mc\1.20.1");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Succeeded.Count).IsEqualTo(2);
        await Assert.That(result.Failed.Count).IsEqualTo(0);
        await Assert.That(download.Requests.Count).IsEqualTo(2);
    }

    [Test]
    public async Task 按资源类型分目录() {
        var download = new FakeDownload();
        await new ResourceBatchDownloader(download).DownloadAsync([
            Version("a.jar", ResourceKind.Mod),
            Version("b.zip", ResourceKind.ResourcePack),
            Version("c.zip", ResourceKind.ShaderPack)
        ], @"C:\mc\1.20.1");
        await Assert.That(download.Requests[0].TargetPath).Contains(@"mods");
        await Assert.That(download.Requests[1].TargetPath).Contains(@"resourcepacks");
        await Assert.That(download.Requests[2].TargetPath).Contains(@"shaderpacks");
    }

    [Test]
    public async Task 单个失败不影响其余() {
        var download = new FakeDownload(request => !request.TargetPath.EndsWith("b.jar", StringComparison.OrdinalIgnoreCase));
        var result = await new ResourceBatchDownloader(download)
            .DownloadAsync([Version("a.jar"), Version("b.jar")], @"C:\mc\1.20.1");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Succeeded.Count).IsEqualTo(1);
        await Assert.That(result.Failed.Count).IsEqualTo(1);
        await Assert.That(result.Failed[0].FileName).IsEqualTo("b.jar");
    }

    [Test]
    public async Task 缺少下载地址时记为失败() {
        var result = await new ResourceBatchDownloader(new FakeDownload())
            .DownloadAsync([Version("no-url.jar", withUrl: false)], @"C:\mc\1.20.1");
        await Assert.That(result.Failed[0].Message).Contains("无法生成下载任务");
    }

    [Test]
    public async Task 下载抛异常时记为失败而不中断() {
        var result = await new ResourceBatchDownloader(new ThrowingDownload())
            .DownloadAsync([Version("a.jar"), Version("b.jar")], @"C:\mc\1.20.1");
        await Assert.That(result.Failed.Count).IsEqualTo(2);
        await Assert.That(result.Failed.All(item => item.Message.Contains("boom"))).IsTrue();
    }

    [Test]
    public async Task 并发数不超过上限() {
        var download = new FakeDownload();
        await new ResourceBatchDownloader(download).DownloadAsync(
            Enumerable.Range(0, 8).Select(index => Version($"m{index}.jar")), @"C:\mc\1.20.1", maxConcurrency: 2);
        await Assert.That(download.MaxConcurrency).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task 进度逐条上报() {
        var reports = new List<ResourceBatchProgress>();
        var progress = new Progress<ResourceBatchProgress>(value => { lock (reports) reports.Add(value); });
        await new ResourceBatchDownloader(new FakeDownload())
            .DownloadAsync([Version("a.jar"), Version("b.jar")], @"C:\mc\1.20.1", 1, progress);
        // Progress<T> 走同步上下文，等待回调落定
        for (var i = 0; i < 50 && reports.Count < 2; i++) await Task.Delay(10);
        List<ResourceBatchProgress> snapshot;
        lock (reports) snapshot = reports.ToList();
        await Assert.That(snapshot.Count).IsEqualTo(2);
        await Assert.That(snapshot[^1].Completed).IsEqualTo(2);
        await Assert.That(snapshot[^1].Total).IsEqualTo(2);
        await Assert.That(snapshot[^1].Percent).IsEqualTo(100);
    }

    [Test]
    public async Task 空集合直接返回成功() {
        var result = await new ResourceBatchDownloader(new FakeDownload())
            .DownloadAsync([], @"C:\mc\1.20.1");
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Total).IsEqualTo(0);
    }

}
