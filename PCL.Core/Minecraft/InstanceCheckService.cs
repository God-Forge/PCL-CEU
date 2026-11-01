using Newtonsoft.Json.Linq;
using PCL.Core.Mods;

namespace PCL.Core.Minecraft;

public enum InstanceCheckSeverity {
    Info,
    Warning,
    Error
}

public sealed class InstanceCheckIssue {

    public InstanceCheckSeverity Severity { get; init; }
    public string Message { get; init; } = "";
    public string? Path { get; init; }

}

public sealed class InstanceCheckResult {

    public IReadOnlyList<InstanceCheckIssue> Issues { get; init; } = Array.Empty<InstanceCheckIssue>();
    public IReadOnlyList<ModInfo> Mods { get; init; } = Array.Empty<ModInfo>();
    public IReadOnlyList<ModIssue> ModIssues { get; init; } = Array.Empty<ModIssue>();
    public bool HasErrors => Issues.Any(issue => issue.Severity == InstanceCheckSeverity.Error) || ModIssues.Any(issue => issue.Severity == ModIssueSeverity.Error);

}

/// <summary>
/// 只读检查实例的 JSON、客户端 JAR、libraries、assets 与 Mod；不修改文件。
/// </summary>
public static class InstanceCheckService {

    public static InstanceCheckResult Check(string instanceFolder, string mcFolder) {
        var issues = new List<InstanceCheckIssue>();
        var mods = new List<ModInfo>();
        var modIssues = new List<ModIssue>();

        if (!Directory.Exists(instanceFolder)) {
            return new InstanceCheckResult {
                Issues = [new InstanceCheckIssue { Severity = InstanceCheckSeverity.Error, Message = "实例文件夹不存在。", Path = instanceFolder }]
            };
        }

        if (instanceFolder.Any(character => character > 127)) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Info,
                Message = "实例路径包含非 ASCII 字符，部分旧版本或 Natives 可能无法正常加载。",
                Path = instanceFolder
            });
        }
        try {
            var root = Path.GetPathRoot(Path.GetFullPath(mcFolder));
            if (!string.IsNullOrEmpty(root)) {
                var drive = new DriveInfo(root);
                if (drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024) {
                    issues.Add(new InstanceCheckIssue {
                        Severity = InstanceCheckSeverity.Warning,
                        Message = $"磁盘剩余空间不足 2 GB（当前 {drive.AvailableFreeSpace / 1024 / 1024} MB），下载或更新可能失败。",
                        Path = root
                    });
                }
            }
        } catch {
        }

        var folderName = Path.GetFileName(instanceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var jsonPath = Path.Combine(instanceFolder, folderName + ".json");
        if (!File.Exists(jsonPath)) {
            var fallback = Directory.EnumerateFiles(instanceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (fallback is not null) {
                jsonPath = fallback;
                issues.Add(new InstanceCheckIssue {
                    Severity = InstanceCheckSeverity.Warning,
                    Message = $"版本 JSON 文件名与实例文件夹名不一致，已使用 {Path.GetFileName(fallback)}。",
                    Path = fallback
                });
            } else {
                return new InstanceCheckResult {
                    Issues = [new InstanceCheckIssue { Severity = InstanceCheckSeverity.Error, Message = "缺少版本 JSON。", Path = instanceFolder }]
                };
            }
        }

        JObject json;
        try {
            json = JObject.Parse(File.ReadAllText(jsonPath));
        } catch (Exception ex) {
            return new InstanceCheckResult {
                Issues = [new InstanceCheckIssue { Severity = InstanceCheckSeverity.Error, Message = "版本 JSON 解析失败：" + ex.Message, Path = jsonPath }]
            };
        }

        var versionId = json["id"]?.ToString() ?? folderName;
        if (!string.Equals(versionId, folderName, StringComparison.OrdinalIgnoreCase)) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Warning,
                Message = $"版本 JSON 的 id（{versionId}）与文件夹名（{folderName}）不一致。",
                Path = jsonPath
            });
        }

        var inheritsFrom = json["inheritsFrom"]?.ToString();
        var jarCandidates = new List<string> {
            Path.Combine(instanceFolder, versionId + ".jar"),
            Path.Combine(instanceFolder, folderName + ".jar")
        };
        if (!string.IsNullOrWhiteSpace(inheritsFrom)) {
            jarCandidates.Add(Path.Combine(mcFolder, "versions", inheritsFrom!, inheritsFrom + ".jar"));
        }
        if (!jarCandidates.Any(File.Exists)) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Warning,
                Message = "未找到客户端 JAR（可能是继承版本或需要修复）。",
                Path = instanceFolder
            });
        }

        CheckLibraries(json, mcFolder, issues);
        CheckAssets(json, mcFolder, issues);

        if (json["mainClass"] is null) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Warning,
                Message = "版本 JSON 缺少 mainClass。",
                Path = jsonPath
            });
        }

        var modsFolder = Path.Combine(instanceFolder, "mods");
        if (Directory.Exists(modsFolder)) {
            var scan = ModScanner.Scan(modsFolder);
            mods.AddRange(scan.Mods);
            modIssues.AddRange(scan.Issues);
        }

        return new InstanceCheckResult {
            Issues = issues,
            Mods = mods,
            ModIssues = modIssues
        };
    }

    private static void CheckLibraries(JObject json, string mcFolder, ICollection<InstanceCheckIssue> issues) {
        var libraries = LibraryResolver.Resolve(json, mcFolder);
        var missing = libraries
            .Where(library => !File.Exists(Path.Combine(mcFolder, library.LocalPath)))
            .Select(library => library.LocalPath)
            .ToList();
        if (missing.Count == 0) return;
        issues.Add(new InstanceCheckIssue {
            Severity = InstanceCheckSeverity.Warning,
            Message = $"缺少 {missing.Count} 个 libraries 文件，例如：{string.Join("，", missing.Take(3))}",
            Path = Path.Combine(mcFolder, "libraries")
        });
    }

    private static void CheckAssets(JObject json, string mcFolder, ICollection<InstanceCheckIssue> issues) {
        var assetIndex = json["assetIndex"]?["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(assetIndex)) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Warning,
                Message = "版本 JSON 缺少 assetIndex。"
            });
            return;
        }

        var indexPath = Path.Combine(mcFolder, "assets", "indexes", assetIndex + ".json");
        if (!File.Exists(indexPath)) {
            issues.Add(new InstanceCheckIssue {
                Severity = InstanceCheckSeverity.Warning,
                Message = $"缺少资源索引：{assetIndex}.json",
                Path = indexPath
            });
        }
    }

}
