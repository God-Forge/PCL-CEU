using System.IO.Compression;
using System.Text;
using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class ModScannerTests {

    private static string CreateModsFolder() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-modscan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateJar(string path, params (string Name, string Content)[] entries) {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries) {
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    [Test]
    public async Task 识别Fabric元数据与缺失依赖() {
        var folder = CreateModsFolder();
        try {
            CreateJar(Path.Combine(folder, "example.jar"),
                ("fabric.mod.json", "{\"id\":\"examplemod\",\"name\":\"Example\",\"version\":\"1.0.0\",\"depends\":{\"minecraft\":\"*\",\"fabricloader\":\">=0.15\",\"missinglib\":\">=1.0\"}}"));

            var result = ModScanner.Scan(folder);
            await Assert.That(result.Mods.Count).IsEqualTo(1);
            await Assert.That(result.Mods[0].ModId).IsEqualTo("examplemod");
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("missinglib"))).IsTrue();
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 识别Forge元数据与重复Mod() {
        var folder = CreateModsFolder();
        try {
            var text = "modLoader=\"javafml\"\n[[mods]]\nmodId=\"forgeexample\"\nversion=\"1.0\"\ndisplayName=\"Forge Example\"\n[[dependencies.forgeexample]]\nmodId=\"forge\"\nmandatory=true\nversionRange=\"[47,)\"\n[[dependencies.forgeexample]]\nmodId=\"missingforge\"\nmandatory=true\n";
            CreateJar(Path.Combine(folder, "a.jar"), ("META-INF/mods.toml", text));
            CreateJar(Path.Combine(folder, "b.jar"), ("META-INF/mods.toml", text));

            var result = ModScanner.Scan(folder);
            await Assert.That(result.Mods.Count).IsEqualTo(2);
            await Assert.That(result.Mods[0].Loader).IsEqualTo("forge");
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("重复"))).IsTrue();
            await Assert.That(result.Issues.Any(issue => issue.Message.Contains("missingforge"))).IsTrue();
        } finally {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public async Task 损坏Jar不会中断扫描() {
        var folder = CreateModsFolder();
        try {
            File.WriteAllText(Path.Combine(folder, "broken.jar"), "not a zip");
            CreateJar(Path.Combine(folder, "good.jar"),
                ("fabric.mod.json", "{\"id\":\"good\",\"version\":\"1.0\"}"));

            var result = ModScanner.Scan(folder);
            await Assert.That(result.Mods.Count).IsEqualTo(1);
            await Assert.That(result.Issues.Any(issue => issue.FileName == "broken.jar")).IsTrue();
        } finally {
            Directory.Delete(folder, true);
        }
    }

}
