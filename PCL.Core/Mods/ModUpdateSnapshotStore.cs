using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Mods;

/// <summary>快照中的一条记录：一次更新里被替换的某个文件。</summary>
public sealed class ModUpdateSnapshotEntry {

    public string ModId { get; init; } = "";

    /// <summary>更新前的文件名（回滚时恢复到这里）。</summary>
    public string OldFileName { get; init; } = "";

    /// <summary>更新后写入的文件名（回滚时删除）。</summary>
    public string NewFileName { get; init; } = "";

    /// <summary>旧文件的备份位置。</summary>
    public string BackupPath { get; init; } = "";

}

/// <summary>一次批量更新的快照。</summary>
public sealed class ModUpdateSnapshot {

    public string Id { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ModUpdateSnapshotEntry> Entries { get; init; } = [];

    public string Describe() =>
        $"{CreatedAt:yyyy-MM-dd HH:mm} 更新了 {Entries.Count} 个 Mod";

}

/// <summary>回滚结果。</summary>
public sealed class ModRollbackResult {

    public bool Success { get; init; }
    public int Restored { get; init; }
    public IReadOnlyList<string> Failures { get; init; } = [];
    public string Message { get; init; } = "";

}

/// <summary>
/// 更新快照的存取与回滚。快照保存在 mods\.backup\snapshots.json，
/// 让「一次批量更新」可以整体撤销，而不是逐个文件手工恢复。
/// </summary>
public static class ModUpdateSnapshotStore {

    private const string IndexFileName = "snapshots.json";

    /// <summary>最多保留的快照数量，超出后丢弃最旧的。</summary>
    public const int MaxSnapshots = 10;

    public static string ResolvePath(string modsFolder) =>
        Path.Combine(modsFolder, ModUpdateApplier.BackupFolderName, IndexFileName);

    /// <summary>读取全部快照，最新的在前；文件缺失或损坏时返回空。</summary>
    public static IReadOnlyList<ModUpdateSnapshot> List(string modsFolder) {
        var path = ResolvePath(modsFolder);
        if (!File.Exists(path)) return [];
        try {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return [];
            var array = JArray.Parse(text);
            return array
                .OfType<JObject>()
                .Select(Parse)
                .Where(snapshot => snapshot is not null)
                .Select(snapshot => snapshot!)
                .OrderByDescending(snapshot => snapshot.CreatedAt)
                .ToList();
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取更新快照失败");
            return [];
        }
    }

    /// <summary>
    /// 记录一次批量更新。只写磁盘上确实存在的备份，避免产生无法回滚的条目。
    /// 返回新快照；没有任何有效条目时返回 <c>null</c>。
    /// </summary>
    public static ModUpdateSnapshot? Record(string modsFolder, IEnumerable<ModUpdateSnapshotEntry>? entries) {
        if (entries is null) return null;
        var valid = entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.BackupPath) && File.Exists(entry.BackupPath))
            .ToList();
        if (valid.Count == 0) return null;
        var snapshot = new ModUpdateSnapshot {
            Id = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"),
            CreatedAt = DateTime.Now,
            Entries = valid
        };
        var all = new List<ModUpdateSnapshot>(List(modsFolder)) { snapshot };
        Save(modsFolder, all.Take(MaxSnapshots).ToList());
        Logger.Info($"已记录 Mod 更新快照：{snapshot.Describe()}");
        return snapshot;
    }

    /// <summary>回滚指定快照：删除新文件、把备份还原回原文件名。</summary>
    public static ModRollbackResult Rollback(string modsFolder, string? snapshotId = null) {
        var snapshots = List(modsFolder);
        var target = string.IsNullOrWhiteSpace(snapshotId)
            ? snapshots.FirstOrDefault()
            : snapshots.FirstOrDefault(snapshot =>
                string.Equals(snapshot.Id, snapshotId, StringComparison.OrdinalIgnoreCase));
        if (target is null) {
            return new ModRollbackResult { Success = false, Message = "没有可回滚的更新记录。" };
        }
        var restored = 0;
        var failures = new List<string>();
        foreach (var entry in target.Entries) {
            try {
                var newPath = Path.Combine(modsFolder, entry.NewFileName);
                if (File.Exists(newPath)) File.Delete(newPath);
                var oldPath = Path.Combine(modsFolder, entry.OldFileName);
                File.Copy(entry.BackupPath, oldPath, true);
                restored++;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "回滚失败：" + entry.OldFileName);
                failures.Add(entry.OldFileName + "：" + ex.Message);
            }
        }
        // 回滚成功后把该快照从索引里移除，避免重复回滚
        if (failures.Count == 0) {
            Save(modsFolder, snapshots.Where(snapshot => snapshot.Id != target.Id).ToList());
        }
        return new ModRollbackResult {
            Success = failures.Count == 0,
            Restored = restored,
            Failures = failures,
            Message = failures.Count == 0
                ? $"已回滚 {restored} 个文件"
                : $"回滚 {restored} 个成功，{failures.Count} 个失败"
        };
    }

    private static void Save(string modsFolder, IReadOnlyList<ModUpdateSnapshot> snapshots) {
        try {
            var path = ResolvePath(modsFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var array = new JArray(snapshots.Select(snapshot => new JObject {
                ["id"] = snapshot.Id,
                ["createdAt"] = snapshot.CreatedAt.ToString("O"),
                ["entries"] = new JArray(snapshot.Entries.Select(entry => new JObject {
                    ["modId"] = entry.ModId,
                    ["oldFileName"] = entry.OldFileName,
                    ["newFileName"] = entry.NewFileName,
                    ["backupPath"] = entry.BackupPath
                }))
            }));
            File.WriteAllText(path, array.ToString(Formatting.None));
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "保存更新快照失败");
        }
    }

    private static ModUpdateSnapshot? Parse(JObject json) {
        var id = json["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(id)) return null;
        var entries = (json["entries"] as JArray ?? [])
            .OfType<JObject>()
            .Select(entry => new ModUpdateSnapshotEntry {
                ModId = entry["modId"]?.ToString() ?? "",
                OldFileName = entry["oldFileName"]?.ToString() ?? "",
                NewFileName = entry["newFileName"]?.ToString() ?? "",
                BackupPath = entry["backupPath"]?.ToString() ?? ""
            })
            .Where(entry => entry.OldFileName.Length > 0 && entry.BackupPath.Length > 0)
            .ToList();
        if (entries.Count == 0) return null;
        return new ModUpdateSnapshot {
            Id = id,
            CreatedAt = DateTime.TryParse(json["createdAt"]?.ToString(), out var created) ? created : DateTime.MinValue,
            Entries = entries
        };
    }

}
