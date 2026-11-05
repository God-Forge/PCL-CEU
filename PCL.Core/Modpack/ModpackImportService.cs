using PCL.Core.Net;

namespace PCL.Core.Modpack;

/// <summary>整合包导入结果。</summary>
public sealed class ModpackImportResult {

    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string TargetFolder { get; init; } = "";
    public int Downloaded { get; init; }
    public int Failed { get; init; }
    public int Overrides { get; init; }

}

/// <summary>
/// 整合包导入的编排：校验目标 → 推导目录 → 调用安装器。
/// 供界面与 CLI 共用，避免两处各自拼装校验逻辑。
/// </summary>
public sealed class ModpackImportService {

    private readonly IDownloadService? _download;

    public ModpackImportService(IDownloadService? download = null) {
        _download = download;
    }

    public async Task<ModpackImportResult> ImportAsync(
        string zipPath,
        string versionsFolder,
        string? instanceName = null,
        CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) {
            return new ModpackImportResult { Success = false, Message = "整合包文件不存在。" };
        }
        if (string.IsNullOrWhiteSpace(versionsFolder)) {
            return new ModpackImportResult { Success = false, Message = "版本目录不能为空。" };
        }
        var error = ModpackInstallPlanner.Validate(versionsFolder, zipPath, instanceName);
        if (error is not null) {
            return new ModpackImportResult { Success = false, Message = error };
        }
        var target = ModpackInstallPlanner.ResolveTargetFolder(versionsFolder, zipPath, instanceName);
        try {
            var result = await new ModpackInstaller(_download)
                .InstallAsync(zipPath, target)
                .ConfigureAwait(false);
            return new ModpackImportResult {
                Success = result.Success,
                Message = result.Message,
                TargetFolder = target,
                Downloaded = result.Downloaded,
                Failed = result.Failed,
                Overrides = result.Overrides
            };
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "导入整合包失败：" + zipPath);
            return new ModpackImportResult {
                Success = false,
                Message = "导入失败：" + ex.Message,
                TargetFolder = target
            };
        }
    }

}
