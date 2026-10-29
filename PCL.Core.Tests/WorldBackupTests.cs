using System.IO.Compression;
using System.Text;
using PCL.Core.Worlds;

namespace PCL.Core.Tests;

public class WorldBackupTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-world-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 备份列表与恢复() {
        var root = CreateRoot();
        var world = Path.Combine(root, "world");
        var backupRoot = Path.Combine(root, "backups");
        var target = Path.Combine(root, "restored");
        Directory.CreateDirectory(Path.Combine(world, "region"));
        File.WriteAllText(Path.Combine(world, "level.dat"), "level", Encoding.UTF8);
        File.WriteAllText(Path.Combine(world, "region", "r.0.0.mca"), "region", Encoding.UTF8);

        try {
            var service = new WorldBackupService();
            var backup = service.CreateBackup(world, backupRoot, "instance", "world");
            await Assert.That(File.Exists(backup.Path)).IsTrue();

            var list = service.ListBackups(backupRoot, "instance");
            await Assert.That(list.Count).IsEqualTo(1);
            await Assert.That(list[0].WorldName).IsEqualTo("world");

            service.Restore(backup.Path, target, backupRoot, "instance");
            await Assert.That(File.ReadAllText(Path.Combine(target, "level.dat"))).IsEqualTo("level");
            await Assert.That(File.ReadAllText(Path.Combine(target, "region", "r.0.0.mca"))).IsEqualTo("region");
            await Assert.That(File.Exists(Path.Combine(target, "_pcl_backup.json"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 恢复前会创建安全备份() {
        var root = CreateRoot();
        var world = Path.Combine(root, "world");
        var backupRoot = Path.Combine(root, "backups");
        Directory.CreateDirectory(world);
        File.WriteAllText(Path.Combine(world, "level.dat"), "new", Encoding.UTF8);

        try {
            var service = new WorldBackupService();
            var backup = service.CreateBackup(world, backupRoot, "instance", "world");
            File.WriteAllText(Path.Combine(world, "level.dat"), "old", Encoding.UTF8);

            service.Restore(backup.Path, world, backupRoot, "instance");
            await Assert.That(File.ReadAllText(Path.Combine(world, "level.dat"))).IsEqualTo("new");
            await Assert.That(service.ListBackups(backupRoot, "instance").Any(item => item.Kind == WorldBackupKind.PreRestore)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 保留数量裁剪() {
        var root = CreateRoot();
        var world = Path.Combine(root, "world");
        var backupRoot = Path.Combine(root, "backups");
        Directory.CreateDirectory(world);
        File.WriteAllText(Path.Combine(world, "level.dat"), "x", Encoding.UTF8);

        try {
            var service = new WorldBackupService();
            for (var i = 0; i < 4; i++) service.CreateBackup(world, backupRoot, "instance", "world");
            service.Prune(backupRoot, "instance", 2);
            await Assert.That(service.ListBackups(backupRoot, "instance").Count).IsEqualTo(2);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 拒绝路径穿越() {
        var root = CreateRoot();
        var backupRoot = Path.Combine(root, "backups");
        var target = Path.Combine(root, "target");
        var evil = Path.Combine(backupRoot, "evil.zip");
        Directory.CreateDirectory(backupRoot);

        using (var zip = ZipFile.Open(evil, ZipArchiveMode.Create)) {
            var entry = zip.CreateEntry("../evil.txt");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write("bad");
        }

        try {
            var service = new WorldBackupService();
            await Assert.That(() => service.Restore(evil, target, backupRoot, "instance")).Throws<InvalidDataException>();
            await Assert.That(File.Exists(Path.Combine(root, "evil.txt"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
