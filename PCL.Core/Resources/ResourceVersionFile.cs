using Newtonsoft.Json.Linq;

namespace PCL.Core.Resources;

/// <summary>发布状态，取值与 CurseForge 的 fileReleaseType 一致。</summary>
public enum ResourceReleaseType {
    Release = 1,
    Beta = 2,
    Alpha = 3
}

/// <summary>
/// 社区资源的单个可下载版本，屏蔽 Modrinth 与 CurseForge 的差异。
/// </summary>
public sealed class ResourceVersionFile {

    /// <summary>来源中的版本/文件 ID。</summary>
    public string Id { get; init; } = "";

    public ResourceSource Source { get; init; }
    public ResourceKind Kind { get; init; }

    /// <summary>上传者自定义的显示名，不等于文件名。</summary>
    public string Display { get; init; } = "";

    public DateTimeOffset? ReleaseDate { get; init; }
    public long DownloadCount { get; init; }

    /// <summary>版本号，不保证是标准格式。</summary>
    public string Version { get; init; } = "";

    public IReadOnlyList<string> Loaders { get; init; } = [];
    public IReadOnlyList<string> GameVersions { get; init; } = [];
    public ResourceReleaseType ReleaseType { get; init; } = ResourceReleaseType.Release;

    public string FileName { get; init; } = "";
    public IReadOnlyList<string> DownloadUrls { get; init; } = [];

    /// <summary>SHA1 或 MD5，小写。</summary>
    public string Hash { get; init; } = "";

    /// <summary>文件大小（字节），未知为 -1。</summary>
    public long Size { get; init; } = -1;

    /// <summary>依赖的项目 ID。</summary>
    public IReadOnlyList<string> Dependencies { get; init; } = [];

    /// <summary>是否具备文件名与下载地址。</summary>
    public bool DownloadAvailable => !string.IsNullOrEmpty(FileName) && DownloadUrls.Count > 0;

    /// <summary>首选下载地址。</summary>
    public string? PrimaryUrl => DownloadUrls.FirstOrDefault();

    /// <summary>发布状态的友好描述。</summary>
    public string ReleaseTypeDisplay => ReleaseType switch {
        ResourceReleaseType.Release => "正式版",
        ResourceReleaseType.Beta => "测试版",
        _ => "早期测试版"
    };

}

/// <summary>
/// 把 Modrinth / CurseForge 的版本 JSON 解析为 <see cref="ResourceVersionFile"/>。
/// </summary>
public static class ResourceVersionParser {

    /// <summary>解析 Modrinth 的单个 version 对象。</summary>
    public static ResourceVersionFile ParseModrinth(JObject data, ResourceKind kind) {
        var files = data["files"] as JArray ?? [];
        var primary = files.OfType<JObject>().FirstOrDefault(file => file["primary"]?.ToObject<bool>() == true)
                      ?? files.OfType<JObject>().FirstOrDefault();
        return new ResourceVersionFile {
            Id = data["id"]?.ToString() ?? "",
            Source = ResourceSource.Modrinth,
            Kind = kind,
            Display = data["name"]?.ToString() ?? "",
            Version = data["version_number"]?.ToString() ?? "",
            ReleaseDate = ReadDate(data["date_published"]),
            DownloadCount = data["downloads"]?.ToObject<long>() ?? 0,
            Loaders = ReadStringArray(data["loaders"]),
            GameVersions = ReadStringArray(data["game_versions"]),
            ReleaseType = ToReleaseType(data["release_type"]?.ToString()),
            FileName = primary?["filename"]?.ToString() ?? "",
            DownloadUrls = primary?["url"]?.ToString() is { Length: > 0 } url ? [url] : [],
            Hash = primary?["hashes"]?["sha1"]?.ToString() ?? "",
            Size = primary?["size"]?.ToObject<long>() ?? -1,
            Dependencies = ReadModrinthDependencies(data["dependencies"])
        };
    }

