using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ModpackInstallPlannerTests {

    private const string Versions = @"C:\mc\versions";

    [Test]
    public async Task 未指定实例名时使用zip文件名() {
        var target = ModpackInstallPlanner.ResolveTargetFolder(Versions, @"C:\downloads\整合包A.zip");
        await Assert.That(target).IsEqualTo(Path.Combine(Versions, "整合包A"));
    }

    [Test]
    public async Task 指定实例名时优先使用() {
        var target = ModpackInstallPlanner.ResolveTargetFolder(Versions, @"C:\downloads\a.zip", "我的整合包");
        await Assert.That(target).IsEqualTo(Path.Combine(Versions, "我的整合包"));
    }

    [Test]
    public async Task 实例名中的非法字符被替换() {
        var target = ModpackInstallPlanner.ResolveTargetFolder(Versions, @"C:\a.zip", "a/b:c");
        await Assert.That(Path.GetFileName(target)).IsEqualTo("a_b_c");
        await Assert.That(target.StartsWith(Versions, StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task 空的或点号实例名使用默认名() {
        await Assert.That(ModpackInstallPlanner.SanitizeInstanceName("")).IsEqualTo("整合包");
        await Assert.That(ModpackInstallPlanner.SanitizeInstanceName("..")).IsEqualTo("整合包");
        await Assert.That(ModpackInstallPlanner.SanitizeInstanceName("   ")).IsEqualTo("整合包");
    }

    [Test]
    public async Task 整合包文件不存在时校验失败() {
        var error = ModpackInstallPlanner.Validate(Versions, Path.Combine(Path.GetTempPath(), "no-such.zip"));
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("不存在");
    }

    [Test]
    public async Task 目标实例已存在时校验失败() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-plan-" + Guid.NewGuid().ToString("N"));
        var versions = Path.Combine(root, "versions");
        Directory.CreateDirectory(Path.Combine(versions, "占位包"));
        var zip = Path.Combine(root, "占位包.zip");
        File.WriteAllBytes(zip, [0x50, 0x4B]);
        try {
            var error = ModpackInstallPlanner.Validate(versions, zip);
            await Assert.That(error).IsNotNull();
            await Assert.That(error!).Contains("已存在");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 正常情况校验通过() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-plan-" + Guid.NewGuid().ToString("N"));
        var versions = Path.Combine(root, "versions");
        Directory.CreateDirectory(versions);
        var zip = Path.Combine(root, "新包.zip");
        File.WriteAllBytes(zip, [0x50, 0x4B]);
        try {
            await Assert.That(ModpackInstallPlanner.Validate(versions, zip)).IsNull();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 空路径抛出参数异常() {
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(ModpackInstallPlanner.ResolveTargetFolder("", @"C:\a.zip")));
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(ModpackInstallPlanner.ResolveTargetFolder(Versions, "")));
    }

}
