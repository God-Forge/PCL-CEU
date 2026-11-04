using System.IO.Compression;

namespace PCL.Core.Modpack;

/// <summary>
/// 整合包 overrides 导入：将 overrides/ 解压到实例目录，阻止路径穿越。
/// </summary>
public static class ModpackInstallService {

    public static int InstallOverrides(string zipPath, string instanceFolder) {
        var root = Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var count = 0;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries) {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            var normalized = entry.FullName.Replace('\\', '/');
            if (!normalized.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase)) continue;
            var relative = normalized.Substring("overrides/".Length);
            if (string.IsNullOrWhiteSpace(relative)) continue;
            var target = Path.GetFullPath(Path.Combine(instanceFolder, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidDataException("整合包包含非法路径：" + entry.FullName);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
            count++;
        }
        return count;
    }

}

