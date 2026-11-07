using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Mods;

/// <summary>配置档中的单个 Mod 条目：<see cref="FileName"/> 始终是带 .jar 的原始文件名，启用状态单独记录。</summary>
public sealed class ModProfileEntry {

    /// <summary>不带 .disabled 后缀的文件名，例如 <c>sodium.jar</c>。</summary>
    public string FileName { get; set; } = "";

    public string Sha256 { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public string ModId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "";

}

/// <summary>一套 Mod 组合（性能档、光影档、纯净档等）。</summary>
public sealed class ModProfile {

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public List<ModProfileEntry> Entries { get; set; } = [];

    /// <summary>启用的 Mod 数量。</summary>
    public int EnabledCount => Entries.Count(entry => entry.Enabled);

}

/// <summary>实例内的配置档集合，保存为 <c>PCL/ModProfiles.json</c>。</summary>
public sealed class ModProfileLibrary {

    public const string FormatName = "pcl-mod-profiles";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    public List<ModProfile> Profiles { get; set; } = [];

}

/// <summary>配置档与实例当前状态的差异。</summary>
public sealed class ModProfileDiff {

    /// <summary>配置档里没有、但实例里有的 Mod。</summary>
    public IReadOnlyList<string> Unlisted { get; init; } = [];

    /// <summary>配置档里有、但实例里缺少的 Mod。</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];

    /// <summary>当前禁用、应用后会启用的 Mod。</summary>
    public IReadOnlyList<string> WillEnable { get; init; } = [];

    /// <summary>当前启用、应用后会禁用的 Mod。</summary>
    public IReadOnlyList<string> WillDisable { get; init; } = [];

    public bool IsEmpty => Unlisted.Count == 0 && Missing.Count == 0 && WillEnable.Count == 0 && WillDisable.Count == 0;

    /// <summary>是否存在与当前状态不同的地方（缺少的 Mod 也算差异，但无法自动补齐）。</summary>
    public bool HasChanges => WillEnable.Count > 0 || WillDisable.Count > 0 || Missing.Count > 0;

    public string Summary {
        get {
            if (IsEmpty) return "与当前状态一致。";
            var parts = new List<string>();
            if (WillEnable.Count > 0) parts.Add($"启用 {WillEnable.Count} 个");
            if (WillDisable.Count > 0) parts.Add($"禁用 {WillDisable.Count} 个");
            if (Missing.Count > 0) parts.Add($"缺少 {Missing.Count} 个");
            if (Unlisted.Count > 0) parts.Add($"另有 {Unlisted.Count} 个不在配置档中");
            return string.Join("，", parts) + "。";
        }
    }

}

/// <summary>应用配置档的结果。</summary>
public sealed class ModProfileApplyResult {

    public int EnabledCount { get; init; }

    public int DisabledCount { get; init; }

    /// <summary>配置档里记录、但实例中没有的 Mod 文件名。</summary>
    public IReadOnlyList<string> Missing { get; init; } = [];

    /// <summary>实例中有、但配置档没有的 Mod 文件名（已按 <see cref="ModProfileService.Apply"/> 的规则处理）。</summary>
    public IReadOnlyList<string> Unlisted { get; init; } = [];

    /// <summary>改名失败的文件及原因。</summary>
    public IReadOnlyList<string> Failed { get; init; } = [];

    public string Summary {
        get {
            var parts = new List<string> { $"已启用 {EnabledCount} 个，禁用 {DisabledCount} 个" };
            if (Missing.Count > 0) parts.Add($"缺少 {Missing.Count} 个：{string.Join("、", Missing.Take(5))}");
            if (Unlisted.Count > 0) parts.Add($"未记录的 {Unlisted.Count} 个已保留原状态");
            if (Failed.Count > 0) parts.Add($"{Failed.Count} 个处理失败");
            return string.Join("；", parts) + "。";
        }
    }

}

/// <summary>
/// Mod 配置档：把实例的 mods 目录记成一套「启用/禁用」快照，之后可一键切换。
/// 只通过 .jar / .jar.disabled 后缀切换状态，不移动、不删除 Mod 文件。
/// </summary>
public static class ModProfileService {

    /// <summary>配置档相对实例目录的存储路径。</summary>
    public const string StoreRelativePath = "PCL/ModProfiles.json";

    private const string EnabledSuffix = ".jar";
    private const string DisabledSuffix = ".jar.disabled";

    public static string GetStorePath(string instanceFolder) =>
        Path.Combine(instanceFolder, "PCL", "ModProfiles.json");

