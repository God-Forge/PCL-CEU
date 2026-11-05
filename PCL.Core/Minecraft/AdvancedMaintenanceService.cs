using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Modpack;
using PCL.Core.Mods;
using PCL.Core.Worlds;

namespace PCL.Core.Minecraft;

public sealed class JvmPreset {

    public string Name { get; init; } = "";
    public IReadOnlyDictionary<string, object> Settings { get; init; } = new Dictionary<string, object>();

}

public sealed class ProcessSample {

    public long WorkingSet { get; init; }
    public double CpuPercent { get; init; }
    public int Threads { get; init; }

}

/// <summary>
/// 实例维护：离线包、迁移、存档、资源包备份、Mod 自动修复、JVM 预设与进程采样。
/// </summary>
public static class AdvancedMaintenanceService {

    public static string ExportOfflinePack(string instanceFolder, string mcFolder, string outputZip) =>
        ExportOfflinePack(instanceFolder, mcFolder, outputZip, new ModpackExportOptions());

    /// <summary>
    /// 导出离线整合包，并按 <paramref name="options"/> 决定包含哪些目录与资源。
    /// </summary>
    public static string ExportOfflinePack(
        string instanceFolder,
        string mcFolder,
        string outputZip,
        ModpackExportOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        var temp = Path.Combine(Path.GetTempPath(), "pcl-offline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            CopyDirectory(instanceFolder, Path.Combine(temp, "instance"), path => {
                var relative = path.Substring(instanceFolder.Length + 1);
                return ModpackExportFilter.ShouldInclude(relative, options);
            });
            var jsonPath = Directory.EnumerateFiles(instanceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (jsonPath is not null) {
                var json = JObject.Parse(File.ReadAllText(jsonPath));
                if (options.IncludeLibraries) {
                    foreach (var library in LibraryResolver.Resolve(json, mcFolder)) {
                        var source = Path.Combine(mcFolder, library.LocalPath);
                        if (!File.Exists(source)) continue;
                        var target = Path.Combine(temp, "libraries", library.LocalPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        File.Copy(source, target, true);
                    }
                }
                var assetIndex = json["assetIndex"]?["id"]?.ToString();
                if (options.IncludeAssets && !string.IsNullOrWhiteSpace(assetIndex)) {
                    var indexPath = Path.Combine(mcFolder, "assets", "indexes", assetIndex + ".json");
                    if (File.Exists(indexPath)) {
                        var indexTarget = Path.Combine(temp, "assets", "indexes", assetIndex + ".json");
                        Directory.CreateDirectory(Path.GetDirectoryName(indexTarget)!);
                        File.Copy(indexPath, indexTarget, true);
                        var index = JObject.Parse(File.ReadAllText(indexPath));
                        var assetRoot = Path.GetFullPath(mcFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        foreach (var asset in AssetIndexResolver.ResolveObjects(index, instanceFolder, mcFolder)) {
                            if (!File.Exists(asset.LocalPath)) continue;
                            if (!Path.GetFullPath(asset.LocalPath).StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase)) continue;
                            var relative = asset.LocalPath.Substring(assetRoot.Length);
                            var target = Path.Combine(temp, relative);
                            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                            File.Copy(asset.LocalPath, target, true);
                        }
                    }
                }
            }
            var manifest = new JObject {
                ["format"] = "pcl-offline-pack",
                ["version"] = 1,
                ["instance"] = Path.GetFileName(instanceFolder),
                ["created"] = DateTime.Now.ToString("O")
            };
            File.WriteAllText(Path.Combine(temp, "offline-pack.json"), manifest.ToString(), new UTF8Encoding(false));
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(temp, outputZip, CompressionLevel.Optimal, false);
            return outputZip;
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    public static string Migrate(string sourceFolder, string targetFolder, bool deleteSource) {
        InstanceCloneService.Clone(sourceFolder, targetFolder, new InstanceCloneOptions {
            IncludeSaves = true,
            IncludeMods = true,
            IncludeConfig = true,
            IncludeResourcePacks = true,
            IncludeShaderPacks = true,
            Overwrite = true
        });
        if (deleteSource) Directory.Delete(sourceFolder, true);
        return targetFolder;
    }

    public static string BackupWorld(string worldFolder, string backupRoot, string instanceName) {
        return new WorldBackupService().CreateBackup(worldFolder, backupRoot, instanceName, Path.GetFileName(worldFolder)).Path;
    }

    public static void RestoreWorld(string backupPath, string worldFolder, string backupRoot, string instanceName) {
        new WorldBackupService().Restore(backupPath, worldFolder, backupRoot, instanceName);
    }

    public static string BackupResourcePacks(string instanceFolder, string outputZip) {
        var temp = Path.Combine(Path.GetTempPath(), "pcl-resource-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            foreach (var folder in new[] { "resourcepacks", "shaderpacks" }) {
                var source = Path.Combine(instanceFolder, folder);
                if (Directory.Exists(source)) CopyDirectory(source, Path.Combine(temp, folder), _ => true);
            }
            if (File.Exists(outputZip)) File.Delete(outputZip);
            ZipFile.CreateFromDirectory(temp, outputZip, CompressionLevel.Optimal, false);
            return outputZip;
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    public static void RestoreResourcePacks(string backupZip, string instanceFolder) {
        using var archive = ZipFile.OpenRead(backupZip);
        var root = Path.GetFullPath(instanceFolder) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries) {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var target = Path.GetFullPath(Path.Combine(instanceFolder, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("备份包含非法路径。");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }

    public static IReadOnlyList<string> AutoFixMods(string instanceFolder) {
        var modsFolder = Path.Combine(instanceFolder, "mods");
        var result = new List<string>();
        if (Directory.Exists(modsFolder)) {
            var hashes = Directory.EnumerateFiles(modsFolder, "*.jar", SearchOption.TopDirectoryOnly)
                .Select(path => (Path: path, Hash: ComputeHash(path)))
                .GroupBy(item => item.Hash, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);
            foreach (var group in hashes) {
                foreach (var duplicate in group.Skip(1)) {
                    File.Delete(duplicate.Path);
                    result.Add("删除重复文件：" + Path.GetFileName(duplicate.Path));
                }
            }
        }
        var scan = ModScanner.Scan(modsFolder);
        foreach (var group in scan.Mods.Where(mod => mod.Enabled && !string.IsNullOrWhiteSpace(mod.ModId))
                     .GroupBy(mod => mod.ModId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1)) {
            foreach (var duplicate in group.Skip(1)) {
                var disabled = duplicate.FilePath + ".disabled";
                File.Move(duplicate.FilePath, disabled);
                result.Add("禁用重复 Mod：" + duplicate.FileName);
            }
        }
        if (result.Count == 0) result.Add("未发现可自动修复的重复 Mod。");
        return result;
    }

    public static IReadOnlyList<JvmPreset> JvmPresets() {
        return [
            new JvmPreset {
                Name = "低配",
                Settings = new Dictionary<string, object> {
                    ["VersionRamType"] = 2, ["VersionRamCustom"] = 2048, ["VersionAdvanceGC"] = 2
                }
            },
            new JvmPreset {
                Name = "均衡",
                Settings = new Dictionary<string, object> {
                    ["VersionRamType"] = 2, ["VersionRamCustom"] = 4096, ["VersionAdvanceGC"] = 0
                }
            },
            new JvmPreset {
                Name = "高性能",
                Settings = new Dictionary<string, object> {
                    ["VersionRamType"] = 2, ["VersionRamCustom"] = 8192, ["VersionAdvanceGC"] = 4
                }
            }
        ];
    }

    public static ProcessSample? SampleProcess(Process process) {
        try {
            var startCpu = process.TotalProcessorTime;
            var start = DateTime.UtcNow;
            Thread.Sleep(500);
            process.Refresh();
            var cpu = (process.TotalProcessorTime - startCpu).TotalMilliseconds
                      / Math.Max(1, (DateTime.UtcNow - start).TotalMilliseconds)
                      / Environment.ProcessorCount * 100;
            return new ProcessSample {
                WorkingSet = process.WorkingSet64,
                CpuPercent = Math.Max(0, Math.Min(100, cpu)),
                Threads = process.Threads.Count
            };
        } catch {
            return null;
        }
    }

    private static string ComputeHash(string path) {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static void CopyDirectory(string source, string target, Func<string, bool> include) {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            if (!include(file)) continue;
            var relative = file.Substring(source.Length + 1);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

}
