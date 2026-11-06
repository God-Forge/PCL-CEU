using System.IO.Compression;
using PCL.Core.Minecraft;
using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ExportWithRulesTests {

    private static (string Root, string Instance, string McFolder) Setup() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-exp-" + Guid.NewGuid().ToString("N"));
        var mc = Path.Combine(root, "mc");
        var instance = Path.Combine(mc, "versions", "26.3");
        Directory.CreateDirectory(Path.Combine(instance, "mods"));
        Directory.CreateDirectory(Path.Combine(instance, "config"));
        Directory.CreateDirectory(Path.Combine(instance, "logs"));
        File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "mod");
        File.WriteAllText(Path.Combine(instance, "mods", "b.jar.disabled"), "disabled");
        File.WriteAllText(Path.Combine(instance, "config", "x.toml"), "cfg");
        File.WriteAllText(Path.Combine(instance, "logs", "latest.log"), "log");
        File.WriteAllText(Path.Combine(instance, "options.txt"), "options");
        return (root, instance, mc);
    }

    private static IReadOnlyList<string> Entries(string zipPath) {
        using var archive = ZipFile.OpenRead(zipPath);
        return archive.Entries.Select(entry => entry.FullName).ToList();
    }

    [Test]
    public async Task 规则只导出勾选的内容() {
        var (root, instance, mc) = Setup();
        try {
            var zip = Path.Combine(root, "out.zip");
            var rules = ExportRuleSet.Parse("mods/|!mods/*.disabled");
            AdvancedMaintenanceService.ExportOfflinePack(instance, mc, zip, new ModpackExportOptions(), rules);
            var entries = Entries(zip);
            await Assert.That(entries.Any(name => name.EndsWith("mods/a.jar"))).IsTrue();
            await Assert.That(entries.Any(name => name.Contains("b.jar.disabled"))).IsFalse();
            await Assert.That(entries.Any(name => name.Contains("config/x.toml"))).IsFalse();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 多个目录规则同时生效() {
        var (root, instance, mc) = Setup();
        try {
            var zip = Path.Combine(root, "out2.zip");
            var rules = ExportRuleSet.Parse("mods/|config/|options.txt");
            AdvancedMaintenanceService.ExportOfflinePack(instance, mc, zip, new ModpackExportOptions(), rules);
            var entries = Entries(zip);
            await Assert.That(entries.Any(name => name.EndsWith("mods/a.jar"))).IsTrue();
            await Assert.That(entries.Any(name => name.EndsWith("config/x.toml"))).IsTrue();
            await Assert.That(entries.Any(name => name.EndsWith("options.txt"))).IsTrue();
            await Assert.That(entries.Any(name => name.Contains("latest.log"))).IsFalse();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 空规则时退回粗粒度开关() {
        var (root, instance, mc) = Setup();
        try {
            var zip = Path.Combine(root, "out3.zip");
            // 传空规则集：应使用 ModpackExportOptions 的默认行为（含 mods，排除 logs）
            AdvancedMaintenanceService.ExportOfflinePack(instance, mc, zip, new ModpackExportOptions(), ExportRuleSet.Empty);
            var entries = Entries(zip);
            await Assert.That(entries.Any(name => name.EndsWith("mods/a.jar"))).IsTrue();
            await Assert.That(entries.Any(name => name.Contains("latest.log"))).IsFalse();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
