using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MeloongCore;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Minecraft;

using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ListBox = System.Windows.Controls.ListBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Size = System.Windows.Size;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public partial class PageLaunchLeft : PCL.App.Controls.MyPageLeft {

    private IReadOnlyList<MinecraftInstanceLocation> _instances = Array.Empty<MinecraftInstanceLocation>();
    private MinecraftInstanceLocation? _current;
    private Process? _process;
    private CancellationTokenSource? _launchCts;
    private bool _launching;

    public PageLaunchLeft() {
        InitializeComponent();
        PanTypeOne.Visibility = Visibility.Collapsed;
        PanType.Visibility = Visibility.Collapsed;
        BtnLaunch.Click += BtnLaunch_Click;
        BtnVersion.Click += BtnVersion_Click;
        BtnCancel.Click += BtnCancel_Click;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _ = RefreshAsync();
        };
    }

    private async Task RefreshAsync() {
        SetLoadingState();
        try {
            var instances = await Task.Run(() => AppServices.MinecraftFolders.ScanAll());
            _instances = instances;
            _current = FindCurrent(instances);
            ApplySelection(_current);
            RefreshPageState();
        } catch (Exception ex) {
            Logger.Error(ex, "加载 Minecraft 实例失败", LogBehavior.Toast);
            _instances = Array.Empty<MinecraftInstanceLocation>();
            _current = null;
            ShowLoadFailed(ex);
        }
    }

    private static MinecraftInstanceLocation? FindCurrent(IReadOnlyList<MinecraftInstanceLocation> instances) {
        var selected = AppServices.SelectedInstanceName;
        if (!string.IsNullOrWhiteSpace(selected)) {
            var matched = instances.FirstOrDefault(instance =>
                string.Equals(instance.Version.Folder, selected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instance.Version.Id, selected, StringComparison.OrdinalIgnoreCase));
            if (matched is not null) return matched;
        }
        return instances.FirstOrDefault(instance => instance.Version.Error is null)
               ?? instances.FirstOrDefault();
    }

    private void ApplySelection(MinecraftInstanceLocation? instance) {
        _current = instance;
        AppServices.SelectedInstanceName = instance?.Version.Folder;
        AppServices.SelectedMinecraftPath = instance?.Folder.Path;
    }

    private void SetLoadingState() {
        BtnLaunch.Text = "正在加载";
        BtnLaunch.IsEnabled = false;
        BtnVersion.IsEnabled = false;
        BtnMore.Visibility = Visibility.Collapsed;
        LabVersion.Text = "正在加载版本列表，请稍候";
        PanLogin.Children.Clear();
        PanLogin.Children.Add(new TextBlock {
            Text = "正在读取 Minecraft 实例",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = System.Windows.Media.Brushes.Gray
        });
    }

    private void RefreshPageState() {
        PanLogin.Children.Clear();
        PanLogin.Children.Add(new TextBlock {
            Text = BuildLoginText(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 0, 20, 0),
            Foreground = System.Windows.Media.Brushes.Gray
        });

        BtnMore.Visibility = Visibility.Collapsed;
        BtnVersion.IsEnabled = !_launching;
        BtnLaunch.IsEnabled = !_launching && _current is not null && _current.Version.Error is null;

        if (_current is null) {
            BtnLaunch.Text = "无可用实例";
            LabVersion.Text = _instances.Count == 0 ? "未找到可用的游戏版本" : "所选版本不可用";
            return;
        }

        BtnLaunch.Text = "启动游戏";
        var name = GetDisplayName(_current);
        LabVersion.Text = _current.Version.Error is null
            ? $"{name} · {_current.Folder.Name}"
            : $"{name} · {_current.Version.Error}";
    }

    private static string BuildLoginText() {
        var login = AppServices.CurrentLogin;
        return login is null
            ? "离线登录 · " + GetOfflineName()
            : $"{login.Name} · {login.UserType}";
    }

    private static string GetOfflineName() {
        var name = Environment.UserName;
        return string.IsNullOrWhiteSpace(name) ? "Player" : name;
    }

    private static string GetDisplayName(MinecraftInstanceLocation instance) {
        return string.IsNullOrWhiteSpace(instance.Version.Id)
            ? instance.Version.Folder
            : instance.Version.Id;
    }

    private void ShowLoadFailed(Exception exception) {
        BtnLaunch.Text = "加载失败";
        BtnLaunch.IsEnabled = false;
        BtnVersion.IsEnabled = true;
        LabVersion.Text = "加载版本列表失败";
        PanLogin.Children.Clear();
        PanLogin.Children.Add(new TextBlock {
            Text = "无法读取 Minecraft 实例：" + exception.Message,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(20, 0, 20, 0),
            Foreground = System.Windows.Media.Brushes.Firebrick
        });
    }

    private async void BtnLaunch_Click(object sender, RoutedEventArgs e) {
        if (_launching || _current is null) return;
        var instance = _current;
        if (instance.Version.Error is not null) {
            NotificationService.Warning("当前实例不可用：" + instance.Version.Error, "启动");
            return;
        }

        _launching = true;
        var cts = new CancellationTokenSource();
        _launchCts = cts;
        BtnLaunch.IsEnabled = false;
        BtnVersion.IsEnabled = false;
        try {
            PrepareLaunchingUI(instance);
            var requiredMajor = instance.Version.JavaVersionMajor;
            var java = await Task.Run(() => JavaScanner.FindBest(
                JavaScanner.DefaultRoots(AppServices.Paths.Base, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                requiredMajor));
            if (cts.IsCancellationRequested) return;
            if (java is null) {
                ShowLaunchFailed("未找到可用的 Java，请先安装 Java 或在设置中配置。");
                return;
            }

            UpdateLaunchingStage("准备启动", 0.08);
            var login = AppServices.CurrentLogin ?? OfflineLoginService.Create(GetOfflineName());
            var instanceFolder = Path.Combine(instance.Folder.Versions, instance.Version.Folder);
            LaunchLogService.Clear();
            LaunchLogService.WriteLine($"[启动] 版本：{GetDisplayName(instance)}");
            LaunchLogService.WriteLine($"[启动] 目录：{instanceFolder}");
            LaunchLogService.WriteLine($"[启动] Java：{java.JavaExe}");

            UpdateLaunchingStage("构建启动参数", 0.2);
            var ramMb = LaunchParameterService.ResolveRamMb(instance);
            var (windowWidth, windowHeight) = LaunchParameterService.ResolveWindowSize();
            var windowType = LaunchParameterService.ResolveWindowType();
            var session = await Task.Run(() => LaunchService.Launch(
                instance.Folder.Path,
                instanceFolder,
                instance.Version.Folder,
                instance.Version.JsonPath,
                login,
                java.JavaExe,
                ramMb,
                windowWidth,
                windowHeight,
                windowType), cts.Token);
            var process = session.Process;
            if (cts.IsCancellationRequested) {
                TryKill(process);
                session.Dispose();
                return;
            }

            _process = process;
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => OnProcessExited(process);
            ShowLaunchSucceeded(process);
            await Task.Delay(900);
            if (!cts.IsCancellationRequested && ReferenceEquals(_process, process) && process.HasExited) {
                OnProcessExited(process);
            }
        } catch (OperationCanceledException) {
        } catch (Exception ex) {
            if (cts.IsCancellationRequested) return;
            Logger.Error(ex, "启动 Minecraft 失败", LogBehavior.Toast);
            ShowLaunchFailed(ex.Message);
        } finally {
            if (ReferenceEquals(_launchCts, cts)) {
                _launchCts = null;
                cts.Dispose();
            }
            _launching = false;
            BtnVersion.IsEnabled = true;
            BtnLaunch.IsEnabled = _current is not null && _current.Version.Error is null;
        }
    }

    private void PrepareLaunchingUI(MinecraftInstanceLocation instance) {
        LabLaunchingTitle.Text = "正在启动游戏";
        LabLaunchingName.Text = GetDisplayName(instance);
        LabLaunchingMethod.Text = BuildLoginText();
        LabLaunchingProgress.Text = "0.00 %";
        LabLaunchingDownload.Text = "0 ms";
        LabLaunchingDownload.Visibility = Visibility.Collapsed;
        LabLaunchingDownloadLeft.Visibility = Visibility.Collapsed;
        LabLaunchingProgress.Visibility = Visibility.Visible;
        LabLaunchingProgressLeft.Visibility = Visibility.Visible;
        PanLaunchingHint.Visibility = Visibility.Collapsed;
        PanLaunchingInfo.Width = double.NaN;
        ProgressLaunchingFinished.Width = new GridLength(0, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1, GridUnitType.Star);
        BtnCancel.Text = "取消";
        PanInput.Visibility = Visibility.Collapsed;
        PanInput.IsHitTestVisible = false;
        PanLaunching.Visibility = Visibility.Visible;
        PanLaunching.Opacity = 1;
        PanLaunching.IsHitTestVisible = true;
        if (PanLaunching.RenderTransform is System.Windows.Media.ScaleTransform scale) {
            scale.ScaleX = 1;
            scale.ScaleY = 1;
        }
        UpdateLaunchingStage("初始化", 0);
    }

    private void UpdateLaunchingStage(string stage, double progress) {
        progress = Math.Clamp(progress, 0, 1);
        LabLaunchingStage.Text = stage;
        LabLaunchingProgress.Text = (progress * 100).ToString("0.00") + " %";
        ProgressLaunchingFinished.Width = new GridLength(progress, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1 - progress, GridUnitType.Star);
    }

    private void ShowLaunchSucceeded(Process process) {
        LabLaunchingTitle.Text = "已启动游戏";
        LabLaunchingStage.Text = "启动完成";
        UpdateLaunchingStage("启动完成", 1);
        BtnCancel.Text = "返回";
        LaunchLogService.WriteLine($"[启动] 游戏进程已启动，PID {process.Id}");
    }

    private void ShowLaunchFailed(string message) {
        _process = null;
        LabLaunchingTitle.Text = "启动失败";
        LabLaunchingStage.Text = "启动失败";
        LabLaunchingProgress.Text = "—";
        ProgressLaunchingFinished.Width = new GridLength(0, GridUnitType.Star);
        ProgressLaunchingUnfinished.Width = new GridLength(1, GridUnitType.Star);
        BtnCancel.Text = "返回";
        PanInput.Visibility = Visibility.Collapsed;
        PanLaunching.Visibility = Visibility.Visible;
        PanLaunching.IsHitTestVisible = true;
        LaunchLogService.WriteLine("[启动] 失败：" + message);
        NotificationService.Error("启动失败：" + message, "启动");
    }

    private void OnProcessExited(Process process) {
        _ = DispatcherService.InvokeAsync(() => {
            if (!ReferenceEquals(_process, process)) return;
            _process = null;
            LabLaunchingTitle.Text = process.ExitCode == 0 ? "游戏已退出" : "游戏异常退出";
            LabLaunchingStage.Text = "退出码 " + process.ExitCode;
            LabLaunchingProgress.Text = "100.00 %";
            ProgressLaunchingFinished.Width = new GridLength(1, GridUnitType.Star);
            ProgressLaunchingUnfinished.Width = new GridLength(0, GridUnitType.Star);
            BtnCancel.Text = "返回";
        });
    }

    private static void TryKill(Process process) {
        try {
            if (!process.HasExited) process.Kill(true);
        } catch (Exception ex) {
            Logger.Warn(ex, "结束 Minecraft 进程失败");
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) {
        if (string.Equals(BtnCancel.Text, "取消", StringComparison.Ordinal)) {
            _launchCts?.Cancel();
            if (_process is not null) {
                TryKill(_process);
                _process = null;
            }
            LaunchLogService.WriteLine("[启动] 已取消启动");
        }
        ShowInput();
    }

    private void ShowInput() {
        PanLaunching.Visibility = Visibility.Collapsed;
        PanLaunching.IsHitTestVisible = false;
        PanInput.Visibility = Visibility.Visible;
        PanInput.IsHitTestVisible = true;
        BtnCancel.Text = "取消";
        RefreshPageState();
    }

    private void BtnVersion_Click(object sender, RoutedEventArgs e) {
        if (_launching) return;
        try {
            var instance = ShowInstancePicker();
            if (instance is null) return;
            ApplySelection(instance);
            RefreshPageState();
        } catch (Exception ex) {
            Logger.Error(ex, "打开版本选择窗口失败", LogBehavior.Toast);
            NotificationService.Error("打开版本选择窗口失败：" + ex.Message, "版本选择");
        }
    }

    private MinecraftInstanceLocation? ShowInstancePicker() {
        if (_instances.Count == 0) {
            NotificationService.Info("未找到可用的 Minecraft 实例。", "版本选择");
            return null;
        }

        var window = new Window {
            Title = "选择 Minecraft 版本",
            Width = 460,
            Height = 360,
            Owner = System.Windows.Application.Current?.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            ResizeMode = ResizeMode.NoResize
        };
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock {
            Text = "请选择要启动的 Minecraft 版本",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(label, 0);
        root.Children.Add(label);

        var list = new ListBox { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var instance in _instances) {
            var type = string.IsNullOrWhiteSpace(instance.Version.Type) ? "未知类型" : instance.Version.Type;
            var error = instance.Version.Error is null ? "" : " · " + instance.Version.Error;
            list.Items.Add(new ListBoxItem {
                Content = $"{GetDisplayName(instance)}    {type}    {instance.Folder.Name}{error}",
                Tag = instance,
                Padding = new Thickness(8, 6, 8, 6),
                ToolTip = Path.Combine(instance.Folder.Versions, instance.Version.Folder)
            });
        }
        list.SelectedIndex = _current is null
            ? (_instances.Count > 0 ? 0 : -1)
            : Math.Max(0, _instances.ToList().FindIndex(instance => ReferenceEquals(instance, _current)));
        list.MouseDoubleClick += (_, _) => {
            if (list.SelectedItem is not null) window.DialogResult = true;
        };
        Grid.SetRow(list, 1);
        root.Children.Add(list);

        var buttons = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = new MyButton {
            Text = "取消",
            Width = 80,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancel.Click += (_, _) => window.DialogResult = false;
        var ok = new MyButton {
            Text = "选择",
            Width = 80,
            Height = 32,
            IsDefault = true,
            ColorType = "Highlight"
        };
        ok.Click += (_, _) => {
            if (list.SelectedItem is not null) window.DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);

        window.Content = root;
        return window.ShowDialog() == true && list.SelectedItem is ListBoxItem item
            ? item.Tag as MinecraftInstanceLocation
            : null;
    }

}
