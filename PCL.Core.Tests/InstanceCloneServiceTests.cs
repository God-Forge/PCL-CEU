using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceCloneServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-clone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 克隆实例并修正JSON标识() {
        var root = CreateRoot();
        try {
            var source = Path.Combine(root, "versions", "source");
            Directory.CreateDirectory(Path.Combine(source, "mods"));
            Directory.CreateDirectory(Path.Combine(source, "saves"));
            File.WriteAllText(Path.Combine(source, "source.json"), """{"id":"source","mainClass":"a"}""");
            File.WriteAllText(Path.Combine(source, "mods", "a.jar"), "mod");
            File.WriteAllText(Path.Combine(source, "saves", "world.dat"), "save");

            var target = Path.Combine(root, "versions", "copy");
            InstanceCloneService.Clone(source, target, new InstanceCloneOptions { IncludeSaves = false });

            await Assert.That(File.Exists(Path.Combine(target, "copy.json"))).IsTrue();
            await Assert.That(File.ReadAllText(Path.Combine(target, "copy.json"))).Contains("\"id\": \"copy\"");
            await Assert.That(File.Exists(Path.Combine(target, "mods", "a.jar"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(target, "saves"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 目标存在且不覆盖时抛出异常() {
        var root = CreateRoot();
        try {
            var source = Path.Combine(root, "versions", "source");
            var target = Path.Combine(root, "versions", "copy");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(source, "source.json"), """{"id":"source"}""");
            await Assert.That(() => InstanceCloneService.Clone(source, target, new InstanceCloneOptions()))
                .Throws<IOException>();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
