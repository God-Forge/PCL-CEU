using System.IO.Compression;
using System.Text;
using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ModpackInstallServiceTests {

    [Test]
    public async Task 导入overrides并阻止路径穿越() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-modpack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "pack.zip");
        var instance = Path.Combine(root, "instance");
        try {
            using (var stream = File.Create(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create)) {
                var entry = archive.CreateEntry("overrides/config/a.txt");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write("value");
            }
            var count = ModpackInstallService.InstallOverrides(zipPath, instance);
            await Assert.That(count).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(instance, "config", "a.txt"))).IsEqualTo("value");
        } finally {
            Directory.Delete(root, true);
        }
    }

}
