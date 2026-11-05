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
        JarInstallerRunner.BuildArguments(installerPath, mcFolder);

    /// <summary>安装后应生成的版本目录。</summary>
    public static string ResolveVersionFolder(string mcFolder, string versionName) =>
        JarInstallerRunner.ResolveVersionFolder(mcFolder, versionName);

    /// <summary>判断安装是否确实产出了版本文件。</summary>
    public static bool HasVersionFiles(string mcFolder, string versionName) =>
        JarInstallerRunner.HasVersionFiles(mcFolder, versionName);

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
        var result = await JarInstallerRunner
            .RunAsync(javaExePath, installerPath, mcFolder, expectedVersionName, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        return new OptiFineInstallResult {
            Success = result.Success,
            VersionFolder = result.VersionFolder,
            Message = result.Message,
            Output = result.Output
        };
    }

}
