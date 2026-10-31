using PCL.Core.Download;

namespace PCL.Core.Tests;

public class DownloadSourceResolverTests {

    [Test]
    public async Task 生成Mojang镜像地址() {
        var (original, mirror) = DownloadSourceResolver.GetLauncherOrMeta(
            "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
        await Assert.That(original).IsEqualTo("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");
        await Assert.That(mirror).IsEqualTo("https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json");
    }

    [Test]
    public async Task 生成Mod请求源顺序() {
        var mirrorFirst = DownloadSourceResolver.GetModRequestSources("https://api.modrinth.com/v2/project/a", 0);
        await Assert.That(mirrorFirst.Count).IsEqualTo(3);
        await Assert.That(mirrorFirst[0].Url).IsEqualTo("https://mod.mcimirror.top/modrinth/v2/project/a");
        await Assert.That(mirrorFirst[2].TimeoutSeconds).IsEqualTo(30);

        var officialOnly = DownloadSourceResolver.GetModRequestSources("https://example.com/a", 0);
        await Assert.That(officialOnly.Count).IsEqualTo(2);
        await Assert.That(officialOnly[0].TimeoutSeconds).IsEqualTo(10);
    }

    [Test]
    public async Task 库文件特殊源不包含原版() {
        var special = DownloadSourceResolver.GetLibrary("https://libraries.minecraft.net/minecraftforge/a.jar");
        await Assert.That(special.UseOriginal).IsFalse();
        await Assert.That(special.Mirrors.Length).IsEqualTo(2);
        await Assert.That(special.Mirrors[0]).Contains("bmclapi2.bangbang93.com/maven");

        var normal = DownloadSourceResolver.GetLibrary("https://libraries.minecraft.net/a.jar");
        await Assert.That(normal.UseOriginal).IsTrue();
        await Assert.That(normal.Mirrors[1]).IsEqualTo("https://bmclapi2.bangbang93.com/libraries/a.jar");
    }

    [Test]
    public async Task 生成Assets镜像地址并升级HTTPS() {
        var (original, mirror) = DownloadSourceResolver.GetAssets(
            "http://resources.download.minecraft.net/ab/abcdef");
        await Assert.That(original).IsEqualTo("https://resources.download.minecraft.net/ab/abcdef");
        await Assert.That(mirror).IsEqualTo("https://bmclapi2.bangbang93.com/assets/ab/abcdef");
    }

    [Test]
    public async Task 生成Mod镜像地址() {
        await Assert.That(DownloadSourceResolver.GetMod("https://cdn.modrinth.com/a.jar"))
            .IsEqualTo("https://mod.mcimirror.top/a.jar");
        await Assert.That(DownloadSourceResolver.GetMod("https://edge.forgecdn.net/b.jar"))
            .IsEqualTo("https://mod.mcimirror.top/b.jar");
    }

}
