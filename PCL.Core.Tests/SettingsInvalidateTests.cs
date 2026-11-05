using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class SettingsInvalidateTests {

    private static (SettingsService Settings, string Root, string Instance) Create() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-inv-" + Guid.NewGuid().ToString("N"));
        var baseDir = Path.Combine(root, "base");
        Directory.CreateDirectory(baseDir);
        var service = new SettingsService(baseDir);
        foreach (var (key, defaultValue) in InstanceSettingKeys.Definitions) {
            service.Register(key, defaultValue, SettingSource.Instance);
        }
        return (service, root, Path.Combine(root, "inst"));
    }

    [Test]
    public async Task 失效缓存后会重新读取磁盘() {
        var (settings, root, instance) = Create();
        try {
            settings.Set(InstanceSettingKeys.WindowTitle, "旧标题", instance);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instance)).IsEqualTo("旧标题");

            // 直接改文件模拟外部修改（例如 VB 版写入或手工编辑）
            var iniPath = Path.Combine(instance, "PCL", "Setup.ini");
            File.WriteAllText(iniPath, "VersionArgumentTitle:新标题\r\n", new System.Text.UTF8Encoding(false));

            // 未失效时仍读缓存
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instance)).IsEqualTo("旧标题");

            settings.InvalidateInstanceCache(instance);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instance)).IsEqualTo("新标题");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 失效不影响其他实例的缓存() {
        var (settings, root, instanceA) = Create();
        try {
            var instanceB = Path.Combine(root, "inst-b");
            settings.Set(InstanceSettingKeys.WindowTitle, "B", instanceB);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instanceB)).IsEqualTo("B");

            settings.InvalidateInstanceCache(instanceA);
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instanceB)).IsEqualTo("B");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 尾部分隔符不同的路径也能被失效() {
        var (settings, root, instance) = Create();
        try {
            settings.Set(InstanceSettingKeys.WindowTitle, "值", instance);
            settings.InvalidateInstanceCache(instance + Path.DirectorySeparatorChar);
            var iniPath = Path.Combine(instance, "PCL", "Setup.ini");
            File.WriteAllText(iniPath, "VersionArgumentTitle:改过\r\n", new System.Text.UTF8Encoding(false));
            await Assert.That(settings.Get<string>(InstanceSettingKeys.WindowTitle, instance)).IsEqualTo("改过");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 空路径不抛异常() {
        var (settings, root, _) = Create();
        try {
            settings.InvalidateInstanceCache(null);
            settings.InvalidateInstanceCache("");
            settings.InvalidateInstanceCache("   ");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
