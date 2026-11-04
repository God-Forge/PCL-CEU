using Newtonsoft.Json.Linq;
using PCL.Core.Login;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchPlanServiceTests {

    [Test]
    public async Task 构建最小启动计划() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-launchplan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "versions", "test"));
        try {
            var json = JObject.Parse("""
            {
              "id": "test",
              "mainClass": "net.minecraft.client.main.Main",
              "type": "release",
              "assetIndex": { "id": "legacy" },
              "arguments": {
                "jvm": ["-Djava.library.path=${natives_directory}", "-cp", "${classpath}"],
                "game": ["--username", "${auth_player_name}", "--clientId", "${clientid}", "--xuid", "${auth_xuid}"]
              },
              "libraries": []
            }
            """);
            var login = OfflineLoginService.Create("Steve");
            var plan = LaunchPlanService.Build(json, root, Path.Combine(root, "versions", "test"), "test", login);
            await Assert.That(plan.MainClass).IsEqualTo("net.minecraft.client.main.Main");
            await Assert.That(plan.Arguments.Contains("--username")).IsTrue();
            await Assert.That(plan.Arguments.Contains("Steve")).IsTrue();
            await Assert.That(plan.Arguments.Contains("-Xmx4096m")).IsTrue();
            await Assert.That(plan.Arguments.Count(argument => argument.StartsWith("-Djava.library.path="))).IsEqualTo(1);
            await Assert.That(plan.Arguments.Count(argument => argument == "-cp")).IsEqualTo(1);
            await Assert.That(plan.Arguments.Any(argument => argument.Contains("${", StringComparison.Ordinal))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