    /// <summary>解析 CurseForge 的单个 file 对象。</summary>
    public static ResourceVersionFile ParseCurseForge(JObject data, ResourceKind kind) {
        var (gameVersions, loaders) = SplitCurseForgeGameVersions(ReadStringArray(data["gameVersions"]));
        var urls = new List<string>();
        if (data["downloadUrl"]?.ToString() is { Length: > 0 } url) urls.Add(url);
        return new ResourceVersionFile {
            Id = data["id"]?.ToString() ?? "",
            Source = ResourceSource.CurseForge,
            Kind = kind,
            Display = data["displayName"]?.ToString() ?? "",
            Version = data["displayName"]?.ToString() ?? "",
            ReleaseDate = ReadDate(data["fileDate"]),
            DownloadCount = data["downloadCount"]?.ToObject<long>() ?? 0,
            Loaders = loaders,
            GameVersions = gameVersions,
            ReleaseType = (ResourceReleaseType)(data["releaseType"]?.ToObject<int>() ?? 1),
            FileName = data["fileName"]?.ToString() ?? "",
            DownloadUrls = urls,
            Hash = (data["hashes"] as JArray ?? []).OfType<JObject>()
                .Select(hash => hash["value"]?.ToString())
                .OfType<string>()
                .FirstOrDefault() ?? "",
            Size = data["fileLength"]?.ToObject<long>() ?? -1,
            Dependencies = ReadCurseForgeDependencies(data["dependencies"])
        };
    }

    /// <summary>CurseForge 的 gameVersions 同时混有游戏版本与加载器名，需要拆开。</summary>
    public static (IReadOnlyList<string> GameVersions, IReadOnlyList<string> Loaders) SplitCurseForgeGameVersions(
        IReadOnlyList<string> values) {
        var versions = new List<string>();
        var loaders = new List<string>();
        foreach (var value in values) {
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (IsLoaderName(value)) loaders.Add(NormalizeLoader(value));
            else versions.Add(value);
        }
        return (versions, loaders);
    }

    private static bool IsLoaderName(string value) =>
        value.Equals("Fabric", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Forge", StringComparison.OrdinalIgnoreCase)
        || value.Equals("NeoForge", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Quilt", StringComparison.OrdinalIgnoreCase)
        || value.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase)
        || value.Equals("Rift", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLoader(string value) => value.ToLowerInvariant() switch {
        "fabric" => "fabric",
        "forge" => "forge",
        "neoforge" => "neoforge",
        "quilt" => "quilt",
        "liteloader" => "liteloader",
        "rift" => "rift",
        _ => value.ToLowerInvariant()
    };

    private static ResourceReleaseType ToReleaseType(string? value) => value?.ToLowerInvariant() switch {
        "beta" => ResourceReleaseType.Beta,
        "alpha" => ResourceReleaseType.Alpha,
        _ => ResourceReleaseType.Release
    };

    private static DateTimeOffset? ReadDate(JToken? token) {
        var text = token?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : DateTimeOffset.TryParse(text, out var date) ? date : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JToken? token) =>
        (token as JArray)?.Select(item => item.ToString()).Where(item => item.Length > 0).ToList() ?? [];

    private static IReadOnlyList<string> ReadModrinthDependencies(JToken? token) =>
        (token as JArray ?? [])
        .OfType<JObject>()
        .Where(dependency => dependency["dependency_type"]?.ToString() == "required")
        .Select(dependency => dependency["project_id"]?.ToString())
        .OfType<string>()
        .Where(id => id.Length > 0)
        .ToList();

    private const int CurseForgeRequiredDependency = 3;

    private static IReadOnlyList<string> ReadCurseForgeDependencies(JToken? token) =>
        (token as JArray ?? [])
        .OfType<JObject>()
        .Where(dependency => dependency["relationType"]?.ToObject<int>() == CurseForgeRequiredDependency)
        .Select(dependency => dependency["modId"]?.ToString())
        .OfType<string>()
        .ToList();

}
