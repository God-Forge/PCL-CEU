using System.IO.Compression;
using System.Text;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceCheckTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-instcheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateJar(string path, string id) {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("fabric.mod.json");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write($"{{\"id\":\"{id}\",\"version\":\"1.0\"}}");
    }

    [Test]
    public async Task 检查缺失文件与重复Mod() {
        var root = CreateRoot();
        var mc = Path.Combine(root, "mc");
        var instance = Path.Combine(mc, "versions", "test");
        Directory.CreateDirectory(instance);
        Directory.CreateDirectory(Path.Combine(instance, "mods"));

        try {
            File.WriteAllText(Path.Combine(instance, "test.json"),
                "{\"id\":\"test\",\"mainClass\":\"net.minecraft.client.main.Main\",\"libraries\":[{\"name\":\"com.example:lib:1.0\"}],\"assetIndex\":{\"id\":\"1.20\"}}");

            CreateJar(Path.Combine(instance, "mods", "a.jar"), "same");
            CreateJar(Path.Combine(instance, "mods", "b.jar"), "same");

            var result = InstanceCheckService.Check(instance, mc);
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("客户端 JAR"))).IsTrue();
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("libraries"))).IsTrue();
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("资源索引"))).IsTrue();
            await Assert.That(result.Mods.Count).IsEqualTo(2);
            await Assert.That(result.ModIssues.Any(issue => issue.Message.Contains("重复"))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 缺少JSON直接返回错误() {
        var root = CreateRoot();
        var mc = Path.Combine(root, "mc");
        var instance = Path.Combine(mc, "versions", "missing");
        Directory.CreateDirectory(instance);
        try {
            var result = InstanceCheckService.Check(instance, mc);
            await Assert.That(result.HasErrors).IsTrue();
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("版本 JSON"))).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
