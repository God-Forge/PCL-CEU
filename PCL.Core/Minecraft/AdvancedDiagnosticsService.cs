using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using PCL.Core.Download;

namespace PCL.Core.Minecraft;

public sealed class EnvironmentInfo {

    public string Os { get; init; } = "";
    public string Gpu { get; init; } = "";
    public string Java { get; init; } = "";

}

public sealed class DownloadSourceRecord {

    public DateTime Time { get; init; } = DateTime.Now;
    public IReadOnlyList<DownloadSpeedResult> Results { get; init; } = Array.Empty<DownloadSpeedResult>();

}

/// <summary>
/// 崩溃对比、环境信息、失败建议与下载源测速历史。
/// </summary>
public static class AdvancedDiagnosticsService {

    public static string CompareLogs(string previousPath, string currentPath) {
        var previous = File.ReadAllLines(previousPath, Encoding.UTF8);
        var current = File.ReadAllLines(currentPath, Encoding.UTF8);
        var previousSet = new HashSet<string>(previous);
        var currentSet = new HashSet<string>(current);
        var removed = previous.Where(line => !currentSet.Contains(line)).Take(40).ToList();
        var added = current.Where(line => !previousSet.Contains(line)).Take(40).ToList();
        var lines = new List<string>();
        lines.Add("=== 崩溃报告对比 ===");
        lines.Add("仅旧报告出现：");
        lines.AddRange(removed.Count == 0 ? new List<string> { "（无）" } : removed);
        lines.Add("");
        lines.Add("仅新报告出现：");
        lines.AddRange(added.Count == 0 ? new List<string> { "（无）" } : added);
        return string.Join("\r\n", lines);
    }

    public static EnvironmentInfo CollectEnvironment(string? javaExe) {
        return new EnvironmentInfo {
            Os = Environment.OSVersion + " / " + (Environment.Is64BitOperatingSystem ? "64 位" : "32 位"),
            Gpu = ReadGpuName(),
            Java = ReadJavaVersion(javaExe)
        };
    }

    public static string BuildFailureAdvice(int exitCode, IEnumerable<string> reasons) {
        var lines = new List<string>();
        var exit = ExitCodeAnalyzer.Analyze(exitCode);
        if (exit is not null) lines.Add($"退出码 {exitCode}：{exit.Description}\r\n{exit.Suggestion}");
        foreach (var reason in reasons) {
            var suggestion = CrashFixSuggestionService.GetSuggestion(reason);
            if (suggestion is not null) lines.Add(reason + "：" + suggestion);
        }
        return lines.Count == 0 ? "未发现明确的失败原因，请查看 latest.log 或诊断包。" : string.Join("\r\n\r\n", lines);
    }

    public static void RecordSourceSpeed(string path, IReadOnlyList<DownloadSpeedResult> results) {
        var records = LoadSourceHistory(path).ToList();
        records.Insert(0, new DownloadSourceRecord { Results = results });
        if (records.Count > 50) records.RemoveRange(50, records.Count - 50);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonConvert.SerializeObject(records, Formatting.Indented), new UTF8Encoding(false));
    }

    public static IReadOnlyList<DownloadSourceRecord> LoadSourceHistory(string path) {
        if (!File.Exists(path)) return Array.Empty<DownloadSourceRecord>();
        try {
            return JsonConvert.DeserializeObject<List<DownloadSourceRecord>>(File.ReadAllText(path)) ?? new List<DownloadSourceRecord>();
        } catch {
            return Array.Empty<DownloadSourceRecord>();
        }
    }

    public static string FormatSourceHistory(IReadOnlyList<DownloadSourceRecord> records) {
        if (records.Count == 0) return "暂无测速历史。";
        return string.Join("\r\n\r\n", records.Take(10).Select(record =>
            record.Time.ToString("yyyy-MM-dd HH:mm") + "\r\n" + DownloadSourceSpeedTester.Format(record.Results)));
    }

    public static string? ChooseFastest(IReadOnlyList<DownloadSpeedResult> results) {
        return results.Where(result => result.Success)
            .OrderBy(result => result.ElapsedMilliseconds)
            .Select(result => result.Name)
            .FirstOrDefault();
    }

    private static string ReadGpuName() {
        try {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = "wmic",
                Arguments = "path win32_VideoController get name",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return "未知";
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            var name = output.Split('\n').Select(line => line.Trim())
                .FirstOrDefault(line => line.Length > 0 && !line.Equals("Name", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(name) ? "未知" : name!;
        } catch {
            return "未知";
        }
    }

    private static string ReadJavaVersion(string? javaExe) {
        if (string.IsNullOrWhiteSpace(javaExe) || !File.Exists(javaExe)) return "未指定";
        try {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = javaExe!,
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process is null) return "未知";
            var text = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            var line = text.Split('\n').Select(value => value.Trim()).FirstOrDefault(value => value.Length > 0);
            return string.IsNullOrWhiteSpace(line) ? "未知" : line!;
        } catch {
            return "未知";
        }
    }

}
