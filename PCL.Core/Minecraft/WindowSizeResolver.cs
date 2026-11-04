namespace PCL.Core.Minecraft;

/// <summary>游戏窗口尺寸。</summary>
public readonly record struct LaunchWindowSize(int Width, int Height);

/// <summary>
/// 根据窗口设置计算游戏窗口尺寸。由 VB 版 ModLaunch 的窗口尺寸逻辑迁移而来。
/// 取值：0 全屏、1 默认、2 与启动器一致、3 自定义、4 最大化。
/// </summary>
public static class WindowSizeResolver {

    public const int TypeFullscreen = 0;
    public const int TypeDefault = 1;
    public const int TypeFollowLauncher = 2;
    public const int TypeCustom = 3;
    public const int TypeMaximized = 4;

    public const int DefaultWidth = 854;
    public const int DefaultHeight = 480;
    public const int MinimumSize = 100;

    /// <summary>标题栏高度（DIP），跟随启动器尺寸时需要扣除。</summary>
    public const int TitleBarHeightDips = 30;

    public static bool IsFullscreen(int windowType) => windowType == TypeFullscreen;

    public static bool IsMaximized(int windowType) => windowType == TypeMaximized;

    /// <summary>
    /// 解析窗口尺寸。
    /// </summary>
    /// <param name="windowType">窗口类型。</param>
    /// <param name="customWidth">自定义宽度（仅类型 3 使用）。</param>
    /// <param name="customHeight">自定义高度（仅类型 3 使用）。</param>
    /// <param name="launcherWidth">启动器窗口宽度（像素）。</param>
    /// <param name="launcherHeight">启动器窗口高度（像素）。</param>
    /// <param name="dpiScale">DPI 缩放比例，影响标题栏扣除高度。</param>
    public static LaunchWindowSize Resolve(
        int windowType,
        int customWidth = DefaultWidth,
        int customHeight = DefaultHeight,
        int launcherWidth = DefaultWidth,
        int launcherHeight = DefaultHeight,
        double dpiScale = 1) {
        return windowType switch {
            TypeFollowLauncher => new LaunchWindowSize(
                Math.Max(MinimumSize, launcherWidth),
                Math.Max(MinimumSize, launcherHeight - (int)Math.Round(TitleBarHeightDips * Math.Max(0.1, dpiScale)))),
            TypeCustom => new LaunchWindowSize(
                Math.Max(MinimumSize, customWidth),
                Math.Max(MinimumSize, customHeight)),
            _ => new LaunchWindowSize(DefaultWidth, DefaultHeight)
        };
    }

}
