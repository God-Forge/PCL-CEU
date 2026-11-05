using PCL.Core.Resources;

namespace PCL.Core.Mods;

/// <summary>单个 Mod 的更新检查结果。</summary>
public sealed class ModUpdateResult {

    /// <summary>本地 Mod 的标识。</summary>
    public string ModId { get; init; } = "";
    public string LocalVersion { get; init; } = "";
    public string? LatestVersion { get; init; }
    public string? ProjectSlug { get; init; }
    public bool HasUpdate { get; init; }

    /// <summary>检查失败的原因，成功时为 <c>null</c>。</summary>
    public string? Error { get; init; }

}

/// <summary>
/// Mod 更新检查：用本地 Mod ID 在 Modrinth 搜索对应项目，再取最新兼容版本比较。
/// 匹配依赖平台搜索，属于「尽力而为」，找不到项目时给出明确原因而不是静默失败。
/// </summary>
public static class ModUpdateChecker {

    /// <summary>
    /// 批量检查 Mod 更新。单个 Mod 的失败不会中断整批。
    /// </summary>
    public static async Task<IReadOnlyList<ModUpdateResult>> CheckAsync(
        IEnumerable<ModInfo> mods,
        ModrinthResourceService service,
        string? minecraftVersion = null,
        string? loader = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(service);
        var results = new List<ModUpdateResult>();
        foreach (var mod in mods) {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await CheckOneAsync(mod, service, minecraftVersion, loader, cancellationToken).ConfigureAwait(false));
        }
        return results;
    }

    private static async Task<ModUpdateResult> CheckOneAsync(
        ModInfo mod,
        ModrinthResourceService service,
        string? minecraftVersion,
        string? loader,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(mod.ModId)) {
            return new ModUpdateResult { ModId = "", LocalVersion = mod.Version, Error = "缺少 Mod ID，无法匹配项目" };
        }
        try {
            var search = await service.SearchAsync(new ResourceSearchQuery {
                Text = mod.ModId,
                Kind = ResourceKind.Mod,
                Limit = 1
            }, cancellationToken).ConfigureAwait(false);
            var project = search.Projects.FirstOrDefault();
            if (project is null) {
                return new ModUpdateResult {
                    ModId = mod.ModId,
                    LocalVersion = mod.Version,
                    Error = "未在 Modrinth 找到对应项目"
                };
            }
            var versions = await service
                .GetVersionsAsync(project.Id, ResourceKind.Mod, minecraftVersion, loader, cancellationToken)
                .ConfigureAwait(false);
            var latest = ResourceVersionSelector.SelectLatest(versions, minecraftVersion, loader);
            return new ModUpdateResult {
                ModId = mod.ModId,
                LocalVersion = mod.Version,
                LatestVersion = latest?.Version,
                ProjectSlug = project.Slug,
                HasUpdate = latest is not null && ResourceVersionSelector.IsUpdateAvailable(mod.Version, latest.Version)
            };
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return new ModUpdateResult {
                ModId = mod.ModId,
                LocalVersion = mod.Version,
                Error = ex.Message
            };
        }
    }

}