    /// <summary>读取配置档集合；文件缺失或损坏时返回空集合，不抛异常。</summary>
    public static ModProfileLibrary Load(string instanceFolder) {
        var path = GetStorePath(instanceFolder);
        if (!File.Exists(path)) return new ModProfileLibrary();
        try {
            var json = JObject.Parse(File.ReadAllText(path));
            var library = new ModProfileLibrary {
                Format = json["format"]?.ToString() ?? ModProfileLibrary.FormatName,
                Version = json["version"]?.ToObject<int>() ?? ModProfileLibrary.CurrentVersion,
                Profiles = []
            };
            if (!string.Equals(library.Format, ModProfileLibrary.FormatName, StringComparison.OrdinalIgnoreCase)) return new ModProfileLibrary();
            if (json["profiles"] is JArray profiles) {
                foreach (var item in profiles.OfType<JObject>()) {
                    var profile = new ModProfile {
                        Id = item["id"]?.ToString() ?? Guid.NewGuid().ToString("N"),
                        Name = item["name"]?.ToString() ?? "",
                        Description = item["description"]?.ToString() ?? "",
                        CreatedUtc = ParseDate(item["createdUtc"]) ?? DateTime.UtcNow,
                        UpdatedUtc = ParseDate(item["updatedUtc"]) ?? DateTime.UtcNow
                    };
                    if (item["entries"] is JArray entries) {
                        foreach (var entry in entries.OfType<JObject>()) {
                            var fileName = NormalizeFileName(entry["fileName"]?.ToString() ?? "");
                            if (fileName.Length == 0) continue;
                            profile.Entries.Add(new ModProfileEntry {
                                FileName = fileName,
                                Sha256 = entry["sha256"]?.ToString() ?? "",
                                Enabled = entry["enabled"]?.ToObject<bool>() ?? true,
                                ModId = entry["modId"]?.ToString() ?? "",
                                Name = entry["name"]?.ToString() ?? "",
                                Version = entry["version"]?.ToString() ?? ""
                            });
                        }
                    }
                    library.Profiles.Add(profile);
                }
            }
            return library;
        } catch {
            return new ModProfileLibrary();
        }
    }

    /// <summary>写入配置档集合；先写临时文件再替换，避免写一半留下损坏 JSON。</summary>
    public static void Save(string instanceFolder, ModProfileLibrary library) {
        ArgumentNullException.ThrowIfNull(library);
        var path = GetStorePath(instanceFolder);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var profiles = new JArray();
        foreach (var profile in library.Profiles) {
            profiles.Add(new JObject {
                ["id"] = profile.Id,
                ["name"] = profile.Name,
                ["description"] = profile.Description,
                ["createdUtc"] = profile.CreatedUtc.ToString("O"),
                ["updatedUtc"] = profile.UpdatedUtc.ToString("O"),
                ["entries"] = new JArray(profile.Entries.Select(entry => new JObject {
                    ["fileName"] = entry.FileName,
                    ["sha256"] = entry.Sha256,
                    ["enabled"] = entry.Enabled,
                    ["modId"] = entry.ModId,
                    ["name"] = entry.Name,
                    ["version"] = entry.Version
                }))
            });
        }

        var json = new JObject {
            ["format"] = ModProfileLibrary.FormatName,
            ["version"] = ModProfileLibrary.CurrentVersion,
            ["profiles"] = profiles
        }.ToString(Newtonsoft.Json.Formatting.Indented);

        var temp = path + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Copy(temp, path, true);
        File.Delete(temp);
    }

    /// <summary>把实例当前的 mods 状态记录成配置档并保存；同名配置档会被更新而不是重复添加。</summary>
    public static ModProfile Capture(string instanceFolder, string name, string? description = null) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("配置档名称不能为空。", nameof(name));
        var profile = new ModProfile {
            Name = name.Trim(),
            Description = description?.Trim() ?? "",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            Entries = ReadEntries(instanceFolder)
        };

        var library = Load(instanceFolder);
        var existing = library.Profiles.FirstOrDefault(item => string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) {
            library.Profiles.Add(profile);
        } else {
            existing.Description = profile.Description;
            existing.Entries = profile.Entries;
            existing.UpdatedUtc = DateTime.UtcNow;
            profile = existing;
        }
        Save(instanceFolder, library);
        return profile;
    }

    /// <summary>
    /// 应用配置档：按条目切换 .jar / .jar.disabled。
    /// <paramref name="disableUnlisted"/> 为 true 时会禁用配置档里没有的 Mod（纯净档/光影档的常见期望）；
    /// 为 false 时这些 Mod 保持原状态。
    /// </summary>
    public static ModProfileApplyResult Apply(string instanceFolder, ModProfile profile, bool disableUnlisted = true) {
        ArgumentNullException.ThrowIfNull(profile);
        var modsFolder = Path.Combine(instanceFolder, "mods");
        var current = ReadState(modsFolder);
        var wanted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in profile.Entries) {
            if (string.IsNullOrWhiteSpace(entry.FileName)) continue;
            wanted[entry.FileName] = entry.Enabled;
        }

