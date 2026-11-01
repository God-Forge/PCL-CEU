using PCL.Core.Mods;

namespace PCL.Core.Minecraft;

public sealed class DiskUsageReport {

    public long Total { get; init; }
    public IReadOnlyDictionary<string, long> Categories { get; init; } = new Dictionary<string, long>();

}

public sealed class WorldInfo {

    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public DateTime LastWrite { get; init; }

}

public sealed class PreflightReport {

    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Passed { get; init; } = Array.Empty<string>();
    public bool HasErrors => Issues.Count > 0;

}

public sealed class TimingStats {

    public int Count { get; init; }
    public long Average { get; init; }
    public long Minimum { get; init; }
    public long Maximum { get; init; }

}

/// <summary>
/// 实例高级体检：磁盘占用、存档、启动前检查、启动耗时、Mod 依赖图与资源包冲突。
/// </summary>
public static class AdvancedAuditService {

    private static readonly string[] DiskCategories = {
        "mods", "resourcepacks", "shaderpacks", "saves", "logs", "crash-reports", "config", "libraries"
    };

    public static DiskUsageReport AnalyzeDiskUsage(string instanceFolder) {
        var categories = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var category in DiskCategories) {
            var folder = Path.Combine(instanceFolder, category);
            var size = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length)
                : 0;
            categories[category] = size;
            total += size;
        }
        foreach (var file in Directory.EnumerateFiles(instanceFolder, "*", SearchOption.TopDirectoryOnly)) {
            var size = new FileInfo(file).Length;
            categories["根目录文件"] = categories.TryGetValue("根目录文件", out var existing) ? existing + size : size;
            total += size;
        }
        return new DiskUsageReport { Total = total, Categories = categories };
    }

    public static IReadOnlyList<WorldInfo> ListWorlds(string instanceFolder) {
        var saves = Path.Combine(instanceFolder, "saves");
        if (!Directory.Exists(saves)) return Array.Empty<WorldInfo>();
        return Directory.EnumerateDirectories(saves)
            .Select(folder => {
                var size = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
                var level = Path.Combine(folder, "level.dat");
                var lastWrite = File.Exists(level) ? File.GetLastWriteTime(level) : Directory.GetLastWriteTime(folder);
                return new WorldInfo {
                    Name = Path.GetFileName(folder),
                    Path = folder,
                    Size = size,
                    LastWrite = lastWrite
                };
            })
            .OrderByDescending(world => world.LastWrite)
            .ToList();
    }

    public static PreflightReport Preflight(string instanceFolder, string mcFolder) {
        var issues = new List<string>();
        var passed = new List<string>();
        if (!Directory.Exists(instanceFolder)) {
            return new PreflightReport { Issues = ["实例文件夹不存在。"] };
        }
        var json = Directory.EnumerateFiles(instanceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (json is null) issues.Add("缺少版本 JSON。"); else passed.Add("版本 JSON 存在");

        if (instanceFolder.Any(character => character > 127)) issues.Add("实例路径包含非 ASCII 字符。");
        else passed.Add("路径为 ASCII");

        try {
            var root = Path.GetPathRoot(Path.GetFullPath(mcFolder));
            if (!string.IsNullOrEmpty(root)) {
                var drive = new DriveInfo(root);
                if (drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024) issues.Add("磁盘剩余空间不足 2 GB。");
                else passed.Add("磁盘空间充足");
            }
        } catch {
        }

        var check = InstanceCheckService.Check(instanceFolder, mcFolder);
        issues.AddRange(check.Issues.Where(issue => issue.Severity != InstanceCheckSeverity.Info).Select(issue => issue.Message));
        issues.AddRange(check.ModIssues.Where(issue => issue.Severity == ModIssueSeverity.Error).Select(issue => issue.Message));
        return new PreflightReport { Issues = issues, Passed = passed };
    }

    public static TimingStats AnalyzeTiming(IEnumerable<LaunchTimingRecord> records) {
        var values = records.Select(record => record.TotalMilliseconds).ToList();
        if (values.Count == 0) return new TimingStats();
        return new TimingStats {
            Count = values.Count,
            Average = (long)values.Average(),
            Minimum = values.Min(),
            Maximum = values.Max()
        };
    }

    public static string BuildModGraph(IReadOnlyList<ModInfo> mods) {
        if (mods.Count == 0) return "未找到 Mod。";
        var ids = new HashSet<string>(mods.Select(mod => mod.ModId).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var mod in mods.OrderBy(mod => mod.FileName, StringComparer.OrdinalIgnoreCase)) {
            var deps = mod.Dependencies.Where(dep => !string.IsNullOrWhiteSpace(dep.Id)).ToList();
            var dependencyText = deps.Count == 0
                ? "无前置"
                : string.Join(", ", deps.Select(dep => dep.Id + (ids.Contains(dep.Id) ? "" : "（缺失）")));
            lines.Add($"{mod.Name}（{mod.FileName}）→ {dependencyText}");
        }
        return string.Join("\r\n", lines);
    }

    public static string FormatReport(string instanceFolder, string mcFolder, string timingPath) {
        var disk = AnalyzeDiskUsage(instanceFolder);
        var worlds = ListWorlds(instanceFolder);
        var preflight = Preflight(instanceFolder, mcFolder);
        var timing = AnalyzeTiming(LaunchTimingService.List(timingPath));
        var scan = ModScanner.Scan(Path.Combine(instanceFolder, "mods"));
        var resources = ResourcePackService.Scan(instanceFolder);

        var lines = new List<string> {
            "=== 实例高级体检 ===",
            $"磁盘占用：{FormatSize(disk.Total)}",
            string.Join("，", disk.Categories.Where(pair => pair.Value > 0).Select(pair => $"{pair.Key} {FormatSize(pair.Value)}")),
            "",
            $"存档：{worlds.Count} 个",
            string.Join("\r\n", worlds.Take(8).Select(world => $"- {world.Name}：{FormatSize(world.Size)}，最后游玩 {world.LastWrite:yyyy-MM-dd HH:mm}")),
            "",
            "启动前检查：" + (preflight.Issues.Count == 0 ? "通过" : string.Join("；", preflight.Issues)),
            "",
            $"启动耗时：{timing.Count} 次，平均 {timing.Average}ms，最快 {timing.Minimum}ms，最慢 {timing.Maximum}ms",
            "",
            $"Mod：{scan.Mods.Count} 个，问题 {scan.Issues.Count} 条",
            BuildModGraph(scan.Mods),
            "",
            $"资源包/光影：{resources.Packs.Count} 个，问题 {resources.Issues.Count} 条"
        };
        return string.Join("\r\n", lines);
    }

    public static string FormatSize(long bytes) {
        if (bytes >= 1024L * 1024 * 1024) return (bytes / 1024d / 1024 / 1024).ToString("0.00") + " GB";
        if (bytes >= 1024L * 1024) return (bytes / 1024d / 1024).ToString("0.0") + " MB";
        if (bytes >= 1024) return (bytes / 1024d).ToString("0.0") + " KB";
        return bytes + " B";
    }

}
