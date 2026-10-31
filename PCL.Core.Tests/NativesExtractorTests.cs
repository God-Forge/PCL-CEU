using System.IO.Compression;
using System.Text;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class NativesExtractorTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-natives-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateJar(string path, params (string Name, string Content)[] entries) {
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var (name, content) in entries) {
            var entry = archive.CreateEntry(name);
            using var entryStream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            entryStream.Write(bytes, 0, bytes.Length);
        }
        return path;
    }

    [Test]
    public async Task 解压Dll并清理多余文件() {
        var root = CreateRoot();
        try {
            var target = Path.Combine(root, "natives");
            var jar = CreateJar(Path.Combine(root, "native.jar"),
                ("a.dll", "a"),
                ("b.txt", "b"),
                ("sub/c.dll", "c"));
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "stale.dll"), "old");

            var result = NativesExtractor.Extract([jar], target);

            await Assert.That(File.ReadAllText(Path.Combine(target, "a.dll"))).IsEqualTo("a");
            await Assert.That(File.ReadAllText(Path.Combine(target, "sub", "c.dll"))).IsEqualTo("c");
            await Assert.That(File.Exists(Path.Combine(target, "b.txt"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(target, "stale.dll"))).IsFalse();
            await Assert.That(result.Extracted.Count).IsEqualTo(2);
            await Assert.That(result.Deleted.Count).IsEqualTo(1);
            await Assert.That(result.Failed.Count).IsEqualTo(0);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 损坏的NativesJar会删除并抛出() {
        var root = CreateRoot();
        try {
            var target = Path.Combine(root, "natives");
            var jar = Path.Combine(root, "broken.jar");
            File.WriteAllText(jar, "not a zip");

            await Assert.That(() => NativesExtractor.Extract([jar], target)).Throws<InvalidDataException>();
            await Assert.That(File.Exists(jar)).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