        var enabled = 0;
        var disabled = 0;
        var failed = new List<string>();
        var missing = new List<string>();

        foreach (var pair in wanted) {
            if (!current.TryGetValue(pair.Key, out var state)) {
                missing.Add(pair.Key);
                continue;
            }
            try {
                SetEnabled(modsFolder, pair.Key, state, pair.Value);
                if (pair.Value) enabled++; else disabled++;
            } catch (Exception ex) {
                failed.Add(pair.Key + "：" + ex.Message);
            }
        }

        var unlisted = new List<string>();
        foreach (var pair in current) {
            if (wanted.ContainsKey(pair.Key)) continue;
            unlisted.Add(pair.Key);
            if (!disableUnlisted) continue;
            try {
                SetEnabled(modsFolder, pair.Key, pair.Value, false);
                disabled++;
            } catch (Exception ex) {
                failed.Add(pair.Key + "：" + ex.Message);
            }
        }

        return new ModProfileApplyResult {
            EnabledCount = enabled,
            DisabledCount = disabled,
            Missing = missing,
            Unlisted = unlisted,
            Failed = failed
        };
    }

    /// <summary>比较配置档与实例当前状态，不修改任何文件。</summary>
    public static ModProfileDiff Diff(string instanceFolder, ModProfile profile) {
        ArgumentNullException.ThrowIfNull(profile);
        var current = ReadState(Path.Combine(instanceFolder, "mods"));
        var wanted = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in profile.Entries) {
            if (string.IsNullOrWhiteSpace(entry.FileName)) continue;
            wanted[entry.FileName] = entry.Enabled;
        }

        var unlisted = current.Keys.Where(key => !wanted.ContainsKey(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        var missing = wanted.Keys.Where(key => !current.ContainsKey(key)).OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
        var willEnable = new List<string>();
        var willDisable = new List<string>();
        foreach (var pair in wanted) {
            if (!current.TryGetValue(pair.Key, out var state)) continue;
            if (state == pair.Value) continue;
            if (pair.Value) willEnable.Add(pair.Key); else willDisable.Add(pair.Key);
        }

        return new ModProfileDiff {
            Unlisted = unlisted,
            Missing = missing,
            WillEnable = willEnable.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList(),
            WillDisable = willDisable.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    /// <summary>删除配置档；返回是否真的删掉了。</summary>
    public static bool Remove(string instanceFolder, string profileId) {
        var library = Load(instanceFolder);
        var removed = library.Profiles.RemoveAll(profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;
        Save(instanceFolder, library);
        return true;
    }

    /// <summary>重命名配置档；返回是否成功。</summary>
    public static bool Rename(string instanceFolder, string profileId, string newName) {
        if (string.IsNullOrWhiteSpace(newName)) return false;
        var library = Load(instanceFolder);
        var profile = library.Profiles.FirstOrDefault(item => string.Equals(item.Id, profileId, StringComparison.OrdinalIgnoreCase));
        if (profile is null) return false;
        if (library.Profiles.Any(item => !ReferenceEquals(item, profile)
                                         && string.Equals(item.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidOperationException("已存在同名配置档：" + newName.Trim());
        }
        profile.Name = newName.Trim();
        profile.UpdatedUtc = DateTime.UtcNow;
        Save(instanceFolder, library);
        return true;
    }

    /// <summary>导出单个配置档为独立 JSON，便于分享。</summary>
    public static void Export(string profilePath, ModProfile profile) {
        ArgumentNullException.ThrowIfNull(profile);
        var directory = Path.GetDirectoryName(Path.GetFullPath(profilePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var json = new JObject {
            ["format"] = ModProfileLibrary.FormatName,
            ["version"] = ModProfileLibrary.CurrentVersion,
            ["profiles"] = new JArray(new JObject {
                ["id"] = profile.Id,
                ["name"] = profile.Name,
                ["description"] = profile.Description,
                ["createdUtc"] = profile.CreatedUtc.ToString("O"),
                ["updatedUtc"] = profile.UpdatedUtc.ToString("O"),
                ["entries"] = new JArray(profile.Entries.Select(entry => new JObject {
                    ["fileName"] = entry.FileName,
                    ["sha256"] = entry.Sha256,
                    ["enabled"] = entry.Enabled,
                    ["modId"] = entry.ModId,
                    ["name"] = entry.Name,
                    ["version"] = entry.Version
                }))
            })
        };
        File.WriteAllText(profilePath, json.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
    }

    /// <summary>从导出的 JSON 中读取配置档，名称重复时自动加上序号。</summary>
    public static ModProfile Import(string instanceFolder, string profilePath, string? rename = null) {
        if (!File.Exists(profilePath)) throw new FileNotFoundException("配置档文件不存在。", profilePath);
        var json = JObject.Parse(File.ReadAllText(profilePath));
        if (json["profiles"] is not JArray profiles || profiles.Count == 0) {
            throw new InvalidDataException("配置档文件里没有可导入的配置。");
        }
        var source = profiles.OfType<JObject>().First();
        var profile = new ModProfile {
            Id = Guid.NewGuid().ToString("N"),
            Name = rename?.Trim() ?? source["name"]?.ToString() ?? "导入的配置档",
            Description = source["description"]?.ToString() ?? "",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
        if (source["entries"] is JArray entries) {
            foreach (var entry in entries.OfType<JObject>()) {
                var fileName = NormalizeFileName(entry["fileName"]?.ToString() ?? "");
                if (fileName.Length == 0) continue;
                profile.Entries.Add(new ModProfileEntry {
                    FileName = fileName,
                    Sha256 = entry["sha256"]?.ToString() ?? "",
                    Enabled = entry["enabled"]?.ToObject<bool>() ?? true,
                    ModId = entry["modId"]?.ToString() ?? "",
                    Name = entry["name"]?.ToString() ?? "",
                    Version = entry["version"]?.ToString() ?? ""
                });
            }
        }

        var library = Load(instanceFolder);
        var unique = profile.Name;
        var index = 2;
        while (library.Profiles.Any(item => string.Equals(item.Name, unique, StringComparison.OrdinalIgnoreCase))) {
            unique = $"{profile.Name} ({index++})";
        }
        profile.Name = unique;
        library.Profiles.Add(profile);
        Save(instanceFolder, library);
        return profile;
    }

    /// <summary>把文件系统状态读成「文件名 → 是否启用」；文件名统一为带 .jar 的形式。</summary>
    private static Dictionary<string, bool> ReadState(string modsFolder) {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(modsFolder)) return result;
        foreach (var file in Directory.EnumerateFiles(modsFolder, "*", SearchOption.TopDirectoryOnly)) {
            var name = Path.GetFileName(file);
            if (name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)) {
                result[NormalizeFileName(name)] = false;
            } else if (name.EndsWith(EnabledSuffix, StringComparison.OrdinalIgnoreCase)) {
                result[NormalizeFileName(name)] = true;
            }
        }
        return result;
    }

    private static List<ModProfileEntry> ReadEntries(string instanceFolder) {
        var modsFolder = Path.Combine(instanceFolder, "mods");
        var entries = new List<ModProfileEntry>();
        if (!Directory.Exists(modsFolder)) return entries;

        var metadata = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
        try {
            foreach (var mod in ModScanner.Scan(modsFolder).Mods) {
                metadata[NormalizeFileName(mod.FileName)] = mod;
            }
        } catch {
            // 元数据只是锦上添花，读不出来不影响配置档
        }

        foreach (var file in Directory.EnumerateFiles(modsFolder, "*", SearchOption.TopDirectoryOnly)) {
            var name = Path.GetFileName(file);
            var enabled = name.EndsWith(EnabledSuffix, StringComparison.OrdinalIgnoreCase);
            if (!enabled && !name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            var fileName = NormalizeFileName(name);
            metadata.TryGetValue(fileName, out var info);
            entries.Add(new ModProfileEntry {
                FileName = fileName,
                Sha256 = info?.Sha256 ?? ComputeSha256(file),
                Enabled = enabled,
                ModId = info?.ModId ?? "",
                Name = info?.Name ?? "",
                Version = info?.Version ?? ""
            });
        }

        return entries.OrderBy(entry => entry.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>按目标状态改名；已经一致时什么都不做。</summary>
    private static void SetEnabled(string modsFolder, string fileName, bool currentEnabled, bool wantedEnabled) {
        if (currentEnabled == wantedEnabled) return;
        var source = Path.Combine(modsFolder, currentEnabled ? fileName : fileName + ".disabled");
        if (!File.Exists(source)) throw new FileNotFoundException("找不到 Mod 文件：" + source);
        var target = Path.Combine(modsFolder, wantedEnabled ? fileName : fileName + ".disabled");
        if (File.Exists(target)) throw new IOException("目标文件已存在：" + Path.GetFileName(target));
        File.Move(source, target);
    }

    /// <summary>去掉 .disabled 后缀，得到规范文件名。</summary>
    private static string NormalizeFileName(string name) {
        name = (name ?? "").Trim();
        if (name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)) {
            return name.Substring(0, name.Length - ".disabled".Length);
        }
        return name;
    }

    private static string ComputeSha256(string path) {
        try {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        } catch {
            return "";
        }
    }

    private static DateTime? ParseDate(JToken? token) {
        if (token is null) return null;
        return DateTime.TryParse(token.ToString(), null,
            System.Globalization.DateTimeStyles.RoundtripKind, out var value)
            ? value.ToUniversalTime()
            : null;
    }

}
