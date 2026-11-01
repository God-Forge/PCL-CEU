using PCL.Core.Minecraft;
using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class AdvancedAuditServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 统计磁盘占用与存档() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "mc", "versions", "test");
            Directory.CreateDirectory(Path.Combine(instance, "mods"));
            Directory.CreateDirectory(Path.Combine(instance, "saves", "world"));
            File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), new string('x', 100));
            File.WriteAllText(Path.Combine(instance, "saves", "world", "level.dat"), new string('y', 50));

            var disk = AdvancedAuditService.AnalyzeDiskUsage(instance);
            await Assert.That(disk.Categories["mods"]).IsEqualTo(100);
            await Assert.That(disk.Categories["saves"]).IsEqualTo(50);

            var worlds = AdvancedAuditService.ListWorlds(instance);
            await Assert.That(worlds.Count).IsEqualTo(1);
            await Assert.That(worlds[0].Name).IsEqualTo("world");
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 生成Mod依赖图() {
        var graph = AdvancedAuditService.BuildModGraph([
            new ModInfo { Name = "A", FileName = "a.jar", ModId = "a", Dependencies = [new ModDependency { Id = "b" }] },
            new ModInfo { Name = "B", FileName = "b.jar", ModId = "b" }
        ]);
        await Assert.That(graph).Contains("A（a.jar）→ b");
        await Assert.That(graph).Contains("B（b.jar）→ 无前置");
    }

}
