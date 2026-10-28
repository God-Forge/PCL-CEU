using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Modpack;

public enum ModpackType {
    Unknown = -1,
    CurseForge = 0,
    HMCL = 1,
    MMC = 2,
    MCBBS = 3,
    Modrinth = 4,
    LauncherPack = 9,
    Compressed = 99
}

public sealed class ModpackInfo {

    public ModpackType Type { get; }
    public string BaseFolder { get; }

    public ModpackInfo(ModpackType type, string baseFolder = "") {
        Type = type;
        BaseFolder = baseFolder;
    }

}

/// <summary>
/// 仅负责识别整合包类型与根目录/一级目录，不执行安装。
/// </summary>
public static class ModpackInspector {

    public static ModpackInfo Inspect(ZipArchive archive) {
        if (archive is null) throw new ArgumentNullException(nameof(archive));

        // 根目录判断
        if (archive.GetEntry("mcbbs.packmeta") is not null) return new ModpackInfo(ModpackType.MCBBS);
        if (archive.GetEntry("mmc-pack.json") is not null) return new ModpackInfo(ModpackType.MMC);
        if (archive.GetEntry("modrinth.index.json") is not null) return new ModpackInfo(ModpackType.Modrinth);

        var rootManifest = archive.GetEntry("manifest.json");
        if (rootManifest is not null) {
            return new ModpackInfo(ReadManifestType(rootManifest));
        }

        if (archive.GetEntry("modpack.json") is not null) return new ModpackInfo(ModpackType.HMCL);
        if (archive.GetEntry("modpack.zip") is not null || archive.GetEntry("modpack.mrpack") is not null) {
            return new ModpackInfo(ModpackType.LauncherPack);
        }

        // 一级目录判断
        foreach (var entry in archive.Entries) {
            var pathSplits = entry.FullName.Split('/');
            if (pathSplits.Length != 2) continue;

            var baseFolder = pathSplits[0] + "/";
            switch (pathSplits[1]) {
                case "mcbbs.packmeta":
                    return new ModpackInfo(ModpackType.MCBBS, baseFolder);
                case "mmc-pack.json":
                    return new ModpackInfo(ModpackType.MMC, baseFolder);
                case "modrinth.index.json":
                    return new ModpackInfo(ModpackType.Modrinth, baseFolder);
                case "manifest.json":
                    return ReadManifestType(entry) == ModpackType.MCBBS
                        ? new ModpackInfo(ModpackType.MCBBS, "overrides/")
                        : new ModpackInfo(ModpackType.CurseForge, baseFolder);
                case "modpack.json":
                    return new ModpackInfo(ModpackType.HMCL, baseFolder);
                case "modpack.zip":
                case "modpack.mrpack":
                    return new ModpackInfo(ModpackType.LauncherPack, baseFolder);
            }
        }

        return new ModpackInfo(ModpackType.Compressed);
    }

    private static ModpackType ReadManifestType(ZipArchiveEntry entry) {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = JObject.Parse(reader.ReadToEnd());
        return json["addons"] is null ? ModpackType.CurseForge : ModpackType.MCBBS;
    }

}
