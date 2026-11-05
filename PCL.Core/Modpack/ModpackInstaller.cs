using PCL.Core.Net;

namespace PCL.Core.Modpack;

/// <summary>整合包安装结果。</summary>
public sealed class ModpackInstallResult {

    public bool Success { get; init; }
    public int Downloaded { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public int Overrides { get; init; }
    public string Message { get; init; } = "";

    public static ModpackInstallResult Error(string message) => new() { Success = false, Message = message };

}

/// <summary>
/// 整合包安装：解析清单 → 下载文件 → 解压 overrides。
/// 由 VB 版 ModModpack 的安装流程迁移而来。
/// </summary>
public sealed class ModpackInstaller {

    private readonly IDownloadService _download;

    public ModpackInstaller(IDownloadService? download = null) {
        _download = download ?? new HttpDownloadService();
    }

    public async Task<ModpackInstallResult> InstallAsync(
        string zipPath,
        string instanceFolder,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        if (!File.Exists(zipPath)) return ModpackInstallResult.Error("整合包文件不存在。");
        if (string.IsNullOrWhiteSpace(instanceFolder)) {
            throw new ArgumentException("实例目录不能为空", nameof(instanceFolder));
        }
        var manifest = ModpackManifestParser.ReadFromZip(zipPath);
        if (manifest is null) return ModpackInstallResult.Error("无法识别整合包格式（需要 modrinth.index.json 或 manifest.json）。");

        var root = Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(instanceFolder);

        var downloaded = 0;
        var failed = 0;
        var skipped = 0;
        var total = manifest.Files.Count;
        for (var i = 0; i < total; i++) {
            var file = manifest.Files[i];
            cancellationToken.ThrowIfCancellationRequested();
            if (!file.HasDownload) {
                // CurseForge 清单只带项目/文件 ID，需要平台 API 解析下载地址
                skipped++;
                continue;
            }
            var target = Path.GetFullPath(Path.Combine(instanceFolder, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                failed++;
                Logger.Warn("整合包文件路径非法，已跳过：" + file.Path);
                continue;
            }
            var request = new DownloadRequest {
                Url = file.Url,
                TargetPath = target,
                Overwrite = true,
                MaxRetries = 2
            };
            try {
                var result = await _download.DownloadAsync(request, progress, cancellationToken).ConfigureAwait(false);
                if (result.Success) downloaded++;
                else failed++;
            } catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException) {
                failed++;
                Logger.Warn(ex, "整合包文件下载失败：" + file.Path);
            }
        }

        var overrides = 0;
        try {
            overrides = ModpackInstallService.InstallOverrides(zipPath, instanceFolder);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "整合包 overrides 解压失败");
        }

        return new ModpackInstallResult {
            Success = failed == 0,
            Downloaded = downloaded,
            Failed = failed,
            Skipped = skipped,
            Overrides = overrides,
            Message = $"下载 {downloaded} 个文件，失败 {failed} 个，跳过 {skipped} 个，解压 {overrides} 个覆盖文件。"
        };
    }

}
