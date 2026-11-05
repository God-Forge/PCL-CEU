using PCL.Core.Mods;
using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ModUpdateApplierTests {

    private sealed class FakeDownload(bool success = true, bool writeFile = true) : IDownloadService {
        public List<DownloadRequest> Requests { get; } = [];

        public Task<DownloadResult> DownloadAsync(
            DownloadRequest request, IProgress<DownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) {
            Requests.Add(request);
            if (success && writeFile) {
                Directory.CreateDirectory(Path.GetDirectoryName(request.TargetPath)!);
                File.WriteAllText(request.TargetPath, "new-version");
            }
            return Task.FromResult(new DownloadResult {
                Success = success, Path = request.TargetPath, SourceUrl = request.Url
            });
        }
    }

    private static ModUpdateResult Update(string modId = "sodium") => new() {
        ModId = modId,
        LocalVersion = "1.2.0",
        LatestVersion = "1.3.0",
        ProjectSlug = modId,
        HasUpdate = true,
        LatestFile = new ResourceVersionFile {
            Id = "v2",
            Kind = ResourceKind.Mod,
            FileName = modId + "-1.3.0.jar",
            DownloadUrls = ["https://cdn.example/" + modId + "-1.3.0.jar"]
        }
    };

    private static (string ModsFolder, string OldFile) Setup() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-apply-" + Guid.NewGuid().ToString("N"));
        var mods = Path.Combine(root, "mods");
        Directory.CreateDirectory(mods);
        var old = Path.Combine(mods, "sodium-1.2.0.jar");
        File.WriteAllText(old, "old-version");
        return (mods, old);
    }

    [Test]
    public async Task 成功更新并保留备份() {
        var (mods, old) = Setup();
        try {
            var result = await new ModUpdateApplier(new FakeDownload()).ApplyAsync(Update(), mods, old);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(old)).IsFalse();
            await Assert.That(File.ReadAllText(result.NewPath)).IsEqualTo("new-version");
            await Assert.That(result.BackupPath).IsNotNull();
            await Assert.That(File.ReadAllText(result.BackupPath!)).IsEqualTo("old-version");
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 下载失败时原文件不动() {
        var (mods, old) = Setup();
        try {
            var result = await new ModUpdateApplier(new FakeDownload(success: false)).ApplyAsync(Update(), mods, old);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(File.Exists(old)).IsTrue();
            await Assert.That(File.ReadAllText(old)).IsEqualTo("old-version");
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 下载声称成功但没落盘时失败() {
        var (mods, old) = Setup();
        try {
            var result = await new ModUpdateApplier(new FakeDownload(writeFile: false)).ApplyAsync(Update(), mods, old);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(File.Exists(old)).IsTrue();
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 没有可用更新时拒绝() {
        var (mods, old) = Setup();
        try {
            var noUpdate = new ModUpdateResult { ModId = "x", HasUpdate = false };
            var result = await new ModUpdateApplier(new FakeDownload()).ApplyAsync(noUpdate, mods, old);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("没有可应用");
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 原文件不存在时拒绝() {
        var (mods, old) = Setup();
        try {
            var result = await new ModUpdateApplier(new FakeDownload())
                .ApplyAsync(Update(), mods, Path.Combine(mods, "missing.jar"));
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("原文件不存在");
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 临时文件在成功后不留残留() {
        var (mods, old) = Setup();
        try {
            await new ModUpdateApplier(new FakeDownload()).ApplyAsync(Update(), mods, old);
            var leftovers = Directory.GetFiles(mods, "*.downloading", SearchOption.AllDirectories);
            await Assert.That(leftovers.Length).IsEqualTo(0);
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

    [Test]
    public async Task 备份目录固定为_backup() {
        var (mods, old) = Setup();
        try {
            var result = await new ModUpdateApplier(new FakeDownload()).ApplyAsync(Update(), mods, old);
            await Assert.That(result.BackupPath!).Contains(ModUpdateApplier.BackupFolderName);
        } finally {
            Directory.Delete(Path.GetDirectoryName(mods)!, true);
        }
    }

}
