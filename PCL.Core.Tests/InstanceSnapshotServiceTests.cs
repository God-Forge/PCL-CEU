using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceSnapshotServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-snapshot-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 创建并恢复实例快照() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "versions", "test");
            Directory.CreateDirectory(Path.Combine(instance, "mods"));
            Directory.CreateDirectory(Path.Combine(instance, "config"));
            File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "mod");
            File.WriteAllText(Path.Combine(instance, "config", "a.toml"), "config");
            File.WriteAllText(Path.Combine(instance, "options.txt"), "lang:zh_cn");

            var snapshotRoot = Path.Combine(root, "snapshots");
            var snapshot = InstanceSnapshotService.Create(instance, snapshotRoot);
            await Assert.That(File.Exists(snapshot.Path)).IsTrue();
            await Assert.That(InstanceSnapshotService.List(instance, snapshotRoot).Count).IsEqualTo(1);

            File.Delete(Path.Combine(instance, "mods", "a.jar"));
            File.Delete(Path.Combine(instance, "config", "a.toml"));
            InstanceSnapshotService.Restore(snapshot.Path, instance);

            await Assert.That(File.Exists(Path.Combine(instance, "mods", "a.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(instance, "config", "a.toml"))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
