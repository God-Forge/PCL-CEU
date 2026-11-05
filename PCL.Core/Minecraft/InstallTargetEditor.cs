namespace PCL.Core.Minecraft;

/// <summary>
/// 安装目标的编辑状态：持有「初始目标」与「当前目标」，支持逐组件修改与恢复。
/// 对应 VB 版 PageDownloadModify 的编辑行为（ChangeState + BtnXxxRestore）。
/// </summary>
public sealed class InstallTargetEditor {

    private readonly InstallTarget _initial;

    public InstallTargetEditor(InstallTarget initial) {
        ArgumentNullException.ThrowIfNull(initial);
        _initial = initial;
        Current = initial;
    }

    /// <summary>打开编辑器时的初始目标，用于恢复。</summary>
    public InstallTarget Initial => _initial;

    /// <summary>当前编辑结果。</summary>
    public InstallTarget Current { get; private set; }

    /// <summary>当前所有组件的对比结果。</summary>
    public IReadOnlyList<TargetComparison> Comparisons => InstallTargetComparer.Compare(_initial, Current);

    public bool HasChanges => InstallTargetComparer.HasChanges(_initial, Current);

    /// <summary>可恢复的组件（初始有值且当前已改变）。</summary>
    public IReadOnlyList<TargetComparison> Restorable => Comparisons.Where(item => item.CanRestore).ToList();

    /// <summary>修改某个组件的值。空字符串会被当作清空。</summary>
    public void Set(InstallTargetComponent component, string? value) {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        Current = Current.With(component, normalized);
    }

    /// <summary>把某个组件恢复为初始值。</summary>
    public void Restore(InstallTargetComponent component) {
        Current = InstallTargetComparer.Restore(Current, _initial, component);
    }

    /// <summary>恢复全部组件。</summary>
    public void RestoreAll() {
        Current = InstallTargetComparer.RestoreAll(Current, _initial);
    }

    /// <summary>当前目标的一行描述。</summary>
    public string DescribeCurrent() => InstallTargetComparer.Describe(Current);

    /// <summary>初始目标的一行描述。</summary>
    public string DescribeInitial() => InstallTargetComparer.Describe(_initial);

}
