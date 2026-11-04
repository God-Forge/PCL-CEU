using System.Windows;
using MeloongCore;
using PCL.Core.Ui;

namespace PCL.App.Services;

/// <summary>
/// 游戏启动后与退出后对启动器窗口的处理。对应 VB 版 LaunchArgumentVisible。
/// </summary>
public static class WindowVisibilityService {

    /// <summary>设置键：启动器可见性。</summary>
    public const string SettingKey = "LaunchArgumentVisible";

    /// <summary>当前设置对应的行为。</summary>
    public static LauncherVisibility Resolve() =>
        LauncherVisibilityResolver.Resolve(AppServices.Settings.Get<int>(SettingKey));

    /// <summary>游戏启动成功后应用可见性设置。</summary>
    public static void ApplyAfterLaunch() {
        var visibility = Resolve();
        DispatcherService.Invoke(() => {
            switch (visibility) {
                case LauncherVisibility.CloseAfterLaunch:
                    Logger.Info("已根据设置，在启动后关闭启动器");
                    System.Windows.Application.Current?.Shutdown();
                    break;
                case LauncherVisibility.HideAfterLaunch:
                    Logger.Info("已根据设置，在启动后隐藏启动器");
                    MainWindow()?.Hide();
                    break;
                case LauncherVisibility.MinimizeAfterLaunch:
                    Logger.Info("已根据设置，在启动后最小化启动器");
                    if (MainWindow() is { } window) window.WindowState = WindowState.Minimized;
                    break;
            }
        });
    }

    /// <summary>游戏退出后恢复窗口（仅隐藏与最小化模式）。</summary>
    public static void RestoreAfterExit() {
        var visibility = Resolve();
        if (!LauncherVisibilityResolver.ShouldRestoreAfterExit(visibility)) return;
        DispatcherService.Invoke(() => {
            if (MainWindow() is not { } window) return;
            window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        });
    }

    private static Window? MainWindow() => System.Windows.Application.Current?.MainWindow;

}
