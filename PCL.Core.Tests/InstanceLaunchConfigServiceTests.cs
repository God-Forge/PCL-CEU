using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceLaunchConfigServiceTests {

    [Test]
    public async Task 导出并导入启动配置() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-launchconfig-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            InstanceLaunchConfigService.Export(path, new Dictionary<string, object?> {
                ["VersionRamCustom"] = 4096,
                ["VersionArgumentJvm"] = "-Dfoo=bar",
                ["VersionAdvanceGC"] = 4
            }, "test");
            var values = InstanceLaunchConfigService.Import(path);
            await Assert.That(values["VersionRamCustom"]!.ToObject<int>()).IsEqualTo(4096);
            await Assert.That(values["VersionArgumentJvm"]!.ToString()).IsEqualTo("-Dfoo=bar");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task 缺少settings时抛出异常() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-launchconfig-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            File.WriteAllText(path, "{}");
            await Assert.That(() => InstanceLaunchConfigService.Import(path)).Throws<InvalidDataException>();
        } finally {
            File.Delete(path);
        }
    }

}
