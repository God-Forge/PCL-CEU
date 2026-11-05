using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Mods;

/// <summary>应用一次 Mod 更新的结果。</summary>
public sealed class ModUpdateApplyResult {

    public bool Success { get; init; }
    public string Message { get; init; } = "";

    /// <summary>新文件的路径（成功时）。</summary>
    public string NewPath { get; init; } = "";

    /// <summary>旧文件的备份路径（成功时；备份失败则为空）。</summary>
    public string? BackupPath { get; init; }

}

/// <summary>
/// 把检查到的更新真正落地：下载新版本 → 备份旧文件 → 替换。
/// 任何一步失败都尽量保留原文件，避免用户丢掉能用的版本。
/// </summary>
public sealed class ModUpdateApplier {

    /// <summary>备份目录名（位于 mods 下）。</summary>
    public const string BackupFolderName = ".backup";

    private readonly IDownloadService _download;

    public ModUpdateApplier(IDownloadService? download = null) {
        _download = download ?? new HttpDownloadService();
    }

    public async Task<ModUpdateApplyResult> ApplyAsync(
        ModUpdateResult update,
        string modsFolder,
        string oldFilePath,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(update);
        if (!update.HasUpdate || update.LatestFile is null) {
            return new ModUpdateApplyResult { Success = false, Message = "没有可应用的更新。" };
        }
        if (string.IsNullOrWhiteSpace(modsFolder)) {
            return new ModUpdateApplyResult { Success = false, Message = "mods 目录不能为空。" };
        }
        if (!File.Exists(oldFilePath)) {
            return new ModUpdateApplyResult { Success = false, Message = "原文件不存在：" + oldFilePath };
        }
        var latest = update.LatestFile;
        var newPath = Path.Combine(modsFolder, latest.FileName);
        var tempPath = newPath + ".downloading";
        try {
            Directory.CreateDirectory(modsFolder);
            var url = latest.DownloadUrls.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(url)) {
                return new ModUpdateApplyResult { Success = false, Message = "新版本缺少下载地址。" };
            }
            var request = new DownloadRequest { Url = url, TargetPath = tempPath, Overwrite = true };
            // 先完整下载到临时文件，确认成功后再动原文件
            var download = await _download
                .DownloadAsync(request, null, cancellationToken)
                .ConfigureAwait(false);
            if (!download.Success || !File.Exists(tempPath)) {
                SafeDelete(tempPath);
                return new ModUpdateApplyResult { Success = false, Message = "下载新版本失败。" };
            }

            string? backup = null;
            try {
                backup = Backup(modsFolder, oldFilePath);
                // 同名文件先移除，避免 File.Move 失败
                if (File.Exists(newPath) && !string.Equals(
                        Path.GetFullPath(newPath), Path.GetFullPath(oldFilePath), StringComparison.OrdinalIgnoreCase)) {
                    File.Delete(newPath);
                }
                if (File.Exists(oldFilePath)) File.Delete(oldFilePath);
                File.Move(tempPath, newPath);
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Error(ex, "替换 Mod 文件失败，尝试恢复备份");
                SafeDelete(tempPath);
                Restore(backup, oldFilePath);
                return new ModUpdateApplyResult {
                    Success = false,
                    Message = "替换文件失败，已尝试恢复原文件：" + ex.Message
                };
            }
            return new ModUpdateApplyResult {
                Success = true,
                Message = $"已更新 {update.ModId}：{update.LocalVersion} → {update.LatestVersion}",
                NewPath = newPath,
                BackupPath = backup
            };
        } catch (OperationCanceledException) {
            SafeDelete(tempPath);
            throw;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            SafeDelete(tempPath);
            Logger.Error(ex, "应用 Mod 更新失败");
            return new ModUpdateApplyResult { Success = false, Message = "更新失败：" + ex.Message };
        }
    }

    private static string Backup(string modsFolder, string oldFilePath) {
        var folder = Path.Combine(modsFolder, BackupFolderName);
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(folder, Path.GetFileName(oldFilePath) + "." + stamp + ".bak");
        File.Copy(oldFilePath, target, true);
        return target;
    }

    private static void Restore(string? backupPath, string oldFilePath) {
        if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath)) return;
        try {
            File.Copy(backupPath, oldFilePath, true);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "恢复 Mod 备份失败：" + backupPath);
        }
    }

    private static void SafeDelete(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        } catch {
            // 清理失败不影响结果
        }
    }

}
