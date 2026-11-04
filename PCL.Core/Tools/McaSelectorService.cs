using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

namespace PCL.Core.Tools;

/// <summary>
/// 解压并启动内置的 MCA Selector 2.9。
/// </summary>
public static class McaSelectorService {

    private const string ResourceName = "PCL.Resources.McASelector.zip";
    private const string Version = "2.9";
    private static readonly object Gate = new();

    public static string EnsureExtracted(string pclRoot) {
        lock (Gate) {
            var target = Path.Combine(pclRoot, "McaSelector");
            var marker = Path.Combine(target, ".pcl-version");
            if (File.Exists(marker) && File.ReadAllText(marker).Trim() == Version
                && File.Exists(Path.Combine(target, "MCA Selector.exe"))) {
                return target;
            }

            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                               ?? throw new InvalidOperationException("内置 MCA Selector 资源缺失。");
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var root = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries) {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var destination = Path.GetFullPath(Path.Combine(target, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException("MCA Selector 包含非法路径：" + entry.FullName);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }
            File.WriteAllText(marker, Version);
            return target;
        }
    }

    public static Process Launch(string pclRoot, string? worldFolder = null) {
        var folder = EnsureExtracted(pclRoot);
        var exe = Path.Combine(folder, "MCA Selector.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("未找到 MCA Selector 可执行文件。", exe);
        var info = new ProcessStartInfo {
            FileName = exe,
            WorkingDirectory = folder,
            UseShellExecute = true
        };
        if (!string.IsNullOrWhiteSpace(worldFolder)) {
            info.Arguments = "\"" + worldFolder!.TrimEnd('\\', '/') + "\"";
        }
        return Process.Start(info) ?? throw new InvalidOperationException("无法启动 MCA Selector。");
    }

}
