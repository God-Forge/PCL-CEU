using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.Net;
using PCL.Core.Update;

namespace PCL.App.Views;

public partial class AboutView : System.Windows.Controls.UserControl {

    public AboutView() {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        VersionLine.Text = $"当前版本：0.24.0-ceu · 程序集 {version}";
    }

    private void OpenSource_Click(object sender, RoutedEventArgs e) => Open("https://github.com/Meloong-Git/PCL");

    private void Donate_Click(object sender, RoutedEventArgs e) => Open("https://meloong.com/afd/a/LTCat");

    private async void Update_Click(object sender, RoutedEventArgs e) {
        try {
            var info = await UpdateCheckerService.CheckAsync(
                new HttpStringFetcher(),
                "https://api.github.com/repos/God-Forge/PCL-CEU/releases/latest",
                "0.24.0");
            System.Windows.MessageBox.Show(
                info.HasUpdate
                    ? $"发现新版本：{info.LatestVersion}\r\n{info.Url}"
                    : "当前已是最新版本。",
                "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
        } catch (Exception ex) {
            System.Windows.MessageBox.Show("检查更新失败：" + ex.Message, "检查更新", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void Open(string url) {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }

}


















