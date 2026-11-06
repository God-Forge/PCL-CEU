namespace PCL.Core.Minecraft;

/// <summary>加载器种类。</summary>
public enum LoaderKind {
    Forge,
    NeoForge,
    Fabric,
    LiteLoader,
    OptiFine
}

/// <summary>一个安装步骤。</summary>
public sealed record LoaderInstallStep(LoaderKind Kind, string Version, int Order, string Note);

/// <summary>安装计划；存在冲突时 <see cref="Error"/> 非空。</summary>
public sealed class LoaderInstallPlan {

    public IReadOnlyList<LoaderInstallStep> Steps { get; init; } = [];
    public string? Error { get; init; }
    public bool IsValid => Error is null;
    public bool HasSteps => Steps.Count > 0;

}

/// <summary>
/// 从安装目标推导加载器安装计划，并处理互斥与顺序：
/// Fabric 与 Forge/NeoForge 不能同时安装；OptiFine 需要放在最后（它会生成独立版本）。
/// </summary>
public static class LoaderInstallPlanner {

    /// <summary>推导安装步骤。</summary>
    public static LoaderInstallPlan Plan(InstallTarget? target) {
        if (target is null) return new LoaderInstallPlan();
        var steps = new List<LoaderInstallStep>();
        if (!string.IsNullOrWhiteSpace(target.FabricVersion)) {
            steps.Add(new LoaderInstallStep(LoaderKind.Fabric, target.FabricVersion!, 1, "直接生成版本，无需安装器"));
        }
        if (!string.IsNullOrWhiteSpace(target.ForgeVersion)) {
            steps.Add(new LoaderInstallStep(LoaderKind.Forge, target.ForgeVersion!, 2, "需要下载并运行 Forge 安装器"));
        }
        if (!string.IsNullOrWhiteSpace(target.NeoForgeVersion)) {
            steps.Add(new LoaderInstallStep(LoaderKind.NeoForge, target.NeoForgeVersion!, 2, "需要下载并运行 NeoForge 安装器"));
        }
        if (!string.IsNullOrWhiteSpace(target.LiteLoaderVersion)) {
            steps.Add(new LoaderInstallStep(LoaderKind.LiteLoader, target.LiteLoaderVersion!, 3, "需要下载并运行 LiteLoader 安装器"));
        }
        if (!string.IsNullOrWhiteSpace(target.OptiFineVersion)) {
            steps.Add(new LoaderInstallStep(LoaderKind.OptiFine, target.OptiFineVersion!, 4, "需要 OptiFine 安装器，且应在其他加载器之后执行"));
        }
        var hasFabric = steps.Any(step => step.Kind == LoaderKind.Fabric);
        var hasForgeFamily = steps.Any(step => step.Kind is LoaderKind.Forge or LoaderKind.NeoForge);
        if (hasFabric && hasForgeFamily) {
            return new LoaderInstallPlan {
                Steps = steps.OrderBy(step => step.Order).ToList(),
                Error = "Fabric 与 Forge/NeoForge 不能同时安装，请只保留一个。"
            };
        }
        if (steps.Count(step => step.Kind == LoaderKind.Forge) + steps.Count(step => step.Kind == LoaderKind.NeoForge) > 1) {
            return new LoaderInstallPlan {
                Steps = steps,
                Error = "Forge 与 NeoForge 不能同时安装。"
            };
        }
        return new LoaderInstallPlan {
            Steps = steps.OrderBy(step => step.Order).ThenBy(step => step.Kind).ToList()
        };
    }

    /// <summary>该步骤能否自动完成（目前只有 Fabric 不需要外部安装器）。</summary>
    /// <summary>
    /// 能否自动完成：Fabric 直接生成版本；LiteLoader 与 OptiFine 可下载安装器运行；
    /// Forge 系需要 install_profile 与 processors，暂不自动。
    /// </summary>
    public static bool CanInstallAutomatically(LoaderInstallStep step) =>
        step.Kind is LoaderKind.Fabric or LoaderKind.LiteLoader or LoaderKind.OptiFine;

}
