using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using PCL.Core.Mods;

namespace PCL.Core.Minecraft;

public sealed class DiagnosticPackageResult {

    public string Path { get; init; } = "";
    public int FileCount { get; init; }
    public long Size { get; init; }

}

/// <summary>
/// 生成实例崩溃诊断包：日志、系统信息、Mod 列表与脱敏后的元数据。
/// </summary>
public static class DiagnosticPackageService {

    public static DiagnosticPackageResult Create(string instanceFolder, string mcFolder, string outputPath) {
        var temp = Path.Combine(Path.GetTempPath(), "pcl-diag-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try {
            var files = new List<string>();
            foreach (var name in new[] { "latest.log", "debug.log" }) {
                var source = Path.Combine(instanceFolder, "logs", name);
                if (File.Exists(source)) {
                    files.Add(CopyRedacted(source, temp, "logs/" + name));
                }
            }

            var crashFolder = Path.Combine(instanceFolder, "crash-reports");
            if (Directory.Exists(crashFolder)) {
                foreach (var file in Directory.EnumerateFiles(crashFolder, "*", SearchOption.TopDirectoryOnly)) {
                    files.Add(CopyRedacted(file, temp, "crash-reports/" + Path.GetFileName(file)));
                }
            }
            foreach (var file in Directory.EnumerateFiles(instanceFolder, "hs_err*.log", SearchOption.TopDirectoryOnly)) {
                files.Add(CopyRedacted(file, temp, Path.GetFileName(file)));
            }

            var scan = ModScanner.Scan(Path.Combine(instanceFolder, "mods"));
            var modLines = scan.Mods.Select(mod =>
                $"{mod.FileName}\t{mod.Loader}\t{mod.ModId}\t{mod.Version}\t{(mod.Enabled ? "启用" : "禁用")}");
            var issueLines = scan.Issues.Select(issue => $"[{issue.Severity}] {issue.FileName}: {issue.Message}");
            WriteText(temp, "mods.txt", string.Join("\r\n", modLines.Concat(issueLines)));

            WriteText(temp, "system-info.txt", string.Join("\r\n", new[] {
                "时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                "系统：" + Environment.OSVersion,
                "64 位系统：" + Environment.Is64BitOperatingSystem,
                "64 位进程：" + Environment.Is64BitProcess,
                "处理器数量：" + Environment.ProcessorCount,
                ".NET：" + Environment.Version,
                "实例：" + instanceFolder,
                "Minecraft 文件夹：" + mcFolder
            }));

            var manifest = new JObject {
                ["created"] = DateTime.Now.ToString("O"),
                ["instance"] = instanceFolder,
                ["mcFolder"] = mcFolder,
                ["modCount"] = scan.Mods.Count,
                ["modIssueCount"] = scan.Issues.Count,
                ["files"] = new JArray(files.Select(file => Path.GetFileName(file)))
            };
            WriteText(temp, "manifest.json", manifest.ToString());

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            ZipFile.CreateFromDirectory(temp, outputPath, CompressionLevel.Optimal, false);
            var info = new FileInfo(outputPath);
            return new DiagnosticPackageResult {
                Path = outputPath,
                FileCount = files.Count + 3,
                Size = info.Length
            };
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static string CopyRedacted(string source, string root, string relativePath) {
        var target = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var text = File.ReadAllText(source);
        File.WriteAllText(target, Redact(text), new UTF8Encoding(false));
        return target;
    }

    private static void WriteText(string root, string relativePath, string text) {
        var target = Path.Combine(root, relativePath);
        File.WriteAllText(target, Redact(text), new UTF8Encoding(false));
    }

    private static string Redact(string text) {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile)) {
            text = text.Replace(userProfile, "%USERPROFILE%");
        }
        text = Regex.Replace(text,
            @"(?i)(accessToken|clientToken|auth_access_token|access_token)(""?\s*[:=]\s*""?)[^""'\s,}]+",
            "$1$2***");
        text = Regex.Replace(text, @"(?i)(username[""' ]?\s*[:=]\s*[""' ]?)[^""'\s,}]+", "$1***");
        return text;
    }

}

