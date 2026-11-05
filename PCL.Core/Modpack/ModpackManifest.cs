using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Modpack;

/// <summary>整合包格式。</summary>
public enum ModpackFormat {
    Unknown,
    Modrinth,
    CurseForge
}

/// <summary>整合包内的单个文件条目。</summary>
public sealed class ModpackFileEntry {

    /// <summary>相对实例目录的路径，统一使用正斜杠。</summary>
    public string Path { get; init; } = "";
    public string Url { get; init; } = "";
    public long Size { get; init; }
    public string Sha1 { get; init; } = "";
    public bool Required { get; init; } = true;

    /// <summary>CurseForge 格式只有项目与文件 ID，没有直接下载地址。</summary>
    public bool HasDownload => !string.IsNullOrWhiteSpace(Url);

}

/// <summary>整合包清单。</summary>
public sealed class ModpackManifest {

    public ModpackFormat Format { get; init; }
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string? MinecraftVersion { get; init; }

    /// <summary>清单声明的加载器（Fabric / Forge / NeoForge / Quilt），未知为 <c>null</c>。</summary>
    public string? Loader { get; init; }

    /// <summary>清单声明的加载器版本。</summary>
    public string? LoaderVersion { get; init; }

    public string OverridesFolder { get; init; } = "overrides";
    public IReadOnlyList<ModpackFileEntry> Files { get; init; } = [];

}

/// <summary>
/// 解析整合包清单：支持 Modrinth 的 modrinth.index.json 与 CurseForge 的 manifest.json。
/// </summary>
public static class ModpackManifestParser {

    public const string ModrinthIndexFile = "modrinth.index.json";
    public const string CurseForgeManifestFile = "manifest.json";

    /// <summary>从 JSON 文本解析清单。无法识别时返回 <c>null</c>。</summary>
    public static ModpackManifest? Parse(string? jsonText) {
        if (string.IsNullOrWhiteSpace(jsonText)) return null;
        JObject json;
        try {
            json = JObject.Parse(jsonText);
        } catch (Newtonsoft.Json.JsonException) {
            return null;
        }
        if (json["files"] is not JArray files) return null;
        // Modrinth：带 formatVersion 且 game 为 minecraft
        if (json["formatVersion"] is not null && json["game"]?.ToString() == "minecraft") {
            var (loader, loaderVersion) = ParseModrinthLoader(json["dependencies"] as JObject);
            return new ModpackManifest {
                Format = ModpackFormat.Modrinth,
                Name = json["name"]?.ToString() ?? "",
                Version = json["versionId"]?.ToString() ?? "",
                MinecraftVersion = json["dependencies"]?["minecraft"]?.ToString(),
                Loader = loader,
                LoaderVersion = loaderVersion,
                OverridesFolder = "overrides",
                Files = files.OfType<JObject>().Select(ParseModrinthFile).Where(file => file is not null).ToList()!
            };
        }
        // CurseForge：带 manifestType
        if (json["manifestType"]?.ToString() == "minecraftModpack") {
            var (loader, loaderVersion) = ParseCurseForgeLoader(json["minecraft"]?["modLoaders"] as JArray);
            return new ModpackManifest {
                Format = ModpackFormat.CurseForge,
                Name = json["name"]?.ToString() ?? "",
                Version = json["version"]?.ToString() ?? "",
                MinecraftVersion = json["minecraft"]?["version"]?.ToString(),
                Loader = loader,
                LoaderVersion = loaderVersion,
                OverridesFolder = json["overrides"]?.ToString() ?? "overrides",
                Files = files.OfType<JObject>().Select(file => new ModpackFileEntry {
                    Path = "",
                    Required = file["required"]?.ToObject<bool>() ?? true
                }).ToList()
            };
        }
        return null;
    }

    /// <summary>解析 Modrinth 清单里的加载器依赖。</summary>
    private static (string? Loader, string? Version) ParseModrinthLoader(JObject? dependencies) {
        if (dependencies is null) return (null, null);
        foreach (var (key, name) in new[] {
                     ("fabric-loader", "Fabric"), ("forge", "Forge"),
                     ("neoforge", "NeoForge"), ("quilt-loader", "Quilt")
                 }) {
            if (dependencies[key] is { } value) return (name, value.ToString());
        }
        return (null, null);
    }

    /// <summary>解析 CurseForge 清单里的主加载器（取 primary 项）。</summary>
    private static (string? Loader, string? Version) ParseCurseForgeLoader(JArray? loaders) {
        if (loaders is null) return (null, null);
        foreach (var loader in loaders.OfType<JObject>()) {
            if (loader["primary"]?.ToObject<bool>() != true) continue;
            var id = loader["id"]?.ToString() ?? "";
            // 形如 forge-47.2.0 / fabric-0.15.0 / neoforge-20.4.1
            var dash = id.IndexOf('-');
            if (dash <= 0) return (null, null);
            var name = id[..dash].ToLowerInvariant() switch {
                "forge" => "Forge",
                "fabric" => "Fabric",
                "neoforge" => "NeoForge",
                "quilt" => "Quilt",
                _ => null
            };
            return (name, id[(dash + 1)..]);
        }
        return (null, null);
    }

    /// <summary>从整合包 zip 中读取清单，优先 Modrinth 格式。</summary>
    public static ModpackManifest? ReadFromZip(string zipPath) {
        if (!File.Exists(zipPath)) return null;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var name in new[] { ModrinthIndexFile, CurseForgeManifestFile }) {
            var entry = archive.GetEntry(name);
            if (entry is null) continue;
            using var reader = new StreamReader(entry.Open());
            var manifest = Parse(reader.ReadToEnd());
            if (manifest is not null) return manifest;
        }
        return null;
    }

    private static ModpackFileEntry? ParseModrinthFile(JObject file) {
        var path = file["path"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(path)) return null;
        var downloads = (file["downloads"] as JArray)?.Select(item => item.ToString()).ToList() ?? [];
        return new ModpackFileEntry {
            Path = NormalizeRelativePath(path),
            Url = downloads.FirstOrDefault() ?? "",
            Size = file["fileSize"]?.ToObject<long>() ?? 0,
            Sha1 = file["hashes"]?["sha1"]?.ToString() ?? "",
            Required = true
        };
    }

    /// <summary>把路径分隔符统一为正斜杠，并去掉开头的斜杠。</summary>
    public static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

}
