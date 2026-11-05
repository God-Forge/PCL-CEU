using PCL.Core.Settings;

namespace PCL.Core.Tests;

/// <summary>
/// 防止「实例键被同名全局键覆盖」类问题再次发生：
/// 全局键若与实例键重名，后注册的定义会覆盖前者，导致不带实例路径的读取抛异常。
/// </summary>
public class SettingKeyCollisionTests {

    [Test]
    public async Task 全局键与实例键不重名() {
        var globalKeys = new[] {
            "LaunchArgumentTitle",
            "LaunchAdvanceJvm",
            "LaunchRamType",
            "LaunchRamCustom",
            "LaunchArgumentWindowType",
            "LaunchArgumentWindowWidth",
            "LaunchArgumentWindowHeight",
            "LaunchArgumentVisible",
            "UiLanguage",
            "UiMusicVolume"
        };
        foreach (var key in globalKeys) {
            await Assert.That(InstanceSettingKeys.IsInstanceLevel(key)).IsFalse();
        }
    }

    [Test]
    public async Task 启动自检能发现被错误注册的实例键() {
        var service = new SettingsService(Path.Combine(Path.GetTempPath(), "pcl-selfcheck-" + Guid.NewGuid().ToString("N")));
        service.Register("VersionArgumentTitle", "", SettingSource.Normal);
        var problems = service.FindKeysNotInstanceScoped([InstanceSettingKeys.WindowTitle]);
        await Assert.That(problems).Contains(InstanceSettingKeys.WindowTitle);
    }

    [Test]
    public async Task 正确注册时自检通过() {
        var service = new SettingsService(Path.Combine(Path.GetTempPath(), "pcl-selfcheck-" + Guid.NewGuid().ToString("N")));
        service.Register(InstanceSettingKeys.WindowTitle, "", SettingSource.Instance);
        await Assert.That(service.FindKeysNotInstanceScoped([InstanceSettingKeys.WindowTitle]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 未注册的键也算问题() {
        var service = new SettingsService(Path.Combine(Path.GetTempPath(), "pcl-selfcheck-" + Guid.NewGuid().ToString("N")));
        var problems = service.FindKeysNotInstanceScoped(["NeverRegistered"]);
        await Assert.That(problems).Contains("NeverRegistered");
    }

    [Test]
    public async Task 同名注册后读实例键会报错() {
        var service = new SettingsService(Path.Combine(Path.GetTempPath(), "pcl-collide-" + Guid.NewGuid().ToString("N")));
        service.Register(InstanceSettingKeys.WindowTitle, "", SettingSource.Normal);
        service.Register(InstanceSettingKeys.WindowTitle, "", SettingSource.Instance);
        // 覆盖后是实例级：不带路径读取应抛出，而不是静默返回空
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(service.Get<string>(InstanceSettingKeys.WindowTitle)));
    }

}
