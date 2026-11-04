using Newtonsoft.Json.Linq;
using PCL.Core.Mods;

namespace PCL.Core.Resources;

/// <summary>
/// 本地资源文件（通常是 mods 目录下的 jar）及其与远程项目的关联。
/// </summary>
public sealed class LocalResourceEntry {

    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Loader { get; init; } = "unknown";
    public string ModId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Sha256 { get; init; } = "";

    /// <summary>关联到的远程来源，未关联时为 <c>null</c>。</summary>
    public ResourceSource? Source { get; init; }
    public string ProjectId { get; init; } = "";
    public string ProjectSlug { get; init; } = "";
    public DateTimeOffset? LastChecked { get; init; }

    /// <summary>去掉 .disabled 后缀后的名字，用于识别同一资源的启用/禁用两份文件。</summary>
    public string EnabledName => FileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
        ? FileName[..^".disabled".Length]
        : FileName;

    /// <summary>是否已经关联到远程项目。</summary>
    public bool HasProject => Source is not null && !string.IsNullOrEmpty(ProjectId);

    public LocalResourceEntry WithProject(ResourceSource source, string projectId, string projectSlug) => new() {
        FilePath = FilePath,
        FileName = FileName,
        Enabled = Enabled,
        Loader = Loader,
        ModId = ModId,
        Name = Name,
        Version = Version,
        Sha256 = Sha256,
        Source = source,
        ProjectId = projectId,
        ProjectSlug = projectSlug,
        LastChecked = DateTimeOffset.Now
    };

}

/// <summary>
/// 本地资源索引，可持久化到 JSON。由 VB 版 <c>LocalResourceFile</c> 的缓存读写迁移而来。
/// </summary>
public sealed class LocalResourceIndex {

    /// <summary>缓存结构版本，不匹配时整份弃用。</summary>
    public const int SchemaVersion = 1;

    private readonly Dictionary<string, LocalResourceEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<LocalResourceEntry> Entries => _entries.Values
        .OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public int Count => _entries.Count;

    /// <summary>读取索引。文件缺失、损坏或版本不符时返回空索引。</summary>
    public static LocalResourceIndex Load(string path) {
        var index = new LocalResourceIndex();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return index;
        try {
            var json = JObject.Parse(File.ReadAllText(path));
            if (json["version"]?.ToObject<int>() != SchemaVersion) {
                Logger.Warn("本地资源索引版本不符，已弃用");
                return index;
            }
            foreach (var item in json["entries"] as JArray ?? []) {
                if (item is not JObject data) continue;
                var entry = FromJson(data);
                if (entry is not null) index._entries[entry.FilePath] = entry;
            }
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取本地资源索引失败，已重置");
        }
        return index;
    }

    /// <summary>原子写入索引：先写临时文件再替换。</summary>
    public void Save(string path) {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("索引路径不能为空", nameof(path));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var json = new JObject {
            ["version"] = SchemaVersion,
            ["entries"] = new JArray(Entries.Select(ToJson))
        };
        var temp = path + ".tmp";
        File.WriteAllText(temp, json.ToString());
        File.Move(temp, path, overwrite: true);
    }

    public LocalResourceEntry? Find(string filePath) =>
        _entries.TryGetValue(filePath, out var entry) ? entry : null;

    public void Upsert(LocalResourceEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.FilePath] = entry;
    }

    /// <summary>移除磁盘上已不存在的文件记录。</summary>
    public void RemoveMissing(ISet<string> existingPaths) {
        foreach (var key in _entries.Keys.ToList()) {
            if (!existingPaths.Contains(key)) _entries.Remove(key);
        }
    }

    private static JObject ToJson(LocalResourceEntry entry) => new() {
        ["path"] = entry.FilePath,
        ["name"] = entry.FileName,
        ["enabled"] = entry.Enabled,
        ["loader"] = entry.Loader,
        ["modId"] = entry.ModId,
        ["title"] = entry.Name,
        ["version"] = entry.Version,
        ["sha256"] = entry.Sha256,
        ["source"] = entry.Source?.ToString() ?? "",
        ["projectId"] = entry.ProjectId,
        ["projectSlug"] = entry.ProjectSlug,
        ["checkedAt"] = entry.LastChecked?.ToString("O") ?? ""
    };

    private static LocalResourceEntry? FromJson(JObject data) {
        var path = data["path"]?.ToString();
        if (string.IsNullOrWhiteSpace(path)) return null;
        return new LocalResourceEntry {
            FilePath = path,
            FileName = data["name"]?.ToString() ?? Path.GetFileName(path),
            Enabled = data["enabled"]?.ToObject<bool>() ?? true,
            Loader = data["loader"]?.ToString() ?? "unknown",
            ModId = data["modId"]?.ToString() ?? "",
            Name = data["title"]?.ToString() ?? "",
            Version = data["version"]?.ToString() ?? "",
            Sha256 = data["sha256"]?.ToString() ?? "",
            Source = Enum.TryParse<ResourceSource>(data["source"]?.ToString(), out var source) ? source : null,
            ProjectId = data["projectId"]?.ToString() ?? "",
            ProjectSlug = data["projectSlug"]?.ToString() ?? "",
            LastChecked = DateTimeOffset.TryParse(data["checkedAt"]?.ToString(), out var checkedAt) ? checkedAt : null
        };
    }

}

/// <summary>
/// 把扫描结果转换为本地资源条目，并按 VB 的规则去重：
/// 同名文件同时存在启用与禁用两份时，只保留启用的那一份。
/// </summary>
public static class LocalResourceIndexBuilder {

    public static IReadOnlyList<LocalResourceEntry> FromScan(ModScanResult scan) {
        ArgumentNullException.ThrowIfNull(scan);
        var entries = scan.Mods.Select(mod => new LocalResourceEntry {
            FilePath = mod.FilePath,
            FileName = mod.FileName,
            Enabled = mod.Enabled,
            Loader = mod.Loader,
            ModId = mod.ModId,
            Name = mod.Name,
            Version = mod.Version,
            Sha256 = mod.Sha256
        });
        return Deduplicate(entries);
    }

    /// <summary>按 <see cref="LocalResourceEntry.EnabledName"/> 去重，启用优先。</summary>
    public static IReadOnlyList<LocalResourceEntry> Deduplicate(IEnumerable<LocalResourceEntry> entries) {
        ArgumentNullException.ThrowIfNull(entries);
        var result = new List<LocalResourceEntry>();
        foreach (var group in entries.GroupBy(entry => entry.EnabledName, StringComparer.OrdinalIgnoreCase)) {
            var preferred = group.FirstOrDefault(entry => entry.Enabled) ?? group.First();
            result.Add(preferred);
        }
        return result.OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

}
