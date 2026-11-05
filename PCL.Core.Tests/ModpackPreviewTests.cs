using System.IO.Compression;
using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ModpackPreviewTests {

    private const string ModrinthIndex = """
    {
      "formatVersion": 1,
      "game": "minecraft",
      "name": "Sodium Plus",
      "versionId": "1.2.0",
      "dependencies": { "minecraft": "26.3", "fabric-loader": "0.15.0" },
      "files": [
        { "path": "mods/sodium.jar", "hashes": { "sha1": "aa" }, "downloads": ["https://cdn/a.jar"], "fileSize": 10 },
        { "path": "mods/lithium.jar", "hashes": { "sha1": "bb" }, "downloads": ["https://cdn/b.jar"], "fileSize": 20 },
        { "path": "mods/optional.jar", "hashes": { "sha1": "cc" }, "downloads": ["https://cdn/c.jar"], "fileSize": 30, "env": { "client": "optional" } }
      ]
    }
    """;

    private const string CurseForgeManifest = """
    {
      "manifestType": "minecraftModpack",
      "name": "Forge Pack",
      "version": "2.0",
      "minecraft": {
        "version": "1.20.1",
        "modLoaders": [ { "id": "forge-47.2.0", "primary": true } ]
      },
      "files": [ { "projectID": 1, "fileID": 2, "required": true } ]
    }
    """;

    private static string CreateZip(string folder, string entryName, string content) {
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry(entryName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
        return path;
    }

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "pcl-preview-" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task 解析Modrinth清单的加载器() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var zip = CreateZip(root, "modrinth.index.json", ModrinthIndex);
            var preview = ModpackPreviewService.Preview(zip, Path.Combine(root, "versions"));
            await Assert.That(preview.Valid).IsTrue();
            await Assert.That(preview.Name).IsEqualTo("Sodium Plus");
            await Assert.That(preview.Target.MinecraftVersion).IsEqualTo("26.3");
            await Assert.That(preview.Target.FabricVersion).IsEqualTo("0.15.0");
            await Assert.That(preview.FileCount).IsEqualTo(3);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 解析CurseForge清单的主加载器() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var zip = CreateZip(root, "manifest.json", CurseForgeManifest);
            var preview = ModpackPreviewService.Preview(zip, Path.Combine(root, "versions"));
            await Assert.That(preview.Valid).IsTrue();
            await Assert.That(preview.Target.ForgeVersion).IsEqualTo("47.2.0");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 目标已存在时标记() {
        var root = TempRoot();
        try {
            var versions = Path.Combine(root, "versions");
            Directory.CreateDirectory(versions);
            var zip = CreateZip(root, "modrinth.index.json", ModrinthIndex);
            var target = ModpackInstallPlanner.ResolveTargetFolder(versions, zip);
            Directory.CreateDirectory(target);
            var preview = ModpackPreviewService.Preview(zip, versions);
            await Assert.That(preview.TargetExists).IsTrue();
            await Assert.That(ModpackPreviewService.Format(preview)).Contains("已存在");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 无法识别的压缩包返回错误() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var zip = CreateZip(root, "readme.txt", "hi");
            var preview = ModpackPreviewService.Preview(zip, Path.Combine(root, "versions"));
            await Assert.That(preview.Valid).IsFalse();
            await Assert.That(preview.Error).Contains("不是可识别");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 文件不存在与空目录() {
        await Assert.That(ModpackPreviewService.Preview(null, "x").Valid).IsFalse();
        await Assert.That(ModpackPreviewService.Preview("", "x").Valid).IsFalse();
        await Assert.That(ModpackPreviewService.Preview("no-such.zip", "x").Valid).IsFalse();
    }

    [Test]
    public async Task 格式化预览文本() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var zip = CreateZip(root, "modrinth.index.json", ModrinthIndex);
            var preview = ModpackPreviewService.Preview(zip, Path.Combine(root, "versions"));
            var text = ModpackPreviewService.Format(preview);
            await Assert.That(text).Contains("Sodium Plus");
            await Assert.That(text).Contains("Fabric 0.15.0");
            await Assert.That(text).Contains("文件：3 个");
            await Assert.That(text).Contains("将新建");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
