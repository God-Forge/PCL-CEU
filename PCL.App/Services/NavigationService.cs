using System.Windows;
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

}
