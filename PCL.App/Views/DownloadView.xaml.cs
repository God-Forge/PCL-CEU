using System.Windows;
using System.Windows.Controls;
using PCL.App.Services;
using PCL.Core.Minecraft;
using PCL.Core.Net;

namespace PCL.App.Views;

public partial class DownloadView : System.Windows.Controls.UserControl {

    public DownloadView() {
        InitializeComponent();
    }

    private async void Install_Click(object sender, RoutedEventArgs e) {
        var version = VersionBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(version)) {
            AppendLog("请输入版本号。");
            return;
        }
        InstallButton.IsEnabled = false;
        Progress.Value = 0;
        AppendLog("开始安装原版 " + version);
        try {
            var progress = new Progress<NetLoaderProgress>(value => {
                Progress.Value = value.FilesTotal > 0 ? value.FilesCompleted * 100d / value.FilesTotal : 0;
                if (value.FilesCompleted == value.FilesTotal && value.FilesTotal > 0) AppendLog("文件下载完成。");
            });
            var folder = await new VanillaInstallService().InstallAsync(AppServices.SelectedMinecraftFolder.Path, version, progress);
            AppendLog("安装完成：" + folder);
            Progress.Value = 100;
        } catch (Exception ex) {
            AppendLog("安装失败：" + ex.Message);
        } finally {
            InstallButton.IsEnabled = true;
        }
    }

    private void AppendLog(string text) {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

}


