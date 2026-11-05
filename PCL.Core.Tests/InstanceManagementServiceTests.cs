using System.Threading;
using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceManagementServiceTests {

    [Test]
    public async Task 读取并写入实例设置() {
        var root = CreateInstance("Old");
        try {
            var setup = InstanceManagementService.ReadSetup(root);
            await Assert.That(setup.CustomInfo).IsEqualTo("旧描述");
            await Assert.That(setup.IsStar).IsTrue();

            InstanceManagementService.SetCustomInfo(root, "新描述");
            InstanceManagementService.ToggleStar(root);
            var updated = InstanceManagementService.ReadSetup(root);
            await Assert.That(updated.CustomInfo).IsEqualTo("新描述");
            await Assert.That(updated.IsStar).IsFalse();
        } finally {
            Directory.Delete(Path.GetDirectoryName(root)!, true);
        }
    }

    [Test]
    public async Task 重命名实例并更新JSON与Jar() {
        var root = CreateInstance("Old");
        var versions = Path.GetDirectoryName(root)!;
        try {
            var renamed = RenameWithRetry(root, "New");

            await Assert.That(Path.GetFileName(renamed)).IsEqualTo("New");
            await Assert.That(Directory.Exists(renamed)).IsTrue();
            await Assert.That(Directory.Exists(root)).IsFalse();
            await Assert.That(File.Exists(Path.Combine(renamed, "New.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(renamed, "Old.json"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(renamed, "New.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(renamed, "Old.jar"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(renamed, "New-natives"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(renamed, "Old-natives"))).IsFalse();

            var json = JObject.Parse(File.ReadAllText(Path.Combine(renamed, "New.json")));
            await Assert.That(json["id"]?.ToString()).IsEqualTo("New");
            var setup = File.ReadAllText(Path.Combine(renamed, "PCL", "Setup.ini"));
            await Assert.That(setup.Contains(renamed, StringComparison.OrdinalIgnoreCase)).IsTrue();
        } finally {
            if (Directory.Exists(versions)) Directory.Delete(versions, true);
        }
    }

    [Test]
    public async Task 目标实例已存在时拒绝重命名() {
        var root = CreateInstance("Old");
        var versions = Path.GetDirectoryName(root)!;
        try {
            Directory.CreateDirectory(Path.Combine(versions, "New"));
            var threw = false;
            try {
                InstanceManagementService.Rename(root, "New");
            } catch (IOException) {
                threw = true;
            }
            await Assert.That(threw).IsTrue();
            await Assert.That(Directory.Exists(root)).IsTrue();
        } finally {
            if (Directory.Exists(versions)) Directory.Delete(versions, true);
        }
    }

    /// <summary>
    /// 重命名并对环境性的目录占用做重试。
    /// Windows 上刚写入的文件可能被索引或扫描短暂持有，跟重命名逻辑本身无关，
    /// 这里只对 <see cref="IOException"/> 重试；逻辑错误会一直失败而暴露。
    /// </summary>
    private static string RenameWithRetry(string source, string newName, int attempts = 4) {
        for (var attempt = 1; ; attempt++) {
            try {
                return InstanceManagementService.Rename(source, newName);
            } catch (IOException) when (attempt < attempts) {
                Thread.Sleep(120 * attempt);
            }
        }
    }

    private static string CreateInstance(string name) {
        var versions = Path.Combine(Path.GetTempPath(), "pcl-instancemgmt-" + Guid.NewGuid().ToString("N"), "versions");
        var folder = Path.Combine(versions, name);
        Directory.CreateDirectory(Path.Combine(folder, "PCL"));
        Directory.CreateDirectory(Path.Combine(folder, name + "-natives"));
        File.WriteAllText(Path.Combine(folder, name + ".json"), $"{{\"id\":\"{name}\",\"mainClass\":\"net.minecraft.client.main.Main\"}}");
        File.WriteAllText(Path.Combine(folder, name + ".jar"), "jar");
        File.WriteAllText(Path.Combine(folder, "PCL", "Setup.ini"),
            $"CustomInfo:旧描述\r\nIsStar:True\r\nDisplayType:0\r\nPath:{folder}\r\n");
        return folder;
    }

}
