using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ResourcePackServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-rp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 扫描并切换资源包() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "instance");
            var packs = Path.Combine(instance, "resourcepacks");
            var shaders = Path.Combine(instance, "shaderpacks");
            Directory.CreateDirectory(packs);
            Directory.CreateDirectory(shaders);
            var pack = Path.Combine(packs, "demo.zip");
            File.WriteAllText(pack, "zip");
            File.WriteAllText(Path.Combine(shaders, "shader.zip.disabled"), "zip");

            var result = ResourcePackService.Scan(instance);
            await Assert.That(result.Packs.Count).IsEqualTo(2);
            await Assert.That(result.Packs.Any(item => item.IsShader && !item.Enabled)).IsTrue();

            var disabled = ResourcePackService.Toggle(pack);
            await Assert.That(File.Exists(disabled)).IsTrue();
            await Assert.That(disabled.EndsWith(".disabled")).IsTrue();
            var enabled = ResourcePackService.Toggle(disabled);
            await Assert.That(File.Exists(enabled)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
