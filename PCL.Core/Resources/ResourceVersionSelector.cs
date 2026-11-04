namespace PCL.Core.Resources;

/// <summary>
/// 从版本列表中挑选可用版本，并判断本地是否落后于远程。
/// </summary>
public static class ResourceVersionSelector {

    /// <summary>
    /// 选出最新的可用版本：要求具备下载地址，匹配指定的游戏版本与加载器，然后按发布时间取最新。
    /// </summary>
    public static ResourceVersionFile? SelectLatest(
        IEnumerable<ResourceVersionFile> versions,
        string? minecraftVersion = null,
        string? loader = null) {
        ArgumentNullException.ThrowIfNull(versions);
        return versions
            .Where(version => version.DownloadAvailable)
            .Where(version => string.IsNullOrWhiteSpace(minecraftVersion)
                              || version.GameVersions.Any(game => string.Equals(game, minecraftVersion, StringComparison.OrdinalIgnoreCase)))
            .Where(version => string.IsNullOrWhiteSpace(loader)
                              || version.Loaders.Any(name => string.Equals(name, loader, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(version => version.ReleaseDate ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    /// <summary>
    /// 判断是否存在更新。两边都是标准版本号时按数字比较；
    /// 非标准版本号无法可靠比较，只要版本号不同就认为可能更新。
    /// </summary>
    public static bool IsUpdateAvailable(string? localVersion, string? remoteVersion) {
        if (string.IsNullOrWhiteSpace(remoteVersion)) return false;
        if (string.IsNullOrWhiteSpace(localVersion)) return true;
        var local = localVersion.Trim();
        var remote = remoteVersion.Trim();
        if (string.Equals(local, remote, StringComparison.OrdinalIgnoreCase)) return false;
        if (Version.TryParse(local, out var localParsed) && Version.TryParse(remote, out var remoteParsed)) {
            return remoteParsed > localParsed;
        }
        return true;
    }

}
