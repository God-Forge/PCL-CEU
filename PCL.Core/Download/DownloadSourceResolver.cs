namespace PCL.Core.Download;

/// <summary>
/// 生成 Mojang 与 Mod 下载源的镜像地址。
/// </summary>
public static class DownloadSourceResolver {

    public static (string Original, string Mirror) GetLauncherOrMeta(string original) {
        if (original is null) throw new ArgumentNullException(nameof(original));
        var mirror = original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com");
        return (original, mirror);
    }

    public static (string Original, string Mirror) GetAssets(string original) {
        original = original.Replace("http://resources.download.minecraft.net", "https://resources.download.minecraft.net");
        var mirror = original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets")
            .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
        return (original, mirror);
    }

    public static string GetMod(string original) {
        return original
            .Replace("api.modrinth.com", "mod.mcimirror.top/modrinth")
            .Replace("staging-api.modrinth.com", "mod.mcimirror.top/modrinth")
            .Replace("cdn.modrinth.com", "mod.mcimirror.top")
            .Replace("api.curseforge.com", "mod.mcimirror.top/curseforge")
            .Replace("edge.forgecdn.net", "mod.mcimirror.top")
            .Replace("mediafilez.forgecdn.net", "mod.mcimirror.top")
            .Replace("media.forgecdn.net", "mod.mcimirror.top");
    }

}
