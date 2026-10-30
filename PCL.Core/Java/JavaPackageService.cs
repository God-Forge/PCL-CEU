using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Java;

public enum JavaPackageType {
    Jdk,
    Jre
}

public sealed class JavaPackageInfo {

    public string JavaHome { get; init; } = "";
    public string JavaVersion { get; init; } = "unknown";
    public JavaPackageType Type { get; init; } = JavaPackageType.Jre;
    public string Vendor { get; init; } = "";
    public string Os { get; init; } = "windows";
    public string Arch { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public string Path { get; init; } = "";
    public long Size { get; init; }

    public string TypeText => Type == JavaPackageType.Jdk ? "jdk" : "jre";

}

/// <summary>
/// .javapkg：包含 javapkg.json 清单的 Java 主目录 ZIP。
/// </summary>
public static class JavaPackageService {

    private const string ManifestEntry = "javapkg.json";
    private const string FormatName = "pcl-javapkg";

    public static JavaPackageInfo Detect(string javaHomeOrExe) {
        var home = NormalizeJavaHome(javaHomeOrExe);
        if (!File.Exists(System.IO.Path.Combine(home, "bin", "java.exe"))) {
            throw new FileNotFoundException("不是有效的 Java 主目录（缺少 bin\\java.exe）", home);
        }

        var release = ReadRelease(home);
        var version = Value(release, "JAVA_VERSION") ?? Value(release, "JAVA_RUNTIME_VERSION") ?? "unknown";
        var vendor = Value(release, "IMPLEMENTOR") ?? Value(release, "JAVA_VENDOR") ?? "";
        var os = (Value(release, "OS_NAME") ?? "windows").ToLowerInvariant();
        var arch = NormalizeArch(Value(release, "OS_ARCH") ?? "");
        var type = File.Exists(System.IO.Path.Combine(home, "bin", "javac.exe"))
                   || Directory.Exists(System.IO.Path.Combine(home, "jmods"))
            ? JavaPackageType.Jdk
            : JavaPackageType.Jre;

        return new JavaPackageInfo {
            JavaHome = home,
            JavaVersion = version,
            Type = type,
            Vendor = vendor,
            Os = os,
            Arch = arch,
            CreatedUtc = DateTime.UtcNow
        };
    }

    public static string GetDefaultFileName(JavaPackageInfo info) {
        return Sanitize(info.JavaVersion) + "." + info.TypeText + ".javapkg";
    }

