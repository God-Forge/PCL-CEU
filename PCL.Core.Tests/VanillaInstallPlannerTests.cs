using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class VanillaInstallPlannerTests {

    [Test]
    public async Task 生成客户端库资源与资源索引任务() {
        var version = JObject.Parse("""
        {
          "id":"1.20.1",
          "downloads":{"client":{"url":"https://example.com/client.jar","size":100}},
          "libraries":[{"name":"com.example:lib:1.0","downloads":{"artifact":{"path":"com/example/lib/1.0/lib-1.0.jar","url":"https://example.com/lib.jar","size":10}}}],
          "assetIndex":{"id":"5","url":"https://example.com/index.json","size":20}
        }
        """);
        var index = "{\"objects\":{\"minecraft/sounds/a.ogg\":{\"hash\":\"abcdef0123456789\",\"size\":30}}}";
        var tasks = VanillaInstallPlanner.Plan(version, @"C:\mc", index);
        await Assert.That(tasks.Count).IsEqualTo(4);
        await Assert.That(tasks.Any(task => task.TargetPath.EndsWith(@"versions\1.20.1\1.20.1.jar"))).IsTrue();
        await Assert.That(tasks.Any(task => task.TargetPath.EndsWith(@"libraries\com\example\lib\1.0\lib-1.0.jar"))).IsTrue();
        await Assert.That(tasks.Any(task => task.TargetPath.EndsWith(@"assets\indexes\5.json"))).IsTrue();
        await Assert.That(tasks.Any(task => task.TargetPath.EndsWith(@"assets\objects\ab\abcdef0123456789"))).IsTrue();
    }

    [Test]
    public async Task 资源对象包含镜像源() {
        var version = JObject.Parse("{\"id\":\"1.20.1\",\"assetIndex\":{\"id\":\"5\"}}");
        var index = "{\"objects\":{\"a\":{\"hash\":\"abcdef\",\"size\":1}}}";
        var tasks = VanillaInstallPlanner.Plan(version, @"C:\mc", index);
        var asset = tasks.Single(task => task.Name == "a");
        await Assert.That(asset.Sources.Count).IsEqualTo(2);
        await Assert.That(asset.Sources[0].Contains("resources.download.minecraft.net")).IsTrue();
        await Assert.That(asset.Sources[1].Contains("bmclapi2.bangbang93.com")).IsTrue();
    }

}
