using PCL.Core.Net;

namespace PCL.Core.Minecraft;

/// <summary>加载器安装的汇总结果。</summary>
public sealed class LoaderInstallResult {

    public bool Success { get; init; }

    /// <summary>已自动安装的加载器描述。</summary>
    public IReadOnlyList<string> Installed { get; init; } = [];

    /// <summary>需要用户手动完成的步骤（含原因）。</summary>
    public IReadOnlyList<string> Pending { get; init; } = [];

    public string Message { get; init; } = "";

}

/// <summary>
/// 按计划安装加载器。Fabric / LiteLoader / OptiFine 可以自动完成
/// （下载安装器并运行，或直接生成版本），Forge 系需要 install_profile 处理，暂列为待办。
/// </summary>
public sealed class LoaderInstallOrchestrator {

    private readonly IStringFetcher? _fetcher;
    private readonly IDownloadService? _download;

    public LoaderInstallOrchestrator(IStringFetcher? fetcher = null, IDownloadService? download = null) {
        _fetcher = fetcher;
        _download = download;
    }

    public async Task<LoaderInstallResult> InstallAsync(
        InstallTarget? target,
        string mcFolder,
        string? javaExePath = null,
        CancellationToken cancellationToken = default) {
        var plan = LoaderInstallPlanner.Plan(target);
        if (!plan.IsValid) {
            return new LoaderInstallResult { Success = false, Message = plan.Error ?? "加载器组合无效。" };
        }
        if (!plan.HasSteps) {
            return new LoaderInstallResult { Success = true, Message = "没有需要安装的加载器。" };
        }
        if (string.IsNullOrWhiteSpace(mcFolder) || !Directory.Exists(mcFolder)) {
            return new LoaderInstallResult { Success = false, Message = "Minecraft 目录不存在。" };
        }
        var mcVersion = target!.MinecraftVersion;
        var installed = new List<string>();
        var pending = new List<string>();
        var tempFolder = Path.Combine(Path.GetTempPath(), "pcl-loader-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(tempFolder);
            foreach (var step in plan.Steps) {
                cancellationToken.ThrowIfCancellationRequested();
                // 需要运行安装器的步骤必须有 Java
                if (step.Kind != LoaderKind.Fabric && string.IsNullOrWhiteSpace(javaExePath)) {
                    pending.Add($"{step.Kind} {step.Version}：未找到可用的 Java，无法运行安装器");
                    continue;
                }
                try {
                    var description = await InstallStepAsync(step, mcVersion, mcFolder, javaExePath, tempFolder, cancellationToken)
                        .ConfigureAwait(false);
                    if (description is null) {
                        pending.Add($"{step.Kind} {step.Version}：{step.Note}");
                    } else {
                        installed.Add($"{step.Kind} {step.Version} → {description}");
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) when (ex is not OutOfMemoryException) {
                    Logger.Error(ex, $"安装 {step.Kind} 失败");
                    pending.Add($"{step.Kind} {step.Version}：安装失败（{ex.Message}），请手动安装");
                }
            }
        } finally {
            try {
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
            } catch {
                // 临时目录清理失败不影响结果
            }
        }
        var message = installed.Count > 0 ? $"已安装 {installed.Count} 个加载器" : "没有自动安装任何加载器";
        if (pending.Count > 0) message += $"，{pending.Count} 个待手动完成";
        return new LoaderInstallResult {
            Success = pending.Count == 0,
            Installed = installed,
            Pending = pending,
            Message = message
        };
    }

    /// <summary>执行单个步骤；返回生成的版本目录名，无法自动完成时返回 <c>null</c>。</summary>
    private async Task<string?> InstallStepAsync(
        LoaderInstallStep step,
        string mcVersion,
        string mcFolder,
        string? javaExePath,
        string tempFolder,
        CancellationToken cancellationToken) {
        switch (step.Kind) {
            case LoaderKind.Fabric: {
                var folder = await new FabricInstallService(_fetcher)
                    .InstallAsync(mcFolder, mcVersion, step.Version, null, cancellationToken)
                    .ConfigureAwait(false);
                return Path.GetFileName(folder);
            }
            case LoaderKind.LiteLoader: {
                var fileName = $"liteloader-installer-{mcVersion}-{step.Version}.jar";
                var url = LiteLoaderInstaller.BuildDownloadUrl(mcVersion, fileName);
                var installer = await DownloadInstallerAsync(url, tempFolder, cancellationToken).ConfigureAwait(false);
                if (installer is null) return null;
                var result = await LiteLoaderInstaller
                    .InstallAsync(javaExePath!, installer, mcFolder, mcVersion, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return result.Success ? Path.GetFileName(result.VersionFolder) : null;
            }
            case LoaderKind.OptiFine: {
                var fileName = $"OptiFine_{mcVersion}_{step.Version}.jar";
                var expected = OptiFineInstallerName.BuildVersionName(fileName, mcVersion);
                if (expected is null) return null;
                var url = OptiFineInstallerName.BuildDownloadUrl(fileName);
                if (url is null) return null;
                var installer = await DownloadInstallerAsync(url, tempFolder, cancellationToken).ConfigureAwait(false);
                if (installer is null) return null;
                var result = await OptiFineInstaller
                    .InstallAsync(installer, mcFolder, javaExePath!, expected, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return result.Success ? Path.GetFileName(result.VersionFolder) : null;
            }
            default:
                // Forge 系需要 install_profile 与 processors，暂不自动处理
                return null;
        }
    }

    private async Task<string?> DownloadInstallerAsync(string url, string folder, CancellationToken cancellationToken) {
        if (_download is null) return null;
        var target = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jar");
        var request = new DownloadRequest { Url = url, TargetPath = target, Overwrite = true };
        var result = await _download.DownloadAsync(request, null, cancellationToken).ConfigureAwait(false);
        return result.Success && File.Exists(target) ? target : null;
    }

}
