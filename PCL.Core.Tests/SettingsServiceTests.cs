using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class SettingsServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 普通设置默认值保存与重置() {
        var root = CreateRoot();
        try {
            var settings = new SettingsService(root, "TEST");
            settings.Register("IntValue", 5);
            settings.Register("StringValue", "default");
            await Assert.That(settings.Get<int>("IntValue")).IsEqualTo(5);

            settings.Set("IntValue", 8);
            await Assert.That(settings.Get<int>("IntValue")).IsEqualTo(8);
            await Assert.That(File.Exists(Path.Combine(root, "PCL", "Setup.ini"))).IsTrue();

            var reloaded = new SettingsService(root, "TEST");
            reloaded.Register("IntValue", 5);
            await Assert.That(reloaded.Get<int>("IntValue")).IsEqualTo(8);

            reloaded.Reset("IntValue");
            await Assert.That(reloaded.Get<int>("IntValue")).IsEqualTo(5);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 实例设置按实例隔离() {
        var root = CreateRoot();
        try {
            var settings = new SettingsService(root, "TEST");
            settings.Register("VersionRamCustom", 4096, SettingSource.Instance);
            var instanceA = Path.Combine(root, "versions", "a");
            var instanceB = Path.Combine(root, "versions", "b");
            settings.Set("VersionRamCustom", 2048, instanceA);
            await Assert.That(settings.Get<int>("VersionRamCustom", instanceA)).IsEqualTo(2048);
            await Assert.That(settings.Get<int>("VersionRamCustom", instanceB)).IsEqualTo(4096);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 加密设置不写入明文() {
        var root = CreateRoot();
        try {
            var settings = new SettingsService(root, "TEST");
            settings.Register("Token", "", encrypted: true);
            settings.Set("Token", "secret-value");
            await Assert.That(settings.Get<string>("Token")).IsEqualTo("secret-value");
            var ini = File.ReadAllText(Path.Combine(root, "PCL", "Setup.ini"));
            await Assert.That(ini.Contains("secret-value")).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 变更事件只在值变化时触发() {
        var root = CreateRoot();
        try {
            var count = 0;
            var settings = new SettingsService(root, "TEST");
            settings.Register("Flag", false, onChanged: _ => count++);
            settings.Set("Flag", true);
            settings.Set("Flag", true);
            await Assert.That(count).IsEqualTo(1);
        } finally {
            Directory.Delete(root, true);
        }
    }

}
