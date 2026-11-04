namespace PCL.Core.Resources;

/// <summary>
/// 聚合多个资源来源的搜索：并发请求、去重、按相关度与下载量排序。
/// 单个来源失败不影响整体结果，失败原因通过 <see cref="ResourceSearchResult.Failures"/> 返回。
/// </summary>
public sealed class ResourceSearchService {

    private readonly IReadOnlyList<IResourceSearchProvider> _providers;

    public ResourceSearchService(IEnumerable<IResourceSearchProvider> providers) {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
    }

    /// <summary>可用的来源。</summary>
    public IReadOnlyList<ResourceSource> Sources => _providers.Select(provider => provider.Source).ToList();

    /// <summary>
    /// 默认配置：Modrinth 始终可用，CurseForge 仅在配置了 API Key 时启用。
    /// </summary>
    public static ResourceSearchService CreateDefault(Net.IStringFetcher? fetcher = null) {
        var providers = new List<IResourceSearchProvider> { new ModrinthResourceService(fetcher) };
        if (CurseForgeResourceService.FromEnvironment(fetcher) is { } curseForge) {
            providers.Add(curseForge);
        }
        return new ResourceSearchService(providers);
    }

    public async Task<ResourceSearchResult> SearchAsync(ResourceSearchQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        var providers = _providers
            .Where(provider => query.Sources is null || query.Sources.Contains(provider.Source))
            .ToList();
        if (providers.Count == 0) {
            return new ResourceSearchResult { Projects = [], TotalHits = 0 };
        }

        var failures = new List<string>();
        var batches = await Task.WhenAll(providers.Select(async provider => {
            try {
                return await provider.SearchAsync(query, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                lock (failures) {
                    failures.Add($"{provider.Source}: {ex.Message}");
                }
                return null;
            }
        })).ConfigureAwait(false);

        var totalHits = batches.Where(batch => batch is not null).Sum(batch => batch!.TotalHits);
        var projects = batches
            .Where(batch => batch is not null)
            .SelectMany(batch => batch!.Projects)
            .ToList();
        var merged = Merge(projects, query.Text);
        return new ResourceSearchResult {
            Projects = merged.Take(Math.Max(1, query.Limit)).ToList(),
            TotalHits = totalHits,
            Failures = failures.ToList()
        };
    }

    /// <summary>
    /// 去重并排序：标题命中查询词的优先，其次按下载量降序，最后按标题稳定排序。
    /// </summary>
    public static IReadOnlyList<ResourceProject> Merge(IEnumerable<ResourceProject> projects, string? query) {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<ResourceProject>();
        foreach (var project in projects) {
            if (seen.Add(project.Key)) merged.Add(project);
        }
        return merged
            .OrderByDescending(project => Score(project, query))
            .ThenByDescending(project => project.Downloads)
            .ThenBy(project => project.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int Score(ResourceProject project, string? query) {
        if (string.IsNullOrWhiteSpace(query)) return 0;
        var keyword = query.Trim();
        if (project.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return 2;
        return project.Slug.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

}
