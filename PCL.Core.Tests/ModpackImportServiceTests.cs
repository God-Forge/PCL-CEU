using PCL.Core.Modpack;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ModpackImportServiceTests {

    private sealed class NoopDownload : IDownloadService {
        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DownloadResult { Success = true, Path = request.TargetPath, SourceUrl = request.Url });
    }

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "pcl-mpimport-" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task 文件不存在时失败() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var result = await new ModpackImportService(new NoopDownload())
                .ImportAsync(Path.Combine(root, "no-such.zip"), Path.Combine(root, "versions"));
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("不存在");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 目标实例已存在时失败() {
        var root = TempRoot();
        try {
            var versions = Path.Combine(root, "versions");
            Directory.CreateDirectory(Path.Combine(versions, "占位"));
            var zip = Path.Combine(root, "占位.zip");
            File.WriteAllBytes(zip, [0x50, 0x4B]);
            var result = await new ModpackImportService(new NoopDownload())
                .ImportAsync(zip, versions);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("已存在");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 空版本目录时失败() {
        var result = await new ModpackImportService(new NoopDownload())
            .ImportAsync("x.zip", "");
        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task 损坏压缩包时返回失败而不抛异常() {
        var root = TempRoot();
        try {
            var versions = Path.Combine(root, "versions");
            Directory.CreateDirectory(versions);
            var zip = Path.Combine(root, "broken.zip");
            File.WriteAllBytes(zip, [0x50, 0x4B, 0x03, 0x04, 0xFF]);
            var result = await new ModpackImportService(new NoopDownload())
                .ImportAsync(zip, versions);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).IsNotEmpty();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
