using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Mods;

public enum ModIssueSeverity {
    Info,
    Warning,
    Error
}

public sealed class ModDependency {

    public string Id { get; init; } = "";
    public bool Mandatory { get; init; } = true;
    public string? VersionRange { get; init; }

}

public sealed record ModInfo {

    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string Loader { get; init; } = "unknown";
    public string ModId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string? Environment { get; init; }
    public string Sha256 { get; init; } = "";
    public IReadOnlyList<ModDependency> Dependencies { get; init; } = Array.Empty<ModDependency>();

}

public sealed class ModIssue {

    public ModIssueSeverity Severity { get; init; }
    public string FileName { get; init; } = "";
    public string Message { get; init; } = "";

}

public sealed class ModScanResult {

    public IReadOnlyList<ModInfo> Mods { get; init; } = Array.Empty<ModInfo>();
    public IReadOnlyList<ModIssue> Issues { get; init; } = Array.Empty<ModIssue>();
    public bool HasErrors => Issues.Any(issue => issue.Severity == ModIssueSeverity.Error);

}

/// <summary>
/// 只读取 Mod JAR 的元数据，不执行 JAR 内容。
/// </summary>
public static class ModScanner {

    private static readonly HashSet<string> BuiltinDependencies = new(StringComparer.OrdinalIgnoreCase) {
        "minecraft", "forge", "neoforge", "fabricloader", "fabric", "fabric-api", "java", "quilt_loader", "quilted_fabric_api"
    };

    public static ModScanResult Scan(string modsFolder) {
        var mods = new List<ModInfo>();
        var issues = new List<ModIssue>();
        if (!Directory.Exists(modsFolder)) return new ModScanResult { Mods = mods, Issues = issues };

        foreach (var file in Directory.EnumerateFiles(modsFolder, "*", SearchOption.TopDirectoryOnly)) {
            var name = Path.GetFileName(file);
            if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase)) continue;

