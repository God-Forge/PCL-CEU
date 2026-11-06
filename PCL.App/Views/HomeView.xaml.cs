using System.IO;
using System.Windows;
using System.Windows.Controls;
using PCL.App.Services;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Minecraft;

namespace PCL.App.Views;

public partial class HomeView : System.Windows.Controls.UserControl {

    private IReadOnlyList<MinecraftInstanceLocation> _instances = Array.Empty<MinecraftInstanceLocation>();

    public HomeView() {
        InitializeComponent();
        InstanceList.SelectionChanged += (_, _) => {
            var text = InstanceList.SelectedItem?.ToString();
            if (!string.IsNullOrWhiteSpace(text)) {
                AppServices.SelectedInstanceName = text.Split('·')[0].Trim();
            }
        };
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private async void Launch_Click(object sender, RoutedEventArgs e) {
        if (InstanceList.SelectedIndex < 0 || InstanceList.SelectedIndex >= _instances.Count) {
            System.Windows.MessageBox.Show("请先选择一个实例。", "启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var instance = _instances[InstanceList.SelectedIndex];
        var java = JavaSelectionService.Resolve(instance);
        if (java is null) {
            System.Windows.MessageBox.Show("未找到可用的 Java，请先安装 Java 或在设置中配置。", "启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try {
            var login = AppServices.CurrentLogin ?? OfflineLoginService.Create(Environment.UserName);
            var session = LaunchService.Launch(
                instance.Folder.Path,
                Path.Combine(instance.Folder.Versions, instance.Version.Folder),
                instance.Version.Folder,
                instance.Version.JsonPath,
                login,
                java.JavaExe,
                LaunchParameterService.ResolveRamMb(instance),
                LaunchParameterService.ResolveWindowSize().Width,
                LaunchParameterService.ResolveWindowSize().Height,
                LaunchParameterService.ResolveWindowType(),
                null,
                LaunchParameterService.ResolveWindowTitle(instance),
                LaunchParameterService.ResolveExtraJvmArguments(instance),
                LaunchParameterService.ResolveExtraGameArguments(instance),
                LaunchParameterService.ResolvePreLaunchCommand(instance),
                LaunchParameterService.ResolvePreLaunchWait(instance));
            var process = session.Process;
            StatusText.Text = $"已启动 {instance.Version.Id}，进程 {process.Id}，Java {java.Version}";
            session.Watcher.StateChanged += (_, state) => DispatcherService.Invoke(() => {
                StatusText.Text = state switch {
                    MinecraftGameState.Loading => $"正在启动 {instance.Version.Id}……",
                    MinecraftGameState.Running => $"{instance.Version.Id} 正在运行（PID {process.Id}）",
                    MinecraftGameState.Crashed => $"{instance.Version.Id} 已崩溃，请查看日志",
                    MinecraftGameState.Canceled => $"已取消启动 {instance.Version.Id}",
                    _ => $"游戏已退出，退出码 {SafeExitCode(process)}"
                };
            });
            await Task.Delay(1500);
            if (process.HasExited) {
                System.Windows.MessageBox.Show($"游戏进程已退出，退出码 {process.ExitCode}。请查看日志以获取详细原因。", "启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        } catch (Exception ex) {
            System.Windows.MessageBox.Show("启动失败：" + ex.Message, "启动", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e) {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
            FileName = AppServices.Paths.Base,
            UseShellExecute = true
        });
    }

    private static object SafeExitCode(System.Diagnostics.Process process) {
        try {
            return process.HasExited ? process.ExitCode : "未知";
        } catch (InvalidOperationException) {
            return "未知";
        }
    }

    private void Refresh() {
        try {
            _instances = AppServices.MinecraftFolders.ScanAll();
            InstanceList.ItemsSource = _instances.Select(instance =>
                $"{instance.Version.Id}  ·  {instance.Version.Type}  ·  {instance.Folder.Name}  ·  {instance.Version.LibrariesCount} libraries" +
                (instance.Version.Error is null ? "" : "  ·  " + instance.Version.Error)).ToList();
            StatusText.Text = $"共 {_instances.Count} 个实例 · {AppServices.MinecraftFolders.GetFolders().Count} 个 Minecraft 文件夹";
            EmptyPanel.Visibility = _instances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        } catch (Exception ex) {
            StatusText.Text = "读取实例失败：" + ex.Message;
        }
    }

}








