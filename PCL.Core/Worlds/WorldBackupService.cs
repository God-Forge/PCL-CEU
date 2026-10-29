using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Worlds;

public enum WorldBackupKind {
    World,
    Saves,
    PreRestore
}

public sealed class WorldBackupInfo {

    public string Path { get; init; } = "";
    public string InstanceName { get; init; } = "";
    public string WorldName { get; init; } = "";
    public WorldBackupKind Kind { get; init; }
    public DateTime CreatedUtc { get; init; }
    public long Size { get; init; }

}

public sealed class WorldBackupService {

    private const string MetadataEntry = "_pcl_backup.json";

    public WorldBackupInfo CreateBackup(
        string worldFolder,
        string backupRoot,
        string instanceName,
        string worldName,
        WorldBackupKind kind = WorldBackupKind.World) {
        if (!Directory.Exists(worldFolder)) throw new DirectoryNotFoundException("世界文件夹不存在：" + worldFolder);
        if (string.IsNullOrWhiteSpace(instanceName)) instanceName = "default";
        if (string.IsNullOrWhiteSpace(worldName)) worldName = Path.GetFileName(worldFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var instanceRoot = Path.Combine(backupRoot, SafeSegment(instanceName));
        Directory.CreateDirectory(instanceRoot);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var prefix = kind switch {
            WorldBackupKind.Saves => "saves",
            WorldBackupKind.PreRestore => "pre-restore",
            _ => SafeSegment(worldName)
        };
        var zipPath = Path.Combine(instanceRoot, $"{prefix}-{stamp}.zip");
        var index = 1;
        while (File.Exists(zipPath)) zipPath = Path.Combine(instanceRoot, $"{prefix}-{stamp}-{index++}.zip");

        var created = DateTime.UtcNow;
        try {
            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            AddFile(zip, worldFolder, worldFolder, MetadataEntry, BuildMetadata(instanceName, worldName, kind, created), true);
            foreach (var file in Directory.EnumerateFiles(worldFolder, "*", SearchOption.AllDirectories)) {
                var relative = GetRelativePath(worldFolder, file).Replace('\\', '/');
                if (string.Equals(Path.GetFileName(relative), "session.lock", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(relative, MetadataEntry, StringComparison.OrdinalIgnoreCase)) continue;
                AddFile(zip, file, worldFolder, relative, null, false);
            }
        } catch {
            TryDelete(zipPath);
            throw;
        }

        return new WorldBackupInfo {
            Path = zipPath,
            InstanceName = instanceName,
            WorldName = worldName,
            Kind = kind,
            CreatedUtc = created,
            Size = new FileInfo(zipPath).Length
        };
    }

    public IReadOnlyList<WorldBackupInfo> ListBackups(string backupRoot, string instanceName) {
        var instanceRoot = Path.Combine(backupRoot, SafeSegment(instanceName));
        if (!Directory.Exists(instanceRoot)) return Array.Empty<WorldBackupInfo>();

        var result = new List<WorldBackupInfo>();
        foreach (var zipPath in Directory.EnumerateFiles(instanceRoot, "*.zip", SearchOption.TopDirectoryOnly)) {
            try {
                using var zip = ZipFile.OpenRead(zipPath);
                var metadata = ReadMetadata(zip);
                var info = ParseMetadata(metadata);
                var createdText = info["createdUtc"]?.ToString();
                var created = DateTime.TryParse(createdText, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed.ToUniversalTime()
                    : File.GetLastWriteTimeUtc(zipPath);
                result.Add(new WorldBackupInfo {
                    Path = zipPath,
                    InstanceName = info["instance"]?.ToString() ?? instanceName,
                    WorldName = info["world"]?.ToString() ?? Path.GetFileNameWithoutExtension(zipPath),
                    Kind = ParseKind(info["kind"]?.ToString()),
                    CreatedUtc = created,
                    Size = new FileInfo(zipPath).Length
                });
            } catch {
                // 损坏或不完整的备份仍然列出，但不阻断其他备份。
                result.Add(new WorldBackupInfo {
                    Path = zipPath,
                    InstanceName = instanceName,
                    WorldName = Path.GetFileNameWithoutExtension(zipPath),
                    Kind = WorldBackupKind.World,
                    CreatedUtc = File.GetLastWriteTimeUtc(zipPath),
                    Size = new FileInfo(zipPath).Length
                });
            }
        }

        return result.OrderByDescending(item => item.CreatedUtc).ToList();
    }

    public void Prune(string backupRoot, string instanceName, int keep = 10) {
        if (keep < 1) return;
        var backups = ListBackups(backupRoot, instanceName);
        foreach (var backup in backups.Skip(keep)) TryDelete(backup.Path);
    }

    public WorldBackupInfo Restore(string backupPath, string targetFolder, string backupRoot, string instanceName) {
        if (!File.Exists(backupPath)) throw new FileNotFoundException("备份不存在", backupPath);

        WorldBackupInfo? safety = null;
        if (Directory.Exists(targetFolder) && Directory.EnumerateFileSystemEntries(targetFolder).Any()) {
            safety = CreateBackup(targetFolder, backupRoot, instanceName, "pre-restore", WorldBackupKind.PreRestore);
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(targetFolder)) ?? throw new InvalidOperationException("目标路径无效");
        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, ".pcl-restore-" + Guid.NewGuid().ToString("N"));
        var old = Path.Combine(parent, ".pcl-restore-old-" + Guid.NewGuid().ToString("N"));

        try {
            Directory.CreateDirectory(temp);
            Extract(backupPath, temp);

            if (Directory.Exists(targetFolder)) Directory.Move(targetFolder, old);
            Directory.Move(temp, targetFolder);
            TryDeleteDirectory(old);
            return safety ?? new WorldBackupInfo {
                Path = backupPath,
                InstanceName = instanceName,
                WorldName = Path.GetFileName(targetFolder),
                Kind = WorldBackupKind.World,
                CreatedUtc = DateTime.UtcNow,
                Size = new FileInfo(backupPath).Length
            };
        } catch {
            TryDeleteDirectory(temp);
            if (!Directory.Exists(targetFolder) && Directory.Exists(old)) {
                try { Directory.Move(old, targetFolder); } catch { }
            }
            throw;
        }
    }

    private static void Extract(string zipPath, string targetFolder) {
        using var zip = ZipFile.OpenRead(zipPath);
        var root = Path.GetFullPath(targetFolder) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries) {
            if (string.Equals(entry.FullName, MetadataEntry, StringComparison.OrdinalIgnoreCase)) continue;
            var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (relative.Length == 0) continue;
            var target = Path.GetFullPath(Path.Combine(targetFolder, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份包含不安全的路径：" + entry.FullName);
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = File.Create(target);
            input.CopyTo(output);
        }
    }

    private static void AddFile(ZipArchive zip, string filePath, string root, string entryName, string? content, bool isText) {
        var entry = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
        using var output = entry.Open();
        if (isText) {
            var bytes = Encoding.UTF8.GetBytes(content ?? "");
            output.Write(bytes, 0, bytes.Length);
        } else {
            using var input = File.OpenRead(filePath);
            input.CopyTo(output);
        }
    }

    private static string BuildMetadata(string instance, string world, WorldBackupKind kind, DateTime createdUtc) {
        return new JObject {
            ["format"] = "pcl-world-backup",
            ["version"] = 1,
            ["instance"] = instance,
            ["world"] = world,
            ["kind"] = kind switch {
                WorldBackupKind.Saves => "saves",
                WorldBackupKind.PreRestore => "pre-restore",
                _ => "world"
            },
            ["createdUtc"] = createdUtc.ToString("O")
        }.ToString(Formatting.None);
    }

    private static string? ReadMetadata(ZipArchive zip) {
        var entry = zip.GetEntry(MetadataEntry);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static JObject ParseMetadata(string? text) {
        if (string.IsNullOrWhiteSpace(text)) return new JObject();
        try { return JObject.Parse(text!); } catch { return new JObject(); }
    }

    private static WorldBackupKind ParseKind(string? text) {
        return text?.ToLowerInvariant() switch {
            "saves" => WorldBackupKind.Saves,
            "pre-restore" or "prerestore" => WorldBackupKind.PreRestore,
            _ => WorldBackupKind.World
        };
    }

    private static string SafeSegment(string value) {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value) builder.Append(invalid.Contains(c) ? '_' : c);
        var result = builder.ToString().Trim();
        if (string.IsNullOrEmpty(result) || result == "." || result == "..") return "default";
        return result;
    }

    private static string GetRelativePath(string root, string file) {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fileFull = Path.GetFullPath(file);
        if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("文件不在世界文件夹内：" + file);
        return fileFull.Substring(rootFull.Length);
    }

    private static void TryDelete(string path) {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path) {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

}
