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
/// 按计划安装加载器：目前只有 Fabric 可以全自动完成（下载 profile 即可），
/// 其余需要外部安装器与 Java，汇总为待办提示而不是静默跳过。
/// </summary>
public sealed class LoaderInstallOrchestrator {

    private readonly IStringFetcher? _fetcher;

    public LoaderInstallOrchestrator(IStringFetcher? fetcher = null) {
        _fetcher = fetcher;
    }

    public async Task<LoaderInstallResult> InstallAsync(
        InstallTarget? target,
        string mcFolder,
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
        var installed = new List<string>();
        var pending = new List<string>();
        foreach (var step in plan.Steps) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!LoaderInstallPlanner.CanInstallAutomatically(step)) {
                pending.Add($"{step.Kind} {step.Version}：{step.Note}");
                continue;
            }
            try {
                var folder = await new FabricInstallService(_fetcher)
                    .InstallAsync(mcFolder, target!.MinecraftVersion, step.Version, null, cancellationToken)
                    .ConfigureAwait(false);
                installed.Add($"{step.Kind} {step.Version} → {Path.GetFileName(folder)}");
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Error(ex, "安装 Fabric 失败");
                pending.Add($"{step.Kind} {step.Version}：安装失败（{ex.Message}），请手动安装");
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

}
