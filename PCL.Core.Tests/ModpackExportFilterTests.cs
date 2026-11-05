using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ModpackExportFilterTests {

    private static ModpackExportOptions Default() => new();

    [Test]
    public async Task 默认包含核心内容() {
        var options = Default();
        await Assert.That(ModpackExportFilter.ShouldInclude("mods/a.jar", options)).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("config/x.toml", options)).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("saves/world/level.dat", options)).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("minecraft-1.20.1.json", options)).IsTrue();
    }

    [Test]
    public async Task 默认排除日志与截图() {
        var options = Default();
        await Assert.That(ModpackExportFilter.ShouldInclude("logs/latest.log", options)).IsFalse();
        await Assert.That(ModpackExportFilter.ShouldInclude("crash-reports/crash.txt", options)).IsFalse();
        await Assert.That(ModpackExportFilter.ShouldInclude("screenshots/a.png", options)).IsFalse();
    }

    [Test]
    public async Task 关闭Mods时不打包mods目录() {
        var options = new ModpackExportOptions { IncludeMods = false };
        await Assert.That(ModpackExportFilter.ShouldInclude("mods/a.jar", options)).IsFalse();
        await Assert.That(ModpackExportFilter.ShouldInclude("config/x.toml", options)).IsTrue();
    }

    [Test]
    public async Task 显式开启日志时包含日志与崩溃报告() {
        var options = new ModpackExportOptions { IncludeLogs = true };
        await Assert.That(ModpackExportFilter.ShouldInclude("logs/latest.log", options)).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("crash-reports/crash.txt", options)).IsTrue();
    }

    [Test]
    public async Task 两种分隔符都能识别() {
        var options = new ModpackExportOptions { IncludeMods = false };
        await Assert.That(ModpackExportFilter.ShouldInclude(@"mods\a.jar", options)).IsFalse();
        await Assert.That(ModpackExportFilter.ShouldInclude("/mods/a.jar", options)).IsFalse();
    }

    [Test]
    public async Task 关闭存档时不打包saves() {
        var options = new ModpackExportOptions { IncludeSaves = false };
        await Assert.That(ModpackExportFilter.ShouldInclude("saves/world/level.dat", options)).IsFalse();
        await Assert.That(ModpackExportFilter.ShouldInclude("backups/x.zip", options)).IsFalse();
    }

    [Test]
    public async Task 空路径默认包含() {
        await Assert.That(ModpackExportFilter.ShouldInclude(null, Default())).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("", Default())).IsTrue();
    }

    [Test]
    public async Task 未知目录默认包含() {
        await Assert.That(ModpackExportFilter.ShouldInclude("resourcepacks/a.zip", Default())).IsTrue();
        await Assert.That(ModpackExportFilter.ShouldInclude("options.txt", Default())).IsTrue();
    }

}
