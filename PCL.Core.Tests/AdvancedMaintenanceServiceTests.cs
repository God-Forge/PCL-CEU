using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class AdvancedMaintenanceServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-maint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 导出离线整合包() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "mc", "versions", "test");
            Directory.CreateDirectory(Path.Combine(instance, "mods"));
            File.WriteAllText(Path.Combine(instance, "test.json"), """{"id":"test","libraries":[]}""");
            File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "mod");
            var output = Path.Combine(root, "offline.zip");
            AdvancedMaintenanceService.ExportOfflinePack(instance, Path.Combine(root, "mc"), output);
            await Assert.That(File.Exists(output)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 自动修复重复Mod() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "instance");
            var mods = Path.Combine(instance, "mods");
            Directory.CreateDirectory(mods);
            File.WriteAllText(Path.Combine(mods, "a.jar"), "same");
            File.WriteAllText(Path.Combine(mods, "b.jar"), "same");
            var result = AdvancedMaintenanceService.AutoFixMods(instance);
            await Assert.That(result.Any(line => line.Contains("删除重复"))).IsTrue();
            await Assert.That(Directory.EnumerateFiles(mods, "*.jar").Count()).IsEqualTo(1);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 提供JVM预设() {
        var presets = AdvancedMaintenanceService.JvmPresets();
        await Assert.That(presets.Count).IsEqualTo(3);
        await Assert.That(presets[1].Settings["VersionRamCustom"]).IsEqualTo(4096);
    }

}
