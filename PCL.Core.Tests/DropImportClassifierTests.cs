using System.IO.Compression;
using PCL.Core.Import;

namespace PCL.Core.Tests;

public class DropImportClassifierTests {

    private static string TempDir() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-drop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateZip(string folder, string entryName, string content = "{}") {
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
        return path;
    }

    [Test]
    public async Task 按扩展名识别常见文件() {
        var root = TempDir();
        try {
            var jar = Path.Combine(root, "a.jar");
            File.WriteAllBytes(jar, [0x50, 0x4B]);
            await Assert.That(DropImportClassifier.Classify(jar)).IsEqualTo(DropImportKind.ModFile);

            var litemod = Path.Combine(root, "b.litemod");
            File.WriteAllBytes(litemod, [0x50]);
            await Assert.That(DropImportClassifier.Classify(litemod)).IsEqualTo(DropImportKind.ModFile);

            var javaPackage = Path.Combine(root, "c.javapkg");
            File.WriteAllBytes(javaPackage, [0x50]);
            await Assert.That(DropImportClassifier.Classify(javaPackage)).IsEqualTo(DropImportKind.JavaPackage);

            var payload = Path.Combine(root, "d.payload");
            File.WriteAllBytes(payload, [0x50]);
            await Assert.That(DropImportClassifier.Classify(payload)).IsEqualTo(DropImportKind.Payload);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 识别整合包与资源包() {
        var root = TempDir();
        try {
            var modpack = CreateZip(root, "modrinth.index.json");
            await Assert.That(DropImportClassifier.Classify(modpack)).IsEqualTo(DropImportKind.Modpack);

            var curseForge = CreateZip(root, "manifest.json");
            await Assert.That(DropImportClassifier.Classify(curseForge)).IsEqualTo(DropImportKind.Modpack);

            var resourcePack = CreateZip(root, "pack.mcmeta");
            await Assert.That(DropImportClassifier.Classify(resourcePack)).IsEqualTo(DropImportKind.ResourcePack);

            var plainZip = CreateZip(root, "readme.txt", "hi");
            await Assert.That(DropImportClassifier.Classify(plainZip)).IsEqualTo(DropImportKind.Unknown);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 识别存档与实例目录() {
        var root = TempDir();
        try {
            var world = Path.Combine(root, "world");
            Directory.CreateDirectory(world);
            File.WriteAllBytes(Path.Combine(world, "level.dat"), [0x1F, 0x8B]);
            await Assert.That(DropImportClassifier.Classify(world)).IsEqualTo(DropImportKind.World);

            var mc = Path.Combine(root, "mc");
            Directory.CreateDirectory(Path.Combine(mc, "versions"));
            await Assert.That(DropImportClassifier.Classify(mc)).IsEqualTo(DropImportKind.InstanceFolder);

            var plain = Path.Combine(root, "plain");
            Directory.CreateDirectory(plain);
            await Assert.That(DropImportClassifier.Classify(plain)).IsEqualTo(DropImportKind.Unknown);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 不存在的路径与空值返回未知() {
        await Assert.That(DropImportClassifier.Classify(null)).IsEqualTo(DropImportKind.Unknown);
        await Assert.That(DropImportClassifier.Classify("")).IsEqualTo(DropImportKind.Unknown);
        await Assert.That(DropImportClassifier.Classify(Path.Combine(Path.GetTempPath(), "no-such-file.jar")))
            .IsEqualTo(DropImportKind.Unknown);
    }

    [Test]
    public async Task 损坏的压缩包不会抛异常() {
        var root = TempDir();
        try {
            var broken = Path.Combine(root, "broken.zip");
            File.WriteAllBytes(broken, [0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF]);
            await Assert.That(DropImportClassifier.Classify(broken)).IsEqualTo(DropImportKind.Unknown);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 类型描述可读() {
        await Assert.That(DropImportClassifier.Describe(DropImportKind.Modpack)).IsEqualTo("整合包");
        await Assert.That(DropImportClassifier.Describe(DropImportKind.World)).IsEqualTo("存档");
        await Assert.That(DropImportClassifier.Describe(DropImportKind.Unknown)).IsEqualTo("未知内容");
    }

}
