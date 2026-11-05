using System.Diagnostics;

namespace PCL.Core.Minecraft;

/// <summary>运行安装器 jar 的结果。</summary>
public sealed class JarInstallerResult {

    public bool Success { get; init; }
    public string VersionFolder { get; init; } = "";
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Output { get; init; } = [];

}

/// <summary>只运行安装器、不做产物判断的执行结果。</summary>
public sealed record JarInstallerExecution(bool Started, int ExitCode, IReadOnlyList<string> Output, string? Error) {

    public bool Succeeded => Started && Error is null;

}

/// <summary>
/// 运行「安装器 jar」（OptiFine、LiteLoader 等）的公共流程：
/// 以 <c>--install</c> 静默安装，产物检查交给调用方。
/// </summary>
public static class JarInstallerRunner {

    /// <summary>安装器参数：静默安装到指定 Minecraft 目录。</summary>
    public static IReadOnlyList<string> BuildArguments(string installerPath, string mcFolder) =>
        ["-jar", installerPath, "--install", mcFolder];

    /// <summary>产物版本目录。</summary>
    public static string ResolveVersionFolder(string mcFolder, string versionName) =>
        Path.Combine(mcFolder, "versions", versionName);

    /// <summary>判断安装是否产出了版本 JSON。</summary>
    public static bool HasVersionFiles(string mcFolder, string versionName) {
        if (string.IsNullOrWhiteSpace(mcFolder) || string.IsNullOrWhiteSpace(versionName)) return false;
        return File.Exists(Path.Combine(ResolveVersionFolder(mcFolder, versionName), versionName + ".json"));
    }

    /// <summary>
    /// 运行安装器并等待退出。不判断产物；超时会杀掉进程。
    /// </summary>
    public static async Task<JarInstallerExecution> ExecuteAsync(
        string javaExePath,
        string installerPath,
        string mcFolder,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) {
        var startInfo = new ProcessStartInfo(javaExePath) {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(mcFolder)) ?? AppContext.BaseDirectory
        };
        foreach (var argument in BuildArguments(Path.GetFullPath(installerPath), Path.GetFullPath(mcFolder))) {
            startInfo.ArgumentList.Add(argument);
        }

        var output = new List<string>();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.Add(e.Data); };
        try {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(30, timeoutSeconds)));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return new JarInstallerExecution(true, process.ExitCode, Snapshot(output), null);
        } catch (OperationCanceledException) {
            try { if (!process.HasExited) process.Kill(true); } catch { /* 忽略 */ }
            if (cancellationToken.IsCancellationRequested) throw;
            return new JarInstallerExecution(false, -1, Snapshot(output), "安装超时。");
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return new JarInstallerExecution(false, -1, Snapshot(output), "运行安装器失败：" + ex.Message);
        }
    }

    /// <summary>运行安装器并以产物版本文件判定成功（适用于版本名可预期的安装器）。</summary>
    public static async Task<JarInstallerResult> RunAsync(
        string javaExePath,
        string installerPath,
        string mcFolder,
        string expectedVersionName,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) {
        // 已安装时不需要 Java 与安装器
        if (HasVersionFiles(mcFolder, expectedVersionName)) {
            return new JarInstallerResult {
                Success = true,
                VersionFolder = ResolveVersionFolder(mcFolder, expectedVersionName),
                Message = "该版本已安装。"
            };
        }
        if (!File.Exists(installerPath)) {
            return new JarInstallerResult { Success = false, Message = "安装器不存在：" + installerPath };
        }
        if (!File.Exists(javaExePath)) {
            return new JarInstallerResult { Success = false, Message = "找不到 Java：" + javaExePath };
        }

        var execution = await ExecuteAsync(javaExePath, installerPath, mcFolder, timeoutSeconds, cancellationToken).ConfigureAwait(false);
        if (!execution.Succeeded) {
            return new JarInstallerResult { Success = false, Message = execution.Error ?? "安装器运行失败。", Output = execution.Output };
        }
        var installed = HasVersionFiles(mcFolder, expectedVersionName);
        return new JarInstallerResult {
            Success = installed,
            VersionFolder = installed ? ResolveVersionFolder(mcFolder, expectedVersionName) : "",
            Message = installed ? "安装完成。" : $"安装器已退出，但没有找到版本文件 {expectedVersionName}。",
            Output = execution.Output
        };
    }

    private static IReadOnlyList<string> Snapshot(List<string> output) {
        lock (output) return output.ToList();
    }

}
