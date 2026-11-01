using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class InstanceSnapshotInfo {

    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime Created { get; init; }
    public long Size { get; init; }

}

/// <summary>
/// 创建与恢复实例配置快照（Mod、配置、资源包与关键文件）。
/// </summary>
public static class InstanceSnapshotService {

    private static readonly string[] SnapshotItems = {
        "mods", "config", "defaultconfigs", "options.txt", "servers.dat",
        "resourcepacks", "shaderpacks", "scripts", "kubejs"
    };

    public static InstanceSnapshotInfo Create(string instanceFolder, string snapshotRoot, string? name = null) {
        instanceFolder = Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(instanceFolder)) throw new DirectoryNotFoundException("实例不存在：" + instanceFolder);
        var instanceName = Path.GetFileName(instanceFolder);
        var folder = Path.Combine(snapshotRoot, instanceName);
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var snapshotName = string.IsNullOrWhiteSpace(name) ? stamp : name;
        var output = Path.Combine(folder, snapshotName + ".zip");
        if (File.Exists(output)) output = Path.Combine(folder, stamp + "-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".zip");

        var temp = Path.Combine(Path.GetTempPath(), "pcl-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            var copied = new List<string>();
            foreach (var item in SnapshotItems) {
                var source = Path.Combine(instanceFolder, item);
                if (File.Exists(source)) {
                    File.Copy(source, Path.Combine(temp, item), true);
                    copied.Add(item);
                } else if (Directory.Exists(source)) {
                    CopyDirectory(source, Path.Combine(temp, item));
                    copied.Add(item);
                }
            }
            var manifest = new JObject {
                ["instance"] = instanceName,
                ["created"] = DateTime.Now.ToString("O"),
                ["items"] = new JArray(copied)
            };
            File.WriteAllText(Path.Combine(temp, "snapshot.json"), manifest.ToString(), new UTF8Encoding(false));
            if (File.Exists(output)) File.Delete(output);
            ZipFile.CreateFromDirectory(temp, output, CompressionLevel.Optimal, false);
            var info = new FileInfo(output);
            return new InstanceSnapshotInfo {
                Path = output,
                Name = Path.GetFileNameWithoutExtension(output),
                Created = info.CreationTime,
                Size = info.Length
            };
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    public static IReadOnlyList<InstanceSnapshotInfo> List(string instanceFolder, string snapshotRoot) {
        var folder = Path.Combine(snapshotRoot, Path.GetFileName(Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar)));
        if (!Directory.Exists(folder)) return Array.Empty<InstanceSnapshotInfo>();
        return Directory.EnumerateFiles(folder, "*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTime)
            .Select(info => new InstanceSnapshotInfo {
                Path = info.FullName,
                Name = Path.GetFileNameWithoutExtension(info.Name),
                Created = info.CreationTime,
                Size = info.Length
            }).ToList();
    }

    public static void Restore(string snapshotPath, string instanceFolder) {
        if (!File.Exists(snapshotPath)) throw new FileNotFoundException("快照不存在。", snapshotPath);
        instanceFolder = Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar);
        using var archive = ZipFile.OpenRead(snapshotPath);
        foreach (var entry in archive.Entries) {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var target = Path.GetFullPath(Path.Combine(instanceFolder, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(instanceFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("快照包含非法路径：" + entry.FullName);
            }
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            entry.ExtractToFile(target, true);
        }
    }

    private static void CopyDirectory(string source, string target) {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            var relative = file.Substring(source.Length + 1);
            var destination = Path.Combine(target, relative);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.Copy(file, destination, true);
        }
    }

}
