using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LauncherProfilesServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 创建并更新launcher_profiles() {
        var root = CreateRoot();
        try {
            var result = LauncherProfilesService.Update(root, "Steve", "token");
            await Assert.That(result.Success).IsTrue();

            var path = Path.Combine(root, "launcher_profiles.json");
            var json = JObject.Parse(File.ReadAllText(path, Encoding.GetEncoding("GB18030")));
            await Assert.That(json["clientToken"]!.ToString()).IsEqualTo("token");
            await Assert.That(json["authenticationDatabase"]!["00000111112222233333444445555566"]!["username"]!.ToString()).IsEqualTo("Steve");
            await Assert.That(json["profiles"]!["PCL"]).IsNotNull();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 损坏文件时重建并更新() {
        var root = CreateRoot();
        try {
            File.WriteAllText(Path.Combine(root, "launcher_profiles.json"), "{ broken");
            var result = LauncherProfilesService.Update(root, "Alex", "token2");
            await Assert.That(result.Success).IsTrue();
            var json = JObject.Parse(File.ReadAllText(Path.Combine(root, "launcher_profiles.json"), Encoding.GetEncoding("GB18030")));
            await Assert.That(json["clientToken"]!.ToString()).IsEqualTo("token2");
        } finally {
            Directory.Delete(root, true);
        }
    }

}
