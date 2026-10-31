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

    public static (bool UseOriginal, string[] Mirrors) GetLibrary(string original) {
        var useOriginal = !new[] { "minecraftforge", "fabricmc", "neoforged" }.Any(key => original.Contains(key));
        var maven = original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/maven")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/maven")
            .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven");
        var libraries = original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/libraries")
            .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/libraries");
        return (useOriginal, [maven, libraries]);
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

    public static IReadOnlyList<(string Url, int TimeoutSeconds)> GetModRequestSources(string url, int setting) {
        var mirror = GetMod(url);
        if (mirror == url) {
            var timeout = url.Contains("modrinth") ? 20 : 10;
            return [(url, timeout), (url, 30)];
        }
        return setting switch {
            0 => [(mirror, 10), (mirror, 10), (url, 30)],
            1 => [(url, url.Contains("modrinth") ? 20 : 10), (mirror, 10), (url, 30), (mirror, 30)],
            _ => [(url, url.Contains("modrinth") ? 20 : 10), (url, 30), (mirror, 30)]
        };
    }

}
