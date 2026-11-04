namespace PCL.Core.Ui;

/// <summary>
/// 游戏启动后启动器的行为，对应设置项 LaunchArgumentVisible。
/// </summary>
public enum LauncherVisibility {
    /// <summary>关闭启动器。</summary>
    CloseAfterLaunch,
    /// <summary>隐藏到托盘。</summary>
    HideAfterLaunch,
    /// <summary>最小化。</summary>
    MinimizeAfterLaunch,
    /// <summary>保持可见。</summary>
    KeepVisible
}

/// <summary>
/// 把设置值解析为启动器可见性行为。VB 版取值：0 关闭、2/3 隐藏、4 最小化、5 不变。
/// </summary>
public static class LauncherVisibilityResolver {

    public const int DefaultSetting = 5;

    public static LauncherVisibility Resolve(int setting) => setting switch {
        0 => LauncherVisibility.CloseAfterLaunch,
        2 or 3 => LauncherVisibility.HideAfterLaunch,
        4 => LauncherVisibility.MinimizeAfterLaunch,
        _ => LauncherVisibility.KeepVisible
    };

    /// <summary>游戏结束后是否需要恢复窗口（隐藏与最小化模式）。</summary>
    public static bool ShouldRestoreAfterExit(LauncherVisibility visibility) =>
        visibility is LauncherVisibility.HideAfterLaunch or LauncherVisibility.MinimizeAfterLaunch;

}
