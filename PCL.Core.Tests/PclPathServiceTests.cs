using PCL.Core.Paths;

namespace PCL.Core.Tests;

public class PclPathServiceTests {

    [Test]
    public async Task 生成实例与资源路径() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-paths-" + Guid.NewGuid().ToString("N"));
        var cache = Path.Combine(root, "cache");
        try {
            var paths = new PclPathService(root, cache);
            await Assert.That(paths.GetInstanceJsonPath("demo")).IsEqualTo(Path.Combine(root, ".minecraft", "versions", "demo", "demo.json"));
            await Assert.That(paths.GetInstanceSetupPath("demo")).IsEqualTo(Path.Combine(root, ".minecraft", "versions", "demo", "PCL", "Setup.ini"));
            await Assert.That(paths.GetInstanceModsPath("demo")).EndsWith("mods\\");
            await Assert.That(paths.GetInstanceSavesPath("demo")).EndsWith("saves\\");
            await Assert.That(paths.TempCache).EndsWith("Cache\\");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 创建目录布局() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-paths-" + Guid.NewGuid().ToString("N"));
        try {
            var paths = new PclPathService(root, Path.Combine(root, "cache"));
            paths.EnsureDirectories();
            await Assert.That(Directory.Exists(paths.Pcl)).IsTrue();
            await Assert.That(Directory.Exists(paths.Minecraft)).IsTrue();
            await Assert.That(Directory.Exists(paths.Libraries)).IsTrue();
            await Assert.That(Directory.Exists(paths.Assets)).IsTrue();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}

