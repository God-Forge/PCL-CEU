using System.Diagnostics;

namespace PCL.Core.Launch;

/// <summary>命令执行结果。</summary>
public sealed class LaunchCommandResult {

    public bool Started { get; init; }
    public int ExitCode { get; init; }
    public string? Error { get; init; }
    public bool Waited { get; init; }

    public bool Success => Started && Error is null;

}

/// <summary>
/// 执行启动前后的自定义命令。命令通过 cmd.exe 执行以支持管道与重定向，
/// 工作目录固定为实例目录（与 VB 版一致）。
/// </summary>
public static class LaunchCommandRunner {

    /// <summary>
    /// 执行一条命令。<paramref name="wait"/> 为真时等待其结束并返回退出码，
    /// 否则立即返回（适合启动游戏后再执行的场景）。
    /// </summary>
    public static async Task<LaunchCommandResult> RunAsync(
        string? command,
        string workingDirectory,
        bool wait,
        int timeoutSeconds = 120,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(command)) {
            return new LaunchCommandResult { Started = false, Error = "命令为空" };
        }
        var startInfo = new ProcessStartInfo("cmd.exe") {
            Arguments = "/c " + command,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppContext.BaseDirectory : workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        try {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start()) {
                return new LaunchCommandResult { Started = false, Error = "无法启动进程" };
            }
            if (!wait) {
                return new LaunchCommandResult { Started = true, ExitCode = 0, Waited = false };
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds)));
            try {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                try { if (!process.HasExited) process.Kill(true); } catch { /* 忽略 */ }
                if (cancellationToken.IsCancellationRequested) throw;
                return new LaunchCommandResult { Started = true, Waited = true, Error = "执行超时" };
            }
            return new LaunchCommandResult {
                Started = true,
                ExitCode = process.ExitCode,
                Waited = true
            };
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "执行启动命令失败：" + command);
            return new LaunchCommandResult { Started = false, Error = ex.Message };
        }
    }

}
