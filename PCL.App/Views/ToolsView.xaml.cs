using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PCL.App.Services;
using PCL.Core.Download;
using PCL.Core.Memory;
using PCL.Core.Tools;

namespace PCL.App.Views;

public partial class ToolsView : System.Windows.Controls.UserControl {

    public ToolsView() {
        InitializeComponent();
    }

    private void Luck_Click(object sender, RoutedEventArgs e) {
        var luck = DailyLuck.GetToday();
        OutputBox.Text = $"今日人品：{luck.Value} / 100\r\n{luck.Comment}";
    }

    private void Memory_Click(object sender, RoutedEventArgs e) {
        var result = MemoryOptimizer.Optimize(MemoryOptimizationKind.CurrentProcess);
        OutputBox.Text = $"{result.Message}\r\n释放：{result.FreedMB:F1} MB\r\n可用：{result.After.AvailableMB:F1} MB / {result.After.TotalMB:F1} MB";
    }

    private async void Speed_Click(object sender, RoutedEventArgs e) {
        OutputBox.Text = "正在测试下载源……";
        var results = await new DownloadSourceSpeedTester().TestAllAsync(DownloadSourceSpeedTester.DefaultTargets());
        OutputBox.Text = DownloadSourceSpeedTester.Format(results);
    }

    private void Environment_Click(object sender, RoutedEventArgs e) {
        var info = PCL.Core.Minecraft.AdvancedDiagnosticsService.CollectEnvironment(null);
        OutputBox.Text = $"系统：{info.Os}\r\n显卡：{info.Gpu}\r\nJava：{info.Java}";
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) {
        Process.Start(new ProcessStartInfo {
            FileName = AppServices.Paths.Pcl,
            UseShellExecute = true
        });
    }

}

