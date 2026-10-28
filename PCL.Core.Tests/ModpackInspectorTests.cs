using System.IO.Compression;
using System.Text;
using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ModpackInspectorTests {

    private static ZipArchive CreateArchive(params (string Name, string? Content)[] entries) {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
            foreach (var (name, content) in entries) {
                var entry = archive.CreateEntry(name);
                if (content is null) continue;
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }

    [Test]
    public async Task 根目录识别CurseForge与MCBBS() {
        using (var archive = CreateArchive(("manifest.json", "{\"minecraft\":{}}"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.CurseForge);
            await Assert.That(info.BaseFolder).IsEqualTo("");
        }

        using (var archive = CreateArchive(("manifest.json", "{\"addons\":[]}"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.MCBBS);
            await Assert.That(info.BaseFolder).IsEqualTo("");
        }
    }

    [Test]
    public async Task 根目录识别常见整合包类型() {
        using (var archive = CreateArchive(("mcbbs.packmeta", "{}"))) {
            await Assert.That(ModpackInspector.Inspect(archive).Type).IsEqualTo(ModpackType.MCBBS);
        }
        using (var archive = CreateArchive(("mmc-pack.json", "{}"))) {
            await Assert.That(ModpackInspector.Inspect(archive).Type).IsEqualTo(ModpackType.MMC);
        }
        using (var archive = CreateArchive(("modrinth.index.json", "{}"))) {
            await Assert.That(ModpackInspector.Inspect(archive).Type).IsEqualTo(ModpackType.Modrinth);
        }
        using (var archive = CreateArchive(("modpack.json", "{}"))) {
            await Assert.That(ModpackInspector.Inspect(archive).Type).IsEqualTo(ModpackType.HMCL);
        }
        using (var archive = CreateArchive(("modpack.zip", null))) {
            await Assert.That(ModpackInspector.Inspect(archive).Type).IsEqualTo(ModpackType.LauncherPack);
        }
    }

    [Test]
    public async Task 一级目录识别并返回基础目录() {
        using (var archive = CreateArchive(("pack/manifest.json", "{\"minecraft\":{}}"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.CurseForge);
            await Assert.That(info.BaseFolder).IsEqualTo("pack/");
        }

        using (var archive = CreateArchive(("pack/manifest.json", "{\"addons\":[]}"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.MCBBS);
            await Assert.That(info.BaseFolder).IsEqualTo("overrides/");
        }

        using (var archive = CreateArchive(("pack/modrinth.index.json", "{}"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.Modrinth);
            await Assert.That(info.BaseFolder).IsEqualTo("pack/");
        }
    }

    [Test]
    public async Task 未知压缩包按普通压缩包处理() {
        using (var archive = CreateArchive(("readme.txt", "hello"))) {
            var info = ModpackInspector.Inspect(archive);
            await Assert.That(info.Type).IsEqualTo(ModpackType.Compressed);
        }
    }

}
