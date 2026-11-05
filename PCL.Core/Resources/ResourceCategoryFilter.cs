namespace PCL.Core.Resources;

/// <summary>
/// 资源分类筛选。页面上的分类项形如 <c>406/worldgen</c>（CurseForge 数字 ID / Modrinth slug）。
/// </summary>
public sealed record ResourceCategoryFilter(int? CurseForgeId, string? ModrinthSlug) {

    public static readonly ResourceCategoryFilter Empty = new(null, null);

    public bool IsEmpty => CurseForgeId is null && string.IsNullOrEmpty(ModrinthSlug);

    /// <summary>
    /// 解析分类项。允许只给 ID、只给 slug 或两者都给；无法识别时返回空筛选。
    /// </summary>
    public static ResourceCategoryFilter Parse(string? tag) {
        if (string.IsNullOrWhiteSpace(tag)) return Empty;
        var parts = tag.Split('/', StringSplitOptions.TrimEntries);
        int? id = null;
        string? slug = null;
        foreach (var part in parts) {
            if (part.Length == 0) continue;
            if (id is null && int.TryParse(part, out var parsed)) {
                id = parsed;
            } else if (slug is null) {
                slug = part;
            }
        }
        return new ResourceCategoryFilter(id, slug);
    }

}
