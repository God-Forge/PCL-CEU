namespace PCL.Core.Resources;

/// <summary>
/// 按项目 ID 获取版本列表的能力。用于依赖解析，便于在测试中替换成假实现。
/// </summary>
public interface IResourceVersionSource {

    Task<IReadOnlyList<ResourceVersionFile>> GetVersionsAsync(
        string projectId,
        ResourceKind kind,
        string? minecraftVersion = null,
        string? loader = null,
        CancellationToken cancellationToken = default);

}

/// <summary>依赖解析结果。</summary>
public sealed class ResourceDependencyPlan {

    /// <summary>可下载文件，第一项是根资源本身。</summary>
    public IReadOnlyList<ResourceVersionFile> Files { get; init; } = [];

    /// <summary>未能解析的依赖项目 ID。</summary>
    public IReadOnlyList<string> Unresolved { get; init; } = [];

    /// <summary>因超出深度或循环而跳过的项目 ID。</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    /// <summary>依赖关系边：从父资源指向其依赖，用于绘制依赖图。</summary>
    public IReadOnlyList<DependencyEdge> Edges { get; init; } = [];

    public bool IsComplete => Unresolved.Count == 0;

}

/// <summary>
/// 递归解析资源依赖：读取每个版本的必需依赖，按需拉取最新兼容版本。
/// 对应 VB 版 ModDownloadLib 中的依赖展开逻辑。
/// </summary>
public static class ResourceDependencyResolver {

    /// <summary>默认递归深度上限，避免依赖链异常时无限展开。</summary>
    public const int DefaultMaxDepth = 8;

    public static async Task<ResourceDependencyPlan> ResolveAsync(
        ResourceVersionFile root,
        IResourceVersionSource source,
        string? minecraftVersion = null,
        string? loader = null,
        int maxDepth = DefaultMaxDepth,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(source);

        var files = new List<ResourceVersionFile> { root };
        var edges = new List<DependencyEdge>();
        var unresolved = new List<string>();
        var skipped = new List<string>();
        // 以项目 ID 去重，根资源自身的 ID 也计入已访问
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(root.Id)) visited.Add(root.Id);

        var queue = new Queue<(ResourceVersionFile Version, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();
            var (version, depth) = queue.Dequeue();
            foreach (var dependencyId in version.Dependencies) {
                if (string.IsNullOrWhiteSpace(dependencyId)) continue;
                if (!visited.Add(dependencyId)) {
                    skipped.Add(dependencyId);
                    continue;
                }
                if (depth + 1 > maxDepth) {
                    skipped.Add(dependencyId);
                    continue;
                }
                IReadOnlyList<ResourceVersionFile> versions;
                try {
                    versions = await source
                        .GetVersionsAsync(dependencyId, version.Kind, minecraftVersion, loader, cancellationToken)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) when (ex is not OutOfMemoryException) {
                    Logger.Warn(ex, "获取依赖版本失败：" + dependencyId);
                    unresolved.Add(dependencyId);
                    continue;
                }
                var latest = ResourceVersionSelector.SelectLatest(versions, minecraftVersion, loader);
                if (latest is null) {
                    unresolved.Add(dependencyId);
                    continue;
                }
                files.Add(latest);
                edges.Add(new DependencyEdge(version.Id ?? version.FileName, latest.Id ?? latest.FileName));
                queue.Enqueue((latest, depth + 1));
            }
        }

        return new ResourceDependencyPlan {
            Files = files,
            Edges = edges,
            Unresolved = unresolved,
            Skipped = skipped
        };
    }

}

/// <summary>依赖图中的一条边：父资源指向其依赖。</summary>
public sealed record DependencyEdge(string FromId, string ToId);
