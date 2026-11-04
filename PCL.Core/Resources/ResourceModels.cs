namespace PCL.Core.Resources;

/// <summary>资源来源。</summary>
public enum ResourceSource {
    Modrinth,
    CurseForge
}

/// <summary>资源类型，对应下载页的分类。</summary>
public enum ResourceKind {
    Mod,
    Modpack,
    ResourcePack,
    ShaderPack,
    DataPack
}

/// <summary>
/// 统一的资源项目模型，屏蔽 Modrinth 与 CurseForge 的字段差异。
/// </summary>
public sealed class ResourceProject {

    public ResourceSource Source { get; init; }
    public ResourceKind Kind { get; init; }

    /// <summary>来源站点的项目标识。</summary>
    public string Id { get; init; } = "";

    /// <summary>项目短名（Modrinth 的 slug 或 CurseForge 的 slug）。</summary>
    public string Slug { get; init; } = "";

    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Author { get; init; } = "";
    public string IconUrl { get; init; } = "";

    public long Downloads { get; init; }
    public long Follows { get; init; }

    public IReadOnlyList<string> Categories { get; init; } = [];
    public IReadOnlyList<string> Loaders { get; init; } = [];
    public IReadOnlyList<string> Versions { get; init; } = [];

    /// <summary>项目主页地址。</summary>
    public string ProjectUrl { get; init; } = "";

    /// <summary>用于去重与缓存的唯一键。</summary>
    public string Key => $"{Source}:{Id}";

}

/// <summary>
/// 一次搜索请求。
/// </summary>
public sealed class ResourceSearchQuery {

    public string Text { get; init; } = "";
    public ResourceKind Kind { get; init; } = ResourceKind.Mod;

    /// <summary>按 Minecraft 版本过滤，为空则不过滤。</summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>按加载器过滤（fabric/forge/neoforge/quilt），为空则不过滤。</summary>
    public string? Loader { get; init; }

    public int Offset { get; init; }
    public int Limit { get; init; } = 20;

    /// <summary>参与本次搜索的来源，为空表示全部启用。</summary>
    public IReadOnlyList<ResourceSource>? Sources { get; init; }

}

/// <summary>
/// 搜索汇总结果。单个来源失败不会让整体失败，失败原因记录在 <see cref="Failures"/>。
/// </summary>
public sealed class ResourceSearchResult {

    public IReadOnlyList<ResourceProject> Projects { get; init; } = [];
    public int TotalHits { get; init; }
    public IReadOnlyList<string> Failures { get; init; } = [];

    public bool HasFailure => Failures.Count > 0;

}

/// <summary>
/// 单个资源来源的搜索能力。
/// </summary>
public interface IResourceSearchProvider {

    ResourceSource Source { get; }

    Task<ResourceSearchResult> SearchAsync(ResourceSearchQuery query, CancellationToken cancellationToken = default);

}
