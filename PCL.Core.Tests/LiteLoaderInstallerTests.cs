using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LiteLoaderInstallerTests {

    private static string TempMc() => Path.Combine(Path.GetTempPath(), "pcl-ll-" + Guid.NewGuid().ToString("N"));

    private static void WriteVersion(string mcFolder, string folderName, string json) {
        var folder = Path.Combine(mcFolder, "versions", folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, folderName + ".json"), json);
    }

    [Test]
    public async Task 安装器地址按版本拼接() {
        var url = LiteLoaderInstaller.BuildDownloadUrl("1.7.10", "liteloader-installer-1.7.10-04.jar");
        await Assert.That(url).IsEqualTo("https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar");
    }

    [Test]
    public async Task 缺少参数时抛出异常() {
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(LiteLoaderInstaller.BuildDownloadUrl("", "a.jar")));
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(LiteLoaderInstaller.BuildDownloadUrl("1.12.2", " ")));
    }

    [Test]
    public async Task 能定位LiteLoader版本目录() {
        var mc = TempMc();
        try {
            WriteVersion(mc, "1.12.2-LiteLoader1.12.2", """{"id":"1.12.2-LiteLoader1.12.2","inheritsFrom":"1.12.2"}""");
            var found = LiteLoaderInstaller.FindLiteLoaderVersion(mc, "1.12.2");
            await Assert.That(found).IsEqualTo("1.12.2-LiteLoader1.12.2");
        } finally {
            if (Directory.Exists(mc)) Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 不是LiteLoader的版本不会被误认() {
        var mc = TempMc();
        try {
            WriteVersion(mc, "1.12.2-Forge", """{"id":"1.12.2-Forge"}""");
            await Assert.That(LiteLoaderInstaller.FindLiteLoaderVersion(mc, "1.12.2")).IsNull();
        } finally {
            if (Directory.Exists(mc)) Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 排除已有目录后返回空() {
        var mc = TempMc();
        try {
            WriteVersion(mc, "1.12.2-LiteLoader1.12.2", """{"id":"x","note":"LiteLoader"}""");
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "1.12.2-LiteLoader1.12.2" };
            await Assert.That(LiteLoaderInstaller.FindLiteLoaderVersion(mc, "1.12.2", exclude)).IsNull();
        } finally {
            if (Directory.Exists(mc)) Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 版本不匹配时返回空() {
        var mc = TempMc();
        try {
            WriteVersion(mc, "1.7.10-LiteLoader", """{"id":"x","note":"LiteLoader"}""");
            await Assert.That(LiteLoaderInstaller.FindLiteLoaderVersion(mc, "1.12.2")).IsNull();
        } finally {
            if (Directory.Exists(mc)) Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 安装器不存在时失败() {
        var result = await LiteLoaderInstaller.InstallAsync(
            "java.exe", Path.Combine(Path.GetTempPath(), "no-such-liteloader.jar"), TempMc(), "1.12.2");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("安装器不存在");
    }

    [Test]
    public async Task Java不存在时失败() {
        var mc = TempMc();
        var installer = Path.Combine(Path.GetTempPath(), "pcl-ll-" + Guid.NewGuid().ToString("N") + ".jar");
        try {
            Directory.CreateDirectory(mc);
            File.WriteAllBytes(installer, [0x50, 0x4B]);
            var result = await LiteLoaderInstaller.InstallAsync(
                Path.Combine(Path.GetTempPath(), "no-java.exe"), installer, mc, "1.12.2");
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("找不到 Java");
        } finally {
            if (File.Exists(installer)) File.Delete(installer);
            if (Directory.Exists(mc)) Directory.Delete(mc, true);
        }
    }

}
