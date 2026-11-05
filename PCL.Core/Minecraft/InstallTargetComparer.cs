namespace PCL.Core.Minecraft;

/// <summary>安装目标里的单个组件。</summary>
public enum InstallTargetComponent {
    Minecraft,
    Forge,
    NeoForge,
    Fabric,
    LiteLoader,
    OptiFine
}

/// <summary>
/// 一次安装的「目标组成」：原版版本 + 一个主加载器 + 可选的光影/兼容层。
/// 对应 VB 版 PageDownloadModify 里的 InitialTarget / Current 两个快照。
/// </summary>
public sealed class InstallTarget {

    public string MinecraftVersion { get; init; } = "";
    public string? ForgeVersion { get; init; }
    public string? NeoForgeVersion { get; init; }
    public string? FabricVersion { get; init; }
    public string? LiteLoaderVersion { get; init; }
    public string? OptiFineVersion { get; init; }

    /// <summary>按组件读取版本号。</summary>
    public string? Get(InstallTargetComponent component) => component switch {
        InstallTargetComponent.Minecraft => MinecraftVersion,
        InstallTargetComponent.Forge => ForgeVersion,
        InstallTargetComponent.NeoForge => NeoForgeVersion,
        InstallTargetComponent.Fabric => FabricVersion,
        InstallTargetComponent.LiteLoader => LiteLoaderVersion,
        InstallTargetComponent.OptiFine => OptiFineVersion,
        _ => null
    };

    /// <summary>把某个组件设成新值（<c>null</c> 表示清空），返回新的目标。</summary>
    public InstallTarget With(InstallTargetComponent component, string? value) => new() {
        MinecraftVersion = component == InstallTargetComponent.Minecraft ? value ?? "" : MinecraftVersion,
        ForgeVersion = component == InstallTargetComponent.Forge ? value : ForgeVersion,
        NeoForgeVersion = component == InstallTargetComponent.NeoForge ? value : NeoForgeVersion,
        FabricVersion = component == InstallTargetComponent.Fabric ? value : FabricVersion,
        LiteLoaderVersion = component == InstallTargetComponent.LiteLoader ? value : LiteLoaderVersion,
        OptiFineVersion = component == InstallTargetComponent.OptiFine ? value : OptiFineVersion
    };

}

/// <summary>单个组件的对比结果。</summary>
public sealed record TargetComparison(
    InstallTargetComponent Component,
    string? Initial,
    string? Current,
    bool Changed) {

    /// <summary>可以恢复到初始值（初始有值，且与当前不同）。</summary>
    public bool CanRestore => Changed && !string.IsNullOrWhiteSpace(Initial);

}

/// <summary>
/// 安装目标的对比与恢复，对应 VB 版 PageDownloadModify 的 RefreshComparisons 与 BtnRestore。
/// </summary>
public static class InstallTargetComparer {

    /// <summary>按固定顺序对比两组目标。</summary>
    public static IReadOnlyList<TargetComparison> Compare(InstallTarget initial, InstallTarget current) {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(current);
        var components = new[] {
            InstallTargetComponent.Minecraft,
            InstallTargetComponent.Forge,
            InstallTargetComponent.NeoForge,
            InstallTargetComponent.Fabric,
            InstallTargetComponent.LiteLoader,
            InstallTargetComponent.OptiFine
        };
        var result = new List<TargetComparison>();
        foreach (var component in components) {
            var before = initial.Get(component);
            var after = current.Get(component);
            var changed = !string.Equals(before ?? "", after ?? "", StringComparison.OrdinalIgnoreCase);
            result.Add(new TargetComparison(component, before, after, changed));
        }
        return result;
    }

    /// <summary>把指定组件恢复为初始值。</summary>
    public static InstallTarget Restore(InstallTarget current, InstallTarget initial, InstallTargetComponent component) =>
        current.With(component, initial.Get(component));

    /// <summary>把所有变更恢复为初始值。</summary>
    public static InstallTarget RestoreAll(InstallTarget current, InstallTarget initial) {
        var result = current;
        foreach (var comparison in Compare(initial, current).Where(item => item.CanRestore)) {
            result = result.With(comparison.Component, comparison.Initial);
        }
        return result;
    }

    /// <summary>是否存在任何差异。</summary>
    public static bool HasChanges(InstallTarget initial, InstallTarget current) =>
        Compare(initial, current).Any(item => item.Changed);

    /// <summary>把目标描述成一行，例如「26.3 + Forge 47.2.0」。</summary>
    public static string Describe(InstallTarget target) {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrWhiteSpace(target.MinecraftVersion)) return "未选择版本";
        var parts = new List<string> { target.MinecraftVersion };
        void Append(string name, string? version) {
            if (!string.IsNullOrWhiteSpace(version)) parts.Add($"{name} {version}");
        }
        Append("Forge", target.ForgeVersion);
        Append("NeoForge", target.NeoForgeVersion);
        Append("Fabric", target.FabricVersion);
        Append("LiteLoader", target.LiteLoaderVersion);
        Append("OptiFine", target.OptiFineVersion);
        return string.Join(" + ", parts);
    }

    public static string Describe(InstallTargetComponent component) => component switch {
        InstallTargetComponent.Minecraft => "原版",
        InstallTargetComponent.Forge => "Forge",
        InstallTargetComponent.NeoForge => "NeoForge",
        InstallTargetComponent.Fabric => "Fabric",
        InstallTargetComponent.LiteLoader => "LiteLoader",
        InstallTargetComponent.OptiFine => "OptiFine",
        _ => component.ToString()
    };

}
