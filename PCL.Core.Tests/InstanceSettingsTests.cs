using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class InstanceSettingsTests {

    private static string TempInstance() =>
        Path.Combine(Path.GetTempPath(), "pcl-inst-" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task 设置文件位于实例的PCL目录下() {
        var folder = TempInstance();
        var settings = new InstanceSettings(folder);
        await Assert.That(settings.SettingsPath)
            .IsEqualTo(Path.Combine(folder, "PCL", "Setup.ini"));
    }

    [Test]
    public async Task 读写往返() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            settings.Set("VersionAdvanceJvm", "-XX:+UseG1GC");
            await Assert.That(settings.GetRaw("VersionAdvanceJvm")).IsEqualTo("-XX:+UseG1GC");
            await Assert.That(settings.Has("VersionAdvanceJvm")).IsTrue();
            await Assert.That(File.Exists(settings.SettingsPath)).IsTrue();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 未设置的键返回默认值() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            await Assert.That(settings.GetRaw("Missing")).IsNull();
            await Assert.That(settings.GetString("Missing", "fallback")).IsEqualTo("fallback");
            await Assert.That(settings.GetInt("Missing", 42)).IsEqualTo(42);
            await Assert.That(settings.GetBool("Missing", true)).IsTrue();
            await Assert.That(settings.Has("Missing")).IsFalse();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 类型化读写() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            settings.SetInt("VersionRamCustom", 15);
            settings.SetBool("VersionAdvanceDisableJLW", true);
            await Assert.That(settings.GetInt("VersionRamCustom")).IsEqualTo(15);
            await Assert.That(settings.GetBool("VersionAdvanceDisableJLW")).IsTrue();
            // 写入格式与 VB 保持一致
            await Assert.That(settings.GetRaw("VersionAdvanceDisableJLW")).IsEqualTo("True");
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 删除后恢复未设置() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            settings.Set("VersionArgumentTitle", "MC");
            settings.Remove("VersionArgumentTitle");
            await Assert.That(settings.Has("VersionArgumentTitle")).IsFalse();
            await Assert.That(settings.GetRaw("VersionArgumentTitle")).IsNull();
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 实例值优先于全局回退() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            await Assert.That(settings.GetWithFallback("VersionAdvanceJvm", () => "-Xmx2G")).IsEqualTo("-Xmx2G");
            settings.Set("VersionAdvanceJvm", "-Xmx6G");
            await Assert.That(settings.GetWithFallback("VersionAdvanceJvm", () => "-Xmx2G")).IsEqualTo("-Xmx6G");
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task int回退在值非法时生效() {
        var folder = TempInstance();
        try {
            var settings = new InstanceSettings(folder);
            settings.Set("VersionRamCustom", "abc");
            await Assert.That(settings.GetInt("VersionRamCustom", 15)).IsEqualTo(15);
            await Assert.That(settings.GetWithFallback("VersionRamCustom", () => 15)).IsEqualTo(15);
        } finally {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 空目录抛出参数异常() {
        await Assert.ThrowsAsync<ArgumentException>(() => Task.FromResult(new InstanceSettings(" ")));
    }

    [Test]
    public async Task provider按实例缓存并支持失效() {
        var folder = TempInstance();
        try {
            InstanceSettingsProvider.Clear();
            var first = InstanceSettingsProvider.For(folder);
            var second = InstanceSettingsProvider.For(folder + Path.DirectorySeparatorChar);
            await Assert.That(ReferenceEquals(first, second)).IsTrue();
            InstanceSettingsProvider.Invalidate(folder);
            var third = InstanceSettingsProvider.For(folder);
            await Assert.That(ReferenceEquals(first, third)).IsFalse();
        } finally {
            InstanceSettingsProvider.Clear();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 实例级键注册表() {
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionAdvanceJvm")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionArgumentTitle")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("UiLanguage")).IsFalse();
        await Assert.That(InstanceSettingKeys.All.Count).IsEqualTo(9);
    }

}
