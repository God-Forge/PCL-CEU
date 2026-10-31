using System.IO.Compression;

namespace PCL.Core.Minecraft;

public sealed class NativesExtractResult {

    public IReadOnlyList<string> Extracted { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Skipped { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Deleted { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Failed { get; init; } = Array.Empty<string>();

}

/// <summary>
/// 将 Natives jar 中的 DLL 解压到实例的 natives 目录，并清理多余文件。
/// </summary>
public static class NativesExtractor {

    public static NativesExtractResult Extract(IEnumerable<string> nativeJars, string targetFolder) {
        targetFolder = Path.GetFullPath(targetFolder);
        Directory.CreateDirectory(targetFolder);
        var extracted = new List<string>();
        var skipped = new List<string>();
        var deleted = new List<string>();
        var failed = new List<string>();
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var jar in nativeJars) {
            ZipArchive archive;
            try {
                archive = ZipFile.OpenRead(jar);
            } catch (InvalidDataException ex) {
                TryDelete(jar);
                throw new InvalidDataException("无法打开 Natives 文件（" + jar + "），该文件可能已损坏。", ex);
            }

            using (archive) {
                foreach (var entry in archive.Entries) {
                    if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                    var path = Path.GetFullPath(Path.Combine(targetFolder, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!IsUnder(targetFolder, path)) {
                        failed.Add(path);
                        continue;
                    }
                    expected.Add(path);

                    var info = new FileInfo(path);
                    if (info.Exists && info.Length == entry.Length) {
                        skipped.Add(path);
                        continue;
                    }
                    if (info.Exists) {
                        try {
                            info.Delete();
                        } catch (UnauthorizedAccessException) {
                            failed.Add(path);
                            break;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using (var input = entry.Open())
                    using (var output = File.Create(path)) {
                        input.CopyTo(output);
                    }
                    extracted.Add(path);
                }
            }
        }

        foreach (var file in Directory.EnumerateFiles(targetFolder)) {
            if (expected.Contains(file)) continue;
            try {
                File.Delete(file);
                deleted.Add(file);
            } catch (UnauthorizedAccessException) {
                failed.Add(file);
                break;
            }
        }

        return new NativesExtractResult {
            Extracted = extracted,
            Skipped = skipped,
            Deleted = deleted,
            Failed = failed
        };
    }

    private static bool IsUnder(string root, string path) {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path) {
        try {
            File.Delete(path);
        } catch {
            // 删除失败时仍抛出原始损坏异常。
        }
    }

}