    public static JavaPackageInfo CreatePackage(string javaHomeOrExe, string outputPath) {
        var info = Detect(javaHomeOrExe);
        var outputFull = System.IO.Path.GetFullPath(outputPath);
        var parent = System.IO.Path.GetDirectoryName(outputFull);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

        var manifest = new JObject {
            ["format"] = FormatName,
            ["version"] = 1,
            ["javaVersion"] = info.JavaVersion,
            ["type"] = info.TypeText,
            ["vendor"] = info.Vendor,
            ["os"] = info.Os,
            ["arch"] = info.Arch,
            ["createdUtc"] = info.CreatedUtc.ToString("O")
        }.ToString(Newtonsoft.Json.Formatting.None);

        try {
            using var zip = ZipFile.Open(outputFull, ZipArchiveMode.Create);
            AddText(zip, ManifestEntry, manifest);
            foreach (var file in Directory.EnumerateFiles(info.JavaHome, "*", SearchOption.AllDirectories)) {
                var relative = GetRelativePath(info.JavaHome, file).Replace('\\', '/');
                if (string.Equals(relative, ManifestEntry, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(System.IO.Path.GetFullPath(file), outputFull, StringComparison.OrdinalIgnoreCase)) continue;
                var entry = zip.CreateEntry(relative, CompressionLevel.Fastest);
                using var output = entry.Open();
                using var input = File.OpenRead(file);
                input.CopyTo(output);
            }
        } catch {
            try { if (File.Exists(outputFull)) File.Delete(outputFull); } catch { }
            throw;
        }

        return new JavaPackageInfo {
            JavaHome = info.JavaHome,
            JavaVersion = info.JavaVersion,
            Type = info.Type,
            Vendor = info.Vendor,
            Os = info.Os,
            Arch = info.Arch,
            CreatedUtc = info.CreatedUtc,
            Path = outputFull,
            Size = new FileInfo(outputFull).Length
        };
    }

    public static JavaPackageInfo ReadInfo(string packagePath) {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("Java 包不存在", packagePath);
        using var zip = ZipFile.OpenRead(packagePath);
        var manifest = zip.GetEntry(ManifestEntry) ?? throw new InvalidDataException("Java 包缺少 javapkg.json。");
        using var reader = new StreamReader(manifest.Open(), Encoding.UTF8);
        var json = JObject.Parse(reader.ReadToEnd());
        if (!string.Equals(json["format"]?.ToString(), FormatName, StringComparison.Ordinal)) throw new InvalidDataException("不是 PCL Java 包。");
        if (json["version"]?.ToObject<int>() != 1) throw new InvalidDataException("不支持的 Java 包版本。");

        return new JavaPackageInfo {
            JavaVersion = json["javaVersion"]?.ToString() ?? "unknown",
            Type = string.Equals(json["type"]?.ToString(), "jdk", StringComparison.OrdinalIgnoreCase) ? JavaPackageType.Jdk : JavaPackageType.Jre,
            Vendor = json["vendor"]?.ToString() ?? "",
            Os = json["os"]?.ToString() ?? "windows",
            Arch = json["arch"]?.ToString() ?? "",
            CreatedUtc = DateTime.TryParse(json["createdUtc"]?.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var created)
                ? created.ToUniversalTime()
                : File.GetLastWriteTimeUtc(packagePath),
            Path = System.IO.Path.GetFullPath(packagePath),
            Size = new FileInfo(packagePath).Length
        };
    }

    public static JavaPackageInfo Extract(string packagePath, string targetFolder) {
        var info = ReadInfo(packagePath);
        var targetFull = System.IO.Path.GetFullPath(targetFolder);
        if (File.Exists(targetFull)) throw new InvalidOperationException("目标路径是一个文件：" + targetFull);
        if (Directory.Exists(targetFull) && Directory.EnumerateFileSystemEntries(targetFull).Any()) {
            throw new InvalidOperationException("目标文件夹已存在且不为空：" + targetFull);
        }

        var parent = System.IO.Path.GetDirectoryName(targetFull);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var temp = targetFull.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + ".pcl-tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temp);
        var root = temp.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;

        try {
            using (var zip = ZipFile.OpenRead(packagePath)) {
                foreach (var entry in zip.Entries) {
                    if (string.Equals(entry.FullName, ManifestEntry, StringComparison.OrdinalIgnoreCase)) continue;
                    var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (relative.Length == 0) continue;
                    var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(temp, relative));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Java 包包含不安全的路径：" + entry.FullName);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                    using var input = entry.Open();
                    using var output = File.Create(target);
                    input.CopyTo(output);
                }
            }

            if (!File.Exists(System.IO.Path.Combine(temp, "bin", "java.exe"))) {
                throw new InvalidDataException("Java 包解压后缺少 bin\\java.exe。");
            }

            if (Directory.Exists(targetFull)) Directory.Delete(targetFull);
            Directory.Move(temp, targetFull);
        } catch {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
            throw;
        }

        return new JavaPackageInfo {
            JavaHome = targetFull,
            JavaVersion = info.JavaVersion,
            Type = info.Type,
            Vendor = info.Vendor,
            Os = info.Os,
            Arch = info.Arch,
            CreatedUtc = info.CreatedUtc,
            Path = info.Path,
            Size = info.Size
        };
    }

    private static string NormalizeJavaHome(string path) {
        var full = System.IO.Path.GetFullPath(path);
        if (File.Exists(full) && string.Equals(System.IO.Path.GetFileName(full), "java.exe", StringComparison.OrdinalIgnoreCase)) {
            var bin = System.IO.Path.GetDirectoryName(full);
            return System.IO.Path.GetDirectoryName(bin) ?? bin ?? full;
        }
        return full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private static Dictionary<string, string> ReadRelease(string home) {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var file = System.IO.Path.Combine(home, "release");
        if (!File.Exists(file)) return result;
        foreach (var line in File.ReadAllLines(file)) {
            var split = line.IndexOf('=');
            if (split <= 0) continue;
            var key = line.Substring(0, split).Trim();
            var value = line.Substring(split + 1).Trim().Trim('"');
            result[key] = value;
        }
        return result;
    }

    private static string? Value(IReadOnlyDictionary<string, string> values, string key) {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private static string NormalizeArch(string arch) {
        return arch.ToLowerInvariant() switch {
            "x86_64" or "amd64" => "x64",
            "x86" or "i386" or "i686" => "x86",
            "aarch64" or "arm64" => "arm64",
            _ => arch
        };
    }

    private static string Sanitize(string value) {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var c in value) builder.Append(invalid.Contains(c) ? '_' : c);
        return string.IsNullOrWhiteSpace(builder.ToString()) ? "unknown" : builder.ToString();
    }

    private static void AddText(ZipArchive zip, string name, string content) {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string GetRelativePath(string root, string file) {
        var rootFull = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var fileFull = System.IO.Path.GetFullPath(file);
        if (!fileFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("文件不在 Java 主目录内。");
        return fileFull.Substring(rootFull.Length);
    }

}
