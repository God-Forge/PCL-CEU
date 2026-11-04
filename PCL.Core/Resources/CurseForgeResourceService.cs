using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>
/// CurseForge 搜索实现。需要 API Key（PCL 向来通过 <c>PCL_CURSEFORGE_API_KEY</c> 环境变量提供）。
/// </summary>
public sealed class CurseForgeResourceService : IResourceSearchProvider {

    private const string SearchEndpoint = "https://api.curseforge.com/v1/mods/search";

    private readonly IStringFetcher _fetcher;
    private readonly string _apiKey;

    public CurseForgeResourceService(string? apiKey, IStringFetcher? fetcher = null) {
        _apiKey = apiKey ?? "";
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public ResourceSource Source => ResourceSource.CurseForge;

    /// <summary>是否已配置 API Key。</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>从环境变量创建，未配置时返回 <c>null</c>。</summary>
    public static CurseForgeResourceService? FromEnvironment(IStringFetcher? fetcher = null) {
        var key = Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? null : new CurseForgeResourceService(key, fetcher);
    }

    public async Task<ResourceSearchResult> SearchAsync(ResourceSearchQuery query, CancellationToken cancellationToken = default) {
        if (!IsConfigured) throw new InvalidOperationException("未配置 CurseForge API Key");
        var url = BuildSearchUrl(query);
        var json = JObject.Parse(await _fetcher
            .GetStringAsync(url, new Dictionary<string, string> { ["x-api-key"] = _apiKey }, cancellationToken)
            .ConfigureAwait(false));
        var projects = (json["data"] as JArray ?? [])
            .OfType<JObject>()
            .Select(item => ParseProject(item, query.Kind))
            .ToList();
        return new ResourceSearchResult {
            Projects = projects,
            TotalHits = json["pagination"]?["totalCount"]?.ToObject<int>() ?? projects.Count
        };
    }

    /// <summary>
    /// 查询项目的文件列表。
    /// </summary>
    public async Task<IReadOnlyList<ResourceVersionFile>> GetFilesAsync(
        string modId,
        ResourceKind kind,
        string? minecraftVersion = null,
        string? loader = null,
        CancellationToken cancellationToken = default) {
        if (!IsConfigured) throw new InvalidOperationException("未配置 CurseForge API Key");
        var url = BuildFilesUrl(modId, minecraftVersion, loader);
        var json = JObject.Parse(await _fetcher
            .GetStringAsync(url, new Dictionary<string, string> { ["x-api-key"] = _apiKey }, cancellationToken)
            .ConfigureAwait(false));
        return (json["data"] as JArray ?? [])
            .OfType<JObject>()
            .Select(file => ResourceVersionParser.ParseCurseForge(file, kind))
            .ToList();
    }

    /// <summary>构造文件列表地址，便于测试断言。</summary>
    public static string BuildFilesUrl(string modId, string? minecraftVersion = null, string? loader = null) {
        var url = $"https://api.curseforge.com/v1/mods/{Uri.EscapeDataString(modId)}/files";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(minecraftVersion)) {
            query.Add("gameVersion=" + Uri.EscapeDataString(minecraftVersion));
        }
        if (!string.IsNullOrWhiteSpace(loader) && ToModLoaderType(loader) is { } loaderType) {
            query.Add("modLoaderType=" + loaderType);
        }
        return query.Count == 0 ? url : url + "?" + string.Join("&", query);
    }

    /// <summary>构造搜索地址，便于测试断言。</summary>
    public static string BuildSearchUrl(ResourceSearchQuery query) {
        var url = SearchEndpoint
                  + "?gameId=432"
                  + "&classId=" + ToClassId(query.Kind)
                  + "&index=" + Math.Max(0, query.Offset)
                  + "&pageSize=" + Math.Clamp(query.Limit, 1, 50)
                  + "&sortField=2&sortOrder=desc";
        if (!string.IsNullOrWhiteSpace(query.Text)) {
            url += "&searchFilter=" + Uri.EscapeDataString(query.Text);
        }
        if (!string.IsNullOrWhiteSpace(query.MinecraftVersion)) {
            url += "&gameVersion=" + Uri.EscapeDataString(query.MinecraftVersion);
        }
        if (!string.IsNullOrWhiteSpace(query.Loader) && ToModLoaderType(query.Loader) is { } loaderType) {
            url += "&modLoaderType=" + loaderType;
        }
        return url;
    }

    /// <summary>资源类型对应的 CurseForge classId。</summary>
    public static int ToClassId(ResourceKind kind) => kind switch {
        ResourceKind.Modpack => 4471,
        ResourceKind.ResourcePack => 12,
        ResourceKind.ShaderPack => 6552,
        ResourceKind.DataPack => 6945,
        _ => 6
    };

    /// <summary>加载器名对应的 CurseForge modLoaderType，未知返回 <c>null</c>。</summary>
    public static int? ToModLoaderType(string loader) => loader.Trim().ToLowerInvariant() switch {
        "forge" => 1,
        "fabric" => 4,
        "quilt" => 5,
        "neoforge" => 6,
        _ => null
    };

    private static ResourceProject ParseProject(JObject item, ResourceKind kind) {
        var slug = item["slug"]?.ToString() ?? "";
        var website = item["links"]?["websiteUrl"]?.ToString() ?? "";
        var authors = (item["authors"] as JArray ?? []).Select(author => author["name"]?.ToString()).OfType<string>();
        var loaders = (item["latestFilesIndexes"] as JArray ?? [])
            .Select(index => index["modLoader"]?.ToObject<int>())
            .OfType<int>()
            .Select(FromModLoaderType)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var versions = (item["latestFilesIndexes"] as JArray ?? [])
            .Select(index => index["gameVersion"]?.ToString())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return new ResourceProject {
            Source = ResourceSource.CurseForge,
            Kind = kind,
            Id = item["id"]?.ToString() ?? slug,
            Slug = slug,
            Title = item["name"]?.ToString() ?? "",
            Description = item["summary"]?.ToString() ?? "",
            Author = string.Join(", ", authors),
            IconUrl = item["logo"]?["url"]?.ToString() ?? "",
            Downloads = item["downloadCount"]?.ToObject<long>() ?? 0,
            Follows = item["thumbsUpCount"]?.ToObject<long>() ?? 0,
            Categories = (item["categories"] as JArray ?? [])
                .Select(category => category["name"]?.ToString())
                .OfType<string>()
                .ToList(),
            Loaders = loaders.ToList(),
            Versions = versions.ToList(),
            ProjectUrl = website
        };
    }

    private static string? FromModLoaderType(int type) => type switch {
        1 => "forge",
        4 => "fabric",
        5 => "quilt",
        6 => "neoforge",
        _ => null
    };

}
