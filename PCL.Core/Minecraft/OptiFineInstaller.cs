using System.Diagnostics;
using PCL.Core.Java;

namespace PCL.Core.Minecraft;

/// <summary>OptiFine 安装结果。</summary>
public sealed class OptiFineInstallResult {

    public bool Success { get; init; }
    public string VersionFolder { get; init; } = "";
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Output { get; init; } = [];

}

/// <summary>
/// OptiFine 安装：调用官方安装器的静默模式，把版本写入 versions 目录。
/// 与 Forge 系的 processors 流程不同，这里只是运行安装器并校验产物。
/// </summary>
public static class OptiFineInstaller {

    /// <summary>安装器静默安装参数。</summary>
    public static IReadOnlyList<string> BuildArguments(string installerPath, string mcFolder) =>
        ["-jar", installerPath, "--install", mcFolder];

    /// <summary>安装后应生成的版本目录。</summary>
    public static string ResolveVersionFolder(string mcFolder, string versionName) =>
        Path.Combine(mcFolder, "versions", versionName);

    /// <summary>判断安装是否确实产出了版本文件。</summary>
    public static bool HasVersionFiles(string mcFolder, string versionName) {
        if (string.IsNullOrWhiteSpace(mcFolder) || string.IsNullOrWhiteSpace(versionName)) return false;
        var folder = ResolveVersionFolder(mcFolder, versionName);
        return File.Exists(Path.Combine(folder, versionName + ".json"));
    }

    /// <summary>
    /// 运行 OptiFine 安装器。安装器的退出码不可靠，最终以产物是否存在为准。
    /// </summary>
    public static async Task<OptiFineInstallResult> InstallAsync(
        string installerPath,
        string mcFolder,
        string javaExePath,
        string expectedVersionName,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) {
        if (HasVersionFiles(mcFolder, expectedVersionName)) {
            return new OptiFineInstallResult {
                Success = true,
                VersionFolder = ResolveVersionFolder(mcFolder, expectedVersionName),
                Message = "该版本已安装。"
            };
        }
        if (!File.Exists(installerPath)) {
            return new OptiFineInstallResult { Success = false, Message = "安装器不存在：" + installerPath };
        }
        if (!File.Exists(javaExePath)) {
            return new OptiFineInstallResult { Success = false, Message = "找不到 Java：" + javaExePath };
        }

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
        } catch (OperationCanceledException) {
            try { if (!process.HasExited) process.Kill(true); } catch { /* 忽略 */ }
            if (cancellationToken.IsCancellationRequested) throw;
            return new OptiFineInstallResult { Success = false, Message = "安装超时。", Output = Snapshot(output) };
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return new OptiFineInstallResult { Success = false, Message = "运行安装器失败：" + ex.Message, Output = Snapshot(output) };
        }

        var installed = HasVersionFiles(mcFolder, expectedVersionName);
        return new OptiFineInstallResult {
            Success = installed,
            VersionFolder = installed ? ResolveVersionFolder(mcFolder, expectedVersionName) : "",
            Message = installed ? "安装完成。" : $"安装器已退出，但没有找到版本文件 {expectedVersionName}。",
            Output = Snapshot(output)
        };
    }

    private static IReadOnlyList<string> Snapshot(List<string> output) {
        lock (output) return output.ToList();
    }

}
