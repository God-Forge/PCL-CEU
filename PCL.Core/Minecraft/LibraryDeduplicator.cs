using PCLCS;

namespace PCL.Core.Minecraft;

/// <summary>
/// 按支持库名称与 Natives 标记去重，并在版本不同时保留较新的条目。
/// </summary>
public static class LibraryDeduplicator {

    public static List<ResolvedLibrary> Deduplicate(
        IEnumerable<ResolvedLibrary> libraries,
        bool keepSameNameDifferentVersion,
        Action<string>? log = null) {
        var result = new Dictionary<string, ResolvedLibrary>();
        foreach (var library in libraries) {
            var name = library.Name;
            if (library.IsNative && name.Contains(":")) {
                name = name.Substring(0, name.LastIndexOf(':'));
            }
            var key = name + library.IsNative;
            var version = GetVersion(library.LocalPath);
            if (result.TryGetValue(key, out var existing)) {
                var existingVersion = GetVersion(existing.LocalPath);
                if (version != existingVersion && keepSameNameDifferentVersion) {
                    log?.Invoke($"发现疑似重复的支持库：{library.Name} ({version}) 与 {existing.Name} ({existingVersion})");
                    result.Add(key + Guid.NewGuid(), library);
                } else {
                    log?.Invoke($"发现重复的支持库：{library.Name} ({version}) 与 {existing.Name} ({existingVersion})，已忽略其中之一");
                    if (McUtils.CompareVersionGE(version, existingVersion)) {
                        result[key] = library;
                    }
                }
            } else {
                result.Add(key, library);
            }
        }
        return result.Values.ToList();
    }

    private static string GetVersion(string localPath) {
        var normalized = localPath.Replace('/', Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(normalized);
        return parent is null ? "" : Path.GetFileName(parent);
    }

}
