using Newtonsoft.Json.Linq;

namespace PCL.Core.Net;

public sealed class ModrinthProject {

    public string Slug { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public long Downloads { get; init; }
    public string ProjectType { get; init; } = "";

}

/// <summary>
/// Modrinth 搜索服务，供下载页的 Mod/资源包/光影/整合包列表使用。
/// </summary>
public sealed class ModrinthSearchService {

    private readonly IStringFetcher _fetcher;

    public ModrinthSearchService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<IReadOnlyList<ModrinthProject>> SearchAsync(
        string query,
        string projectType,
        int offset = 0,
        int limit = 20,
        CancellationToken cancellationToken = default) {
        var facets = Uri.EscapeDataString($"[[\"project_type:{projectType}\"]]");
        var url = "https://api.modrinth.com/v2/search"
                  + "?query=" + Uri.EscapeDataString(query ?? "")
                  + "&limit=" + Math.Clamp(limit, 1, 50)
                  + "&offset=" + Math.Max(0, offset)
                  + "&facets=" + facets;
        var json = JObject.Parse(await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false));
        return (json["hits"] as JArray)?.OfType<JObject>().Select(hit => new ModrinthProject {
            Slug = hit["slug"]?.ToString() ?? "",
            Title = hit["title"]?.ToString() ?? "",
            Description = hit["description"]?.ToString() ?? "",
            Author = hit["author"]?.ToString() ?? "",
            Downloads = hit["downloads"]?.ToObject<long>() ?? 0,
            ProjectType = hit["project_type"]?.ToString() ?? projectType
        }).ToList() ?? new List<ModrinthProject>();
    }

}