            try {
                var mod = ReadMod(file);
                mods.Add(mod);
                if (string.IsNullOrWhiteSpace(mod.ModId)) {
                    issues.Add(new ModIssue {
                        Severity = ModIssueSeverity.Warning,
                        FileName = name,
                        Message = "未能从 JAR 中解析出 Mod ID。"
                    });
                }
            } catch (Exception ex) {
                issues.Add(new ModIssue {
                    Severity = ModIssueSeverity.Warning,
                    FileName = name,
                    Message = "无法读取 Mod 元数据：" + ex.Message
                });
            }
        }

        AddDuplicateIssues(mods, issues);
        AddMissingDependencyIssues(mods, issues);
        return new ModScanResult {
            Mods = mods.OrderBy(mod => mod.FileName, StringComparer.OrdinalIgnoreCase).ToList(),
            Issues = issues
        };
    }

    private static ModInfo ReadMod(string filePath) {
        var fileName = Path.GetFileName(filePath);
        var enabled = fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
        using var archive = ZipFile.OpenRead(filePath);
        var hash = ComputeSha256(filePath);

        var fabric = archive.Entries.FirstOrDefault(entry => entry.FullName.Equals("fabric.mod.json", StringComparison.OrdinalIgnoreCase));
        if (fabric is not null) {
            var json = JObject.Parse(ReadEntry(fabric));
            return new ModInfo {
                FilePath = filePath,
                FileName = fileName,
                Enabled = enabled,
                Loader = "fabric",
                ModId = json["id"]?.ToString() ?? "",
                Name = json["name"]?.ToString() ?? json["id"]?.ToString() ?? "",
                Version = json["version"]?.ToString() ?? "",
                Environment = json["environment"]?.ToString(),
                Sha256 = hash,
                Dependencies = ReadFabricDependencies(json)
            };
        }

        var quilt = archive.Entries.FirstOrDefault(entry => entry.FullName.Equals("quilt.mod.json", StringComparison.OrdinalIgnoreCase));
        if (quilt is not null) {
            var json = JObject.Parse(ReadEntry(quilt));
            var quiltLoader = json["quilt_loader"] as JObject;
            return new ModInfo {
                FilePath = filePath,
                FileName = fileName,
                Enabled = enabled,
                Loader = "quilt",
                ModId = quiltLoader?["id"]?.ToString() ?? json["id"]?.ToString() ?? "",
                Name = quiltLoader?["metadata"]?["name"]?.ToString() ?? quiltLoader?["id"]?.ToString() ?? "",
                Version = quiltLoader?["version"]?.ToString() ?? "",
                Environment = quiltLoader?["metadata"]?["environment"]?.ToString(),
                Sha256 = hash,
                Dependencies = ReadQuiltDependencies(quiltLoader)
            };
        }

        var toml = archive.Entries.FirstOrDefault(entry =>
            entry.FullName.Equals("META-INF/neoforge.mods.toml", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.Equals("META-INF/mods.toml", StringComparison.OrdinalIgnoreCase));
        if (toml is not null) {
            var info = ParseForgeToml(ReadEntry(toml), fileName, filePath, enabled, hash);
            return info with { Loader = toml.FullName.IndexOf("neoforge", StringComparison.OrdinalIgnoreCase) >= 0 ? "neoforge" : "forge" };
        }

        return new ModInfo {
            FilePath = filePath,
            FileName = fileName,
            Enabled = enabled,
            Sha256 = hash
        };
    }

    private static IReadOnlyList<ModDependency> ReadFabricDependencies(JObject json) {
        var result = new List<ModDependency>();
        if (json["depends"] is not JObject depends) return result;
        foreach (var property in depends.Properties()) {
            var range = property.Value switch {
                JArray array => string.Join(" || ", array.Select(item => item.ToString())),
                _ => property.Value.ToString()
            };
            result.Add(new ModDependency { Id = property.Name, Mandatory = true, VersionRange = range });
        }
        return result;
    }

    private static IReadOnlyList<ModDependency> ReadQuiltDependencies(JObject? loader) {
        var result = new List<ModDependency>();
        if (loader?["depends"] is not JArray depends) return result;
        foreach (var dependency in depends.OfType<JObject>()) {
            var id = dependency["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            result.Add(new ModDependency {
                Id = id!,
                Mandatory = dependency["optional"]?.ToObject<bool>() != true,
                VersionRange = dependency["versions"]?.ToString()
            });
        }
        return result;
    }

    private static ModInfo ParseForgeToml(string text, string fileName, string filePath, bool enabled, string hash) {
        var modId = "";
        var name = "";
        var version = "";
        var dependencies = new List<ModDependency>();
        var section = "";
        var inDependency = false;
        string? dependencyId = null;
        var mandatory = true;
        string? versionRange = null;

        void FlushDependency() {
            if (!inDependency || string.IsNullOrWhiteSpace(dependencyId)) return;
            dependencies.Add(new ModDependency { Id = dependencyId!, Mandatory = mandatory, VersionRange = versionRange });
            inDependency = false;
            dependencyId = null;
            mandatory = true;
            versionRange = null;
        }

        foreach (var rawLine in text.Split('\n')) {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            if (line.StartsWith("[[mods]]", StringComparison.OrdinalIgnoreCase)) {
                FlushDependency();
                section = "mods";
                continue;
            }
            if (line.StartsWith("[[dependencies.", StringComparison.OrdinalIgnoreCase)) {
                FlushDependency();
                section = "dependencies";
                inDependency = true;
                continue;
            }
            if (line.StartsWith("[[", StringComparison.Ordinal)) {
                FlushDependency();
                section = "other";
                continue;
            }

            var split = line.IndexOf('=');
            if (split < 0) continue;
            var key = line.Substring(0, split).Trim();
            var value = Unquote(line.Substring(split + 1).Trim());

            if (section == "mods") {
                switch (key.ToLowerInvariant()) {
                    case "modid": if (modId.Length == 0) modId = value; break;
                    case "displayname": if (name.Length == 0) name = value; break;
                    case "version": if (version.Length == 0) version = value; break;
                }
            } else if (section == "dependencies" && inDependency) {
                switch (key.ToLowerInvariant()) {
                    case "modid": dependencyId = value; break;
                    case "mandatory": mandatory = !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                    case "versionrange": versionRange = value; break;
                }
            }
        }
        FlushDependency();

        return new ModInfo {
            FilePath = filePath,
            FileName = fileName,
            Enabled = enabled,
            ModId = modId,
            Name = name,
            Version = version,
            Sha256 = hash,
            Dependencies = dependencies
        };
    }

    private static void AddDuplicateIssues(IReadOnlyList<ModInfo> mods, ICollection<ModIssue> issues) {
        foreach (var group in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.ModId))
                     .GroupBy(mod => mod.ModId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)) {
            issues.Add(new ModIssue {
                Severity = ModIssueSeverity.Error,
                FileName = string.Join(", ", group.Select(mod => mod.FileName)),
                Message = $"存在重复的 Mod ID：{group.Key}"
            });
        }

        foreach (var group in mods.Where(mod => !string.IsNullOrWhiteSpace(mod.Sha256))
                     .GroupBy(mod => mod.Sha256, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)) {
            issues.Add(new ModIssue {
                Severity = ModIssueSeverity.Warning,
                FileName = string.Join(", ", group.Select(mod => mod.FileName)),
                Message = "存在完全相同的 Mod 文件（哈希重复）。"
            });
        }
    }

    private static void AddMissingDependencyIssues(IReadOnlyList<ModInfo> mods, ICollection<ModIssue> issues) {
        var ids = new HashSet<string>(mods.Select(mod => mod.ModId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods) {
            foreach (var dependency in mod.Dependencies.Where(dep => dep.Mandatory)) {
                if (string.IsNullOrWhiteSpace(dependency.Id) || BuiltinDependencies.Contains(dependency.Id)) continue;
                if (ids.Contains(dependency.Id)) continue;
                issues.Add(new ModIssue {
                    Severity = ModIssueSeverity.Warning,
                    FileName = mod.FileName,
                    Message = $"缺少前置 Mod：{dependency.Id}"
                });
            }
        }
    }

    private static string ReadEntry(ZipArchiveEntry entry) {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string ComputeSha256(string path) {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static string Unquote(string value) {
        if (value.Length >= 2 && ((value[0] == '"' && value[value.Length - 1] == '"') || (value[0] == '\'' && value[value.Length - 1] == '\''))) {
            return value.Substring(1, value.Length - 2);
        }
        return value;
    }

}
