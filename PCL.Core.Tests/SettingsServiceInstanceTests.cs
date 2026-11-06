using PCL.Core.Settings;

namespace PCL.Core.Tests;

/// <summary>
/// 实例级设置最终统一由 <see cref="SettingsService"/> 的 SettingSource.Instance 承载，
/// 存储位置是 &lt;实例目录&gt;\PCL\Setup.ini。
/// </summary>
public class SettingsServiceInstanceTests {

    private static (SettingsService Settings, string Base, string InstanceA, string InstanceB) Create() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-svc-" + Guid.NewGuid().ToString("N"));
        var baseDir = Path.Combine(root, "base");
        Directory.CreateDirectory(baseDir);
        var service = new SettingsService(baseDir);
        foreach (var (key, defaultValue) in InstanceSettingKeys.Definitions) {
            service.Register(key, defaultValue, SettingSource.Instance);
        }
        return (service, root, Path.Combine(root, "inst-a"), Path.Combine(root, "inst-b"));
    }

    [Test]
    public async Task 实例设置写入实例目录下的Setup_Ini() {
        var (settings, root, instanceA, _) = Create();
        try {
            settings.Set(InstanceSettingKeys.JvmArguments, "-Xmx6G", instanceA);
            var path = Path.Combine(instanceA, "PCL", "Setup.ini");
            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(File.ReadAllText(path)).Contains("-Xmx6G");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 实例之间互不影响() {
        var (settings, root, instanceA, instanceB) = Create();
        try {
            settings.Set(InstanceSettingKeys.WindowTitle, "A 的标题", instanceA);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instanceA)).IsEqualTo("A 的标题");
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instanceB)).IsEqualTo("");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task HasSaved区分显式设置与默认值() {
        var (settings, root, instanceA, _) = Create();
        try {
            await Assert.That(settings.HasSaved(InstanceSettingKeys.RamType, instanceA)).IsFalse();
            settings.Set(InstanceSettingKeys.RamType, 0, instanceA);
            await Assert.That(settings.HasSaved(InstanceSettingKeys.RamType, instanceA)).IsTrue();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 缺省值来自注册表() {
        var (settings, root, instanceA, _) = Create();
        try {
            await Assert.That(settings.Get<int>(InstanceSettingKeys.RamType, instanceA)).IsEqualTo(2);
            await Assert.That(settings.Get<int>(InstanceSettingKeys.RamCustom, instanceA)).IsEqualTo(15);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.JavaSelect, instanceA))
                .IsEqualTo(InstanceSettingKeys.JavaSelectGlobal);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 重置后恢复默认值() {
        var (settings, root, instanceA, _) = Create();
        try {
            settings.Set(InstanceSettingKeys.RamCustom, 30, instanceA);
            await Assert.That(settings.Get<int>(InstanceSettingKeys.RamCustom, instanceA)).IsEqualTo(30);
            settings.Reset(InstanceSettingKeys.RamCustom, instanceA);
            await Assert.That(settings.Get<int>(InstanceSettingKeys.RamCustom, instanceA)).IsEqualTo(15);
            await Assert.That(settings.HasSaved(InstanceSettingKeys.RamCustom, instanceA)).IsFalse();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 实例级键缺少实例路径时报错() {
        var (settings, root, _, _) = Create();
        try {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                Task.FromResult(settings.Get<int>(InstanceSettingKeys.RamType)));
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 键注册表覆盖VB的全部实例级设置() {
        await Assert.That(InstanceSettingKeys.All.Count).IsEqualTo(13);
        await Assert.That(InstanceSettingKeys.Definitions.Count).IsEqualTo(13);
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionAdvanceJvm")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionArgumentJavaSelect")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionArgumentIndieV2")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("VersionServerLogin")).IsTrue();
        await Assert.That(InstanceSettingKeys.IsInstanceLevel("UiLanguage")).IsFalse();
    }

    [Test]
    public async Task 页面用到的实例键都已注册() {
        // PageInstanceSetup 会直接读写的实例键，缺一个就会在页面里抛 KeyNotFoundException
        var required = new[] {
            "VersionArgumentJavaV2",
            InstanceSettingKeys.JavaSelect,
            "VersionRamType",
            "VersionArgumentIndieV2",
            "VersionServerLogin",
            // 启动链路会读取的两项
            InstanceSettingKeys.JvmArguments,
            InstanceSettingKeys.GameArguments
        };
        foreach (var key in required) {
            await Assert.That(InstanceSettingKeys.IsInstanceLevel(key)).IsTrue();
        }
    }

}
