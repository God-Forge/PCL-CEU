using System.Windows;
using Forms = System.Windows.Forms;

namespace PCL.App.Services;

/// <summary>
/// 系统托盘入口：显示主窗口与退出应用。
/// </summary>
public sealed class TrayService : IDisposable {

    private Forms.NotifyIcon? _notifyIcon;

    public void Initialize(Window window) {
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("显示 PCL");
        showItem.Click += (_, _) => DispatcherService.Invoke(() => {
            window.Show();
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        });
        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => DispatcherService.Invoke(() => System.Windows.Application.Current.Shutdown());
        menu.Items.Add(showItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new Forms.NotifyIcon {
            Icon = LoadAppIcon() ?? System.Drawing.SystemIcons.Application,
            Text = "PCL CEU",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => DispatcherService.Invoke(() => {
            window.Show();
            window.Activate();
        });
    }

    /// <summary>从嵌入资源读取应用图标；失败时返回 null，由调用方回退到系统默认。</summary>
    private static System.Drawing.Icon? LoadAppIcon() {
        try {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute));
            if (resource?.Stream is null) return null;
            using var stream = resource.Stream;
            return new System.Drawing.Icon(stream);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            MeloongCore.Logger.Warn(ex, "加载托盘图标失败，回退系统默认");
            return null;
        }
    }

    public void Dispose() {
        if (_notifyIcon is null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

}
