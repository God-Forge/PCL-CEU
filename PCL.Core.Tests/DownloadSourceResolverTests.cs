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
    public async Task 生成Mod镜像地址() {
        await Assert.That(DownloadSourceResolver.GetMod("https://cdn.modrinth.com/a.jar"))
            .IsEqualTo("https://mod.mcimirror.top/a.jar");
        await Assert.That(DownloadSourceResolver.GetMod("https://edge.forgecdn.net/b.jar"))
            .IsEqualTo("https://mod.mcimirror.top/b.jar");
    }

}
