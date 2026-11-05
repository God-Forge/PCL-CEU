using PCL.Core.Java;

namespace PCL.Core.Java;

public sealed class JavaInstallation {

    public string JavaHome { get; init; } = "";
    public string JavaExe => System.IO.Path.Combine(JavaHome, "bin", "java.exe");
    public string Version { get; init; } = "";
    public int Major { get; init; }

}

/// <summary>
/// 扫描常见 Java 安装目录，供启动页选择可用 Java。
/// </summary>
public static class JavaScanner {

    public static IReadOnlyList<string> DefaultRoots(string baseDirectory, string appData) {
        return new[] {
            Environment.GetEnvironmentVariable("JAVA_HOME") ?? "",
            System.IO.Path.Combine(baseDirectory, "PCL", "Java"),
            System.IO.Path.Combine(appData, ".minecraft", "runtime"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"),
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft")
        }.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)).ToList();
    }

    public static IReadOnlyList<JavaInstallation> Scan(IEnumerable<string> roots) {
        var result = new List<JavaInstallation>();
        foreach (var root in roots) {
            if (!Directory.Exists(root)) continue;
            foreach (var javaExe in FindJavaExe(root, 0)) {
                try {
                    var info = JavaPackageService.Detect(javaExe);
                    var major = ParseMajor(info.JavaVersion);
                    result.Add(new JavaInstallation {
                        JavaHome = info.JavaHome,
                        Version = info.JavaVersion,
                        Major = major
                    });
                } catch {
                }
            }
        }
        return result.GroupBy(item => item.JavaHome, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.Major)
            .ToList();
    }

    public static JavaInstallation? FindBest(IEnumerable<string> roots, int requiredMajor = 0) {
        var all = Scan(roots);
        if (requiredMajor <= 0) return all.FirstOrDefault();
        return all.Where(item => item.Major >= requiredMajor).OrderBy(item => item.Major).FirstOrDefault()
               ?? all.FirstOrDefault();
    }

    /// <summary>
    /// 检查指定的 Java 路径（可以是 java.exe、bin 目录或 Java 主目录）。
    /// 路径无效或无法识别时返回 <c>null</c>。
    /// </summary>
    public static JavaInstallation? Inspect(string? pathOrHome) {
        if (string.IsNullOrWhiteSpace(pathOrHome)) return null;
        var path = pathOrHome.Trim();
        string? javaExe = null;
        if (File.Exists(path) && path.EndsWith("java.exe", StringComparison.OrdinalIgnoreCase)) {
            javaExe = path;
        } else if (Directory.Exists(path)) {
            javaExe = FindJavaExe(path, 0).FirstOrDefault();
        }
        if (javaExe is null) return null;
        try {
            var info = JavaPackageService.Detect(javaExe);
            return new JavaInstallation {
                JavaHome = info.JavaHome,
                Version = info.JavaVersion,
                Major = ParseMajor(info.JavaVersion)
            };
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "无法识别指定的 Java：" + path);
            return null;
        }
    }

    private static IEnumerable<string> FindJavaExe(string root, int depth) {
        var candidate = System.IO.Path.Combine(root, "bin", "java.exe");
        if (File.Exists(candidate)) yield return candidate;
        if (depth >= 4) yield break;
        IEnumerable<string> children;
        try {
            children = Directory.EnumerateDirectories(root);
        } catch {
            yield break;
        }
        foreach (var child in children) {
            foreach (var java in FindJavaExe(child, depth + 1)) yield return java;
        }
    }

    private static int ParseMajor(string version) {
        var text = version.Trim();
        if (text.StartsWith("1.", StringComparison.Ordinal)) text = text.Substring(2);
        var dot = text.IndexOf('.');
        if (dot >= 0) text = text.Substring(0, dot);
        return int.TryParse(text, out var major) ? major : 0;
    }

}


