using System.Windows;
using MeloongCore;
using PCL.Core.Ui;

namespace PCL.App.Services;

/// <summary>
/// 页面导航：把页面切换与历史记录集中到一处，供登录成功等流程回跳。
/// 实际切换动作由主窗口注入。
/// </summary>
public static class NavigationService {

    private static readonly NavigationHistory History = new();
    private static Action<UIElement>? _navigate;

    /// <summary>绑定主窗口的切换动作。</summary>
    public static void Initialize(Action<UIElement> navigate, string homeKey = "launch") {
        _navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
        History.Reset(homeKey);
    }

    public static bool CanGoBack => History.CanGoBack;

    /// <summary>记录并切换到指定页面。</summary>
    public static void Navigate(UIElement view, string key) {
        ArgumentNullException.ThrowIfNull(view);
        _navigate?.Invoke(view);
        History.Push(key);
    }

    /// <summary>回到启动页。登录成功后调用。</summary>
    public static void NavigateHome() {
        Navigate(new Views.LaunchPageView(), "launch");
    }

    /// <summary>
    /// 按页面键切换页面，供自定义事件（切换页面）使用。
    /// 未知的键会被忽略。
    /// </summary>
    public static void NavigateTo(string? key) {
        if (string.IsNullOrWhiteSpace(key)) return;
        UIElement? view = key.Trim().ToLowerInvariant() switch {
            "launch" or "home" or "启动" => new Views.LaunchPageView(),
            "download" or "下载" => new Views.DownloadPageView(),
            "instance" or "实例" => new Views.InstanceView(),
            "settings" or "setup" or "设置" => new Views.SetupPageView(),
            "tools" or "百宝箱" => new Views.ToolsView(),
            _ => null
        };
        if (view is null) {
            Logger.Warn("未知的页面键：" + key);
            return;
        }
        Navigate(view, key.Trim());
    }

}
