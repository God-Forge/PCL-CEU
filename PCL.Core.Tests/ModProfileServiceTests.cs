using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class ModProfileServiceTests {

    private static string CreateInstance(out string root) {
        root = Path.Combine(Path.GetTempPath(), "pcl-profile-" + Guid.NewGuid().ToString("N"));
        var instance = Path.Combine(root, "versions", "test");
        Directory.CreateDirectory(Path.Combine(instance, "mods"));
        File.WriteAllText(Path.Combine(instance, "test.json"), """{"id":"test","libraries":[]}""");
        return instance;
    }

    [Test]
    public async Task 捕获配置档记录启用状态() {
        var instance = CreateInstance(out var root);
        try {
            File.WriteAllText(Path.Combine(instance, "mods", "sodium.jar"), "a");
            File.WriteAllText(Path.Combine(instance, "mods", "shaders.jar.disabled"), "b");

            var profile = ModProfileService.Capture(instance, "性能档", "关掉光影相关 Mod");

            await Assert.That(profile.Name).IsEqualTo("性能档");
            await Assert.That(profile.Entries.Count).IsEqualTo(2);
            var sodium = profile.Entries.Single(entry => entry.FileName == "sodium.jar");
            await Assert.That(sodium.Enabled).IsTrue();
            var shaders = profile.Entries.Single(entry => entry.FileName == "shaders.jar");
            await Assert.That(shaders.Enabled).IsFalse();
            await Assert.That(File.Exists(ModProfileService.GetStorePath(instance))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 应用配置档切换启用状态() {
        var instance = CreateInstance(out var root);
        try {
            var mods = Path.Combine(instance, "mods");
            File.WriteAllText(Path.Combine(mods, "a.jar"), "a");
            File.WriteAllText(Path.Combine(mods, "b.jar.disabled"), "b");
            var profile = ModProfileService.Capture(instance, "标准档");

            // 人为把状态反过来，模拟玩家手动改过
            File.Move(Path.Combine(mods, "a.jar"), Path.Combine(mods, "a.jar.disabled"));
            File.Move(Path.Combine(mods, "b.jar.disabled"), Path.Combine(mods, "b.jar"));

            var result = ModProfileService.Apply(instance, profile);

            await Assert.That(result.EnabledCount).IsEqualTo(1);
            await Assert.That(result.DisabledCount).IsEqualTo(1);
            await Assert.That(File.Exists(Path.Combine(mods, "a.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mods, "a.jar.disabled"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(mods, "b.jar.disabled"))).IsTrue();
            await Assert.That(result.Failed.Count).IsEqualTo(0);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 应用配置档时禁用未记录的Mod() {
        var instance = CreateInstance(out var root);
        try {
            var mods = Path.Combine(instance, "mods");
            File.WriteAllText(Path.Combine(mods, "known.jar"), "a");
            var profile = ModProfileService.Capture(instance, "纯净档");
            File.WriteAllText(Path.Combine(mods, "extra.jar"), "c");

            var result = ModProfileService.Apply(instance, profile, disableUnlisted: true);

            await Assert.That(result.Unlisted.Contains("extra.jar")).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mods, "extra.jar.disabled"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mods, "known.jar"))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 可选择保留未记录的Mod() {
        var instance = CreateInstance(out var root);
        try {
            var mods = Path.Combine(instance, "mods");
            File.WriteAllText(Path.Combine(mods, "known.jar"), "a");
            var profile = ModProfileService.Capture(instance, "纯净档");
            File.WriteAllText(Path.Combine(mods, "extra.jar"), "c");

            var result = ModProfileService.Apply(instance, profile, disableUnlisted: false);

            await Assert.That(result.Unlisted.Contains("extra.jar")).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mods, "extra.jar"))).IsTrue();
            await Assert.That(result.EnabledCount).IsEqualTo(1);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 差异对比列出启用禁用与缺失() {
        var instance = CreateInstance(out var root);
        try {
            var mods = Path.Combine(instance, "mods");
            File.WriteAllText(Path.Combine(mods, "a.jar"), "a");
            File.WriteAllText(Path.Combine(mods, "b.jar"), "b");
            var profile = ModProfileService.Capture(instance, "档");
            profile.Entries.Add(new ModProfileEntry { FileName = "missing.jar", Enabled = true });

            File.Move(Path.Combine(mods, "a.jar"), Path.Combine(mods, "a.jar.disabled"));
            File.WriteAllText(Path.Combine(mods, "c.jar"), "c");

            var diff = ModProfileService.Diff(instance, profile);

            await Assert.That(diff.WillEnable.Contains("a.jar")).IsTrue();
            await Assert.That(diff.Missing.Contains("missing.jar")).IsTrue();
            await Assert.That(diff.Unlisted.Contains("c.jar")).IsTrue();
            await Assert.That(diff.IsEmpty).IsFalse();
            await Assert.That(diff.HasChanges).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 配置档可删除与重命名() {
        var instance = CreateInstance(out var root);
        try {
            File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "a");
            var first = ModProfileService.Capture(instance, "档一");
            var second = ModProfileService.Capture(instance, "档二");

            await Assert.That(ModProfileService.Rename(instance, first.Id, "性能档")).IsTrue();
            var library = ModProfileService.Load(instance);
            await Assert.That(library.Profiles.Any(profile => profile.Name == "性能档")).IsTrue();

            await Assert.That(ModProfileService.Remove(instance, second.Id)).IsTrue();
            library = ModProfileService.Load(instance);
            await Assert.That(library.Profiles.Count).IsEqualTo(1);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 同名捕获会更新而不是重复添加() {
        var instance = CreateInstance(out var root);
        try {
            var mods = Path.Combine(instance, "mods");
            File.WriteAllText(Path.Combine(mods, "a.jar"), "a");
            ModProfileService.Capture(instance, "档");
            File.WriteAllText(Path.Combine(mods, "b.jar"), "b");
            var updated = ModProfileService.Capture(instance, "档");

            var library = ModProfileService.Load(instance);
            await Assert.That(library.Profiles.Count).IsEqualTo(1);
            await Assert.That(updated.Entries.Count).IsEqualTo(2);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 损坏的配置文件不会抛异常() {
        var instance = CreateInstance(out var root);
        try {
            var path = ModProfileService.GetStorePath(instance);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ not json");

            var library = ModProfileService.Load(instance);

            await Assert.That(library.Profiles.Count).IsEqualTo(0);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 导出后导入会自动编号避免重名() {
        var instance = CreateInstance(out var root);
        try {
            File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "a");
            var profile = ModProfileService.Capture(instance, "分享档");
            var exportPath = Path.Combine(root, "profile.json");
            ModProfileService.Export(exportPath, profile);

            var imported = ModProfileService.Import(instance, exportPath);

            await Assert.That(imported.Name).IsEqualTo("分享档 (2)");
            await Assert.That(ModProfileService.Load(instance).Profiles.Count).IsEqualTo(2);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 空名称捕获会被拒绝() {
        var instance = CreateInstance(out var root);
        try {
            await Assert.That(() => ModProfileService.Capture(instance, "  "))
                .Throws<ArgumentException>();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
