using System.Net.Http;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class NetFileLoaderTests {

    private sealed class FakeDownloadService : IDownloadService {

        private readonly bool _fail;

        public FakeDownloadService(bool fail = false) {
            _fail = fail;
        }

        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default) {
            if (_fail) throw new HttpRequestException("fake failure");
            var content = System.Text.Encoding.UTF8.GetBytes(request.TargetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(request.TargetPath)!);
            File.WriteAllBytes(request.TargetPath, content);
            progress?.Report(new DownloadProgress { BytesReceived = content.Length, TotalBytes = content.Length });
            return Task.FromResult(new DownloadResult {
                Success = true,
                Path = request.TargetPath,
                Bytes = content.Length,
                SourceUrl = request.Url
            });
        }

    }

    private sealed class SynchronousProgress(Action<NetLoaderProgress> action) : IProgress<NetLoaderProgress> {

        public void Report(NetLoaderProgress value) => action(value);

    }

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-netloader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 下载多个文件并聚合进度() {
        var root = CreateRoot();
        try {
            var files = new[] {
                new NetFileTask { Name = "a", TargetPath = Path.Combine(root, "a.bin"), Sources = ["https://example.com/a"] },
                new NetFileTask { Name = "b", TargetPath = Path.Combine(root, "b.bin"), Sources = ["https://example.com/b"] }
            };
            var progress = new System.Collections.Concurrent.ConcurrentBag<NetLoaderProgress>();
            var loader = new NetLoader(new FakeDownloadService(), new FakeDownloadService());
            var result = await loader.DownloadAsync(files, new SynchronousProgress(progress.Add), maxConcurrency: 2);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(files.All(file => file.State == NetFileState.Finished)).IsTrue();
            await Assert.That(progress.Any(value => value.FilesCompleted == 2)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 单个文件失败不影响其他文件状态() {
        var root = CreateRoot();
        try {
            var good = new NetFileTask { Name = "good", TargetPath = Path.Combine(root, "good.bin"), Sources = ["https://example.com/good"] };
            var bad = new NetFileTask { Name = "bad", TargetPath = Path.Combine(root, "bad.bin"), Sources = ["https://example.com/bad"], UseParallel = true };
            var loader = new NetLoader(new FakeDownloadService(), new FakeDownloadService(fail: true));
            var result = await loader.DownloadAsync(new[] { good, bad }, maxConcurrency: 2);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(good.State).IsEqualTo(NetFileState.Finished);
            await Assert.That(bad.State).IsEqualTo(NetFileState.Failed);
            await Assert.That(bad.Error).IsNotNull();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
