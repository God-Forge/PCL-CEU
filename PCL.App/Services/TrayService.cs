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
            Icon = System.Drawing.SystemIcons.Application,
            Text = "PCL CEU",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => DispatcherService.Invoke(() => {
            window.Show();
            window.Activate();
        });
    }

    public void Dispose() {
        if (_notifyIcon is null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

}
