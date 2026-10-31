using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class MinecraftVersionScannerTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-versions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 扫描版本目录与JSON() {
        var root = CreateRoot();
        try {
            var versions = Path.Combine(root, "versions");
            Directory.CreateDirectory(Path.Combine(versions, "1.20.1"));
            File.WriteAllText(Path.Combine(versions, "1.20.1", "1.20.1.json"),
                "{\"id\":\"1.20.1\",\"type\":\"release\",\"releaseTime\":\"2023-06-12T00:00:00+00:00\",\"mainClass\":\"net.minecraft.client.main.Main\",\"assetIndex\":{\"id\":\"5\"},\"libraries\":[{},{}]}");

            var result = MinecraftVersionScanner.Scan(root);
            await Assert.That(result.Count).IsEqualTo(1);
            await Assert.That(result[0].Id).IsEqualTo("1.20.1");
            await Assert.That(result[0].MainClass).IsEqualTo("net.minecraft.client.main.Main");
            await Assert.That(result[0].AssetIndex).IsEqualTo("5");
            await Assert.That(result[0].LibrariesCount).IsEqualTo(2);
            await Assert.That(result[0].Error).IsNull();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 损坏JSON返回错误() {
        var root = CreateRoot();
        try {
            var version = Path.Combine(root, "versions", "bad");
            Directory.CreateDirectory(version);
            File.WriteAllText(Path.Combine(version, "bad.json"), "not json");
            var result = MinecraftVersionScanner.ScanVersion(version);
            await Assert.That(result.Error).IsNotNull();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
