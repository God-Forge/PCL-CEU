using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Resources;

/// <summary>
/// Modrinth 搜索实现，输出统一的 <see cref="ResourceProject"/>。
/// </summary>
public sealed class ModrinthResourceService : IResourceSearchProvider {

    private const string SearchEndpoint = "https://api.modrinth.com/v2/search";

    private readonly IStringFetcher _fetcher;

    public ModrinthResourceService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public ResourceSource Source => ResourceSource.Modrinth;

    public async Task<ResourceSearchResult> SearchAsync(ResourceSearchQuery query, CancellationToken cancellationToken = default) {
        var url = BuildSearchUrl(query);
        var json = JObject.Parse(await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false));
        var projects = (json["hits"] as JArray ?? [])
            .OfType<JObject>()
            .Select(hit => ParseProject(hit, query.Kind))
            .ToList();
        return new ResourceSearchResult {
            Projects = projects,
            TotalHits = json["total_hits"]?.ToObject<int>() ?? projects.Count
        };
    }

    /// <summary>
    /// 查询项目的版本列表。
    /// </summary>
    public async Task<IReadOnlyList<ResourceVersionFile>> GetVersionsAsync(
        string projectId,
        ResourceKind kind,
        string? minecraftVersion = null,
        string? loader = null,
        CancellationToken cancellationToken = default) {
        var url = BuildVersionsUrl(projectId, minecraftVersion, loader);
        var array = JArray.Parse(await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false));
        return array
            .OfType<JObject>()
            .Select(version => ResourceVersionParser.ParseModrinth(version, kind))
            .ToList();
    }

    /// <summary>构造版本列表地址，便于测试断言。</summary>
    public static string BuildVersionsUrl(string projectId, string? minecraftVersion = null, string? loader = null) {
        var url = $"https://api.modrinth.com/v2/project/{Uri.EscapeDataString(projectId)}/version";
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(minecraftVersion)) {
            query.Add("game_versions=" + Uri.EscapeDataString($"[\"{minecraftVersion}\"]"));
        }
        if (!string.IsNullOrWhiteSpace(loader)) {
            query.Add("loaders=" + Uri.EscapeDataString($"[\"{loader}\"]"));
        }
        return query.Count == 0 ? url : url + "?" + string.Join("&", query);
    }

    /// <summary>构造搜索地址，便于测试断言。</summary>
    public static string BuildSearchUrl(ResourceSearchQuery query) {
        var facets = BuildFacets(query);
        return SearchEndpoint
               + "?query=" + Uri.EscapeDataString(query.Text ?? "")
               + "&limit=" + Math.Clamp(query.Limit, 1, 100)
               + "&offset=" + Math.Max(0, query.Offset)
               + "&facets=" + Uri.EscapeDataString(facets);
    }

    /// <summary>把资源类型与过滤条件转成 Modrinth 的 facets 表达式。</summary>
    public static string BuildFacets(ResourceSearchQuery query) {
        var groups = new List<string> { $"[\"project_type:{ToProjectType(query.Kind)}\"]" };
        if (!string.IsNullOrWhiteSpace(query.MinecraftVersion)) {
            groups.Add($"[\"versions:{query.MinecraftVersion}\"]");
        }
        if (!string.IsNullOrWhiteSpace(query.Loader)) {
            groups.Add($"[\"categories:{query.Loader}\"]");
        }
        return "[" + string.Join(",", groups) + "]";
    }

    /// <summary>资源类型对应的 Modrinth project_type。</summary>
    public static string ToProjectType(ResourceKind kind) => kind switch {
        ResourceKind.Modpack => "modpack",
        ResourceKind.ResourcePack => "resourcepack",
        ResourceKind.ShaderPack => "shader",
        ResourceKind.DataPack => "datapack",
        _ => "mod"
    };

    /// <summary>资源类型对应的 Modrinth 页面路径段。</summary>
    public static string ToUrlSegment(ResourceKind kind) => kind switch {
        ResourceKind.Modpack => "modpack",
        ResourceKind.ResourcePack => "resourcepack",
        ResourceKind.ShaderPack => "shader",
        ResourceKind.DataPack => "datapack",
        _ => "mod"
    };

    private static ResourceProject ParseProject(JObject hit, ResourceKind kind) {
        var slug = hit["slug"]?.ToString() ?? "";
        return new ResourceProject {
            Source = ResourceSource.Modrinth,
            Kind = kind,
            Id = hit["project_id"]?.ToString() ?? slug,
            Slug = slug,
            Title = hit["title"]?.ToString() ?? "",
            Description = hit["description"]?.ToString() ?? "",
            Author = hit["author"]?.ToString() ?? "",
            IconUrl = hit["icon_url"]?.ToString() ?? "",
            Downloads = hit["downloads"]?.ToObject<long>() ?? 0,
            Follows = hit["follows"]?.ToObject<long>() ?? 0,
            Categories = ReadStringArray(hit["categories"]),
            Loaders = ReadStringArray(hit["loaders"]),
            Versions = ReadStringArray(hit["versions"]),
            ProjectUrl = string.IsNullOrEmpty(slug) ? "" : $"https://modrinth.com/{ToUrlSegment(kind)}/{slug}"
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JToken? token) {
        return (token as JArray)?.Select(item => item.ToString()).ToList() ?? [];
    }

}
