using System.Text;

namespace PCL.Core.Minecraft;

public sealed class LogViewResult {

    public string Path { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    public bool FoundErrors { get; init; }

}

/// <summary>
/// 从 latest.log 提取错误行，供日志查看器使用。
/// </summary>
public static class LogViewerService {

    public static LogViewResult ExtractLatest(string instanceFolder, int maximum = 80) {
        var path = Path.Combine(instanceFolder, "logs", "latest.log");
        if (!File.Exists(path)) return new LogViewResult { Path = path };
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var errors = lines
            .Where(line => line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0
                           || line.IndexOf("FATAL", StringComparison.OrdinalIgnoreCase) >= 0
                           || line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                           || line.IndexOf("Caused by", StringComparison.OrdinalIgnoreCase) >= 0)
            .Take(maximum)
            .ToList();
        if (errors.Count == 0) {
            errors = lines.Skip(Math.Max(0, lines.Length - Math.Min(maximum, 30))).ToList();
        }
        return new LogViewResult {
            Path = path,
            Lines = errors,
            FoundErrors = errors.Count > 0 && lines.Any(line => line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
        };
    }

    public static string Format(LogViewResult result) {
        if (result.Lines.Count == 0) return "未找到 latest.log 或日志为空。";
        return $"日志：{result.Path}\r\n\r\n" + string.Join("\r\n", result.Lines);
    }

}

