using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft;

public sealed class CrashFinding {

    public string Reason { get; init; } = "";
    public string? Detail { get; init; }

}

/// <summary>
/// 补充崩溃分析规则：原生库、文件访问、命令行过长、存档损坏、着色器与 API 不兼容。
/// </summary>
public static class CrashPatternAnalyzer {

    public static IReadOnlyList<CrashFinding> Analyze(
        string? logMc,
        string? logCrash,
        string? logHs,
        string? logDebug) {
        var text = string.Join("\n", new[] { logMc, logCrash, logHs, logDebug }
            .Where(value => !string.IsNullOrEmpty(value)));
        var findings = new List<CrashFinding>();

        if (text.Contains("java.lang.StackOverflowError")
            || text.Contains("EXCEPTION_STACK_OVERFLOW")) {
            Add(findings, "堆栈溢出", null);
        }

        if (text.Contains("UnsatisfiedLinkError")
            || text.Contains("NoClassDefFoundError: com/sun/jna")
            || text.IndexOf("no lwjgl", StringComparison.OrdinalIgnoreCase) >= 0) {
            Add(findings, "原生库加载失败", Extract(text,
                @"(?:UnsatisfiedLinkError|NoClassDefFoundError):\s*([^\r\n]+)"));
        }

        if (text.Contains("AccessDeniedException")
            || text.Contains("FileNotFoundException")
            || text.Contains("NoSuchFileException")) {
            Add(findings, "文件访问失败", Extract(text,
                @"(?:AccessDeniedException|FileNotFoundException|NoSuchFileException):\s*([^\r\n]+)"));
        }

        if (text.Contains("CreateProcess error=206")
            || text.IndexOf("The filename or extension is too long", StringComparison.OrdinalIgnoreCase) >= 0) {
            Add(findings, "启动参数过长", "Windows 命令行长度超过限制");
        }

        var chunkMatch = Regex.Match(text,
            @"(?:Region file|Chunk file at).*?(?:is missing|does not exist|is invalid)|Compression method[^\r\n]*(?:invalid|unsupported)",
            RegexOptions.IgnoreCase);
        if (chunkMatch.Success) {
            Add(findings, "存档或区块损坏", chunkMatch.Value.Trim());
        }

        if (text.Contains("Shader compilation failed")
            || text.Contains("Failed to load resource pack")
            || text.Contains("Could not load shader")) {
            Add(findings, "资源包或光影加载失败", Extract(text,
                @"(?:Shader compilation failed|Failed to load resource pack|Could not load shader)[^\r\n]*"));
        }

        if (text.Contains("AbstractMethodError")
            || text.Contains("VerifyError")) {
            Add(findings, "Mod版本或API不兼容", Extract(text,
                @"(?:AbstractMethodError|VerifyError):\s*([^\r\n]+)"));
        }

        return findings
            .GroupBy(finding => (finding.Reason, finding.Detail))
            .Select(group => group.First())
            .ToList();
    }

    private static void Add(List<CrashFinding> findings, string reason, string? detail) {
        findings.Add(new CrashFinding {
            Reason = reason,
            Detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim()
        });
    }

    private static string? Extract(string text, string pattern) {
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

}


