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

public partial class PageSelectLeft : PCL.App.Controls.MyPageLeft {

    private readonly List<MyListItem> _items = new();
    private IReadOnlyList<MinecraftInstanceLocation> _instances = Array.Empty<MinecraftInstanceLocation>();
    private MinecraftInstanceLocation? _selected;
    private TextBlock? _status;
    private MyButton? _btnLaunch;
    private MyButton? _btnOpen;
    private bool _loading;

    public PageSelectLeft() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _ = RefreshAsync();
        };
    }

    private async Task RefreshAsync() {
        if (_loading) return;
        _loading = true;
        try {
            var instances = await Task.Run(() => AppServices.MinecraftFolders.ScanAll());
            _instances = instances;
            _selected = FindSelected(instances);
            if (_selected is not null) {
                AppServices.SelectedInstanceName = _selected.Version.Folder;
                AppServices.SelectedMinecraftPath = _selected.Folder.Path;
            }
            BuildList();
        } catch (Exception ex) {
            Logger.Error(ex, "加载 Minecraft 实例列表失败", LogBehavior.Toast);
            BuildError(ex);
        } finally {
            _loading = false;
        }
    }

    private static MinecraftInstanceLocation? FindSelected(IReadOnlyList<MinecraftInstanceLocation> instances) {
        var selected = AppServices.SelectedInstanceName;
        if (!string.IsNullOrWhiteSpace(selected)) {
            var matched = instances.FirstOrDefault(instance =>
                string.Equals(instance.Version.Folder, selected, StringComparison.OrdinalIgnoreCase)
                || string.Equals(instance.Version.Id, selected, StringComparison.OrdinalIgnoreCase));
            if (matched is not null) return matched;
        }
        return instances.FirstOrDefault();
    }

    private void BuildList() {
        PanList.Children.Clear();
        _items.Clear();

        PanList.Children.Add(new TextBlock {
            Text = "Minecraft 实例",
            Margin = new Thickness(13, 18, 5, 8),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });

        if (_instances.Count == 0) {
            PanList.Children.Add(new TextBlock {
                Text = "未找到可用的 Minecraft 实例。",
                Margin = new Thickness(13, 4, 10, 8),
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.Gray
            });
        } else {
            foreach (var instance in _instances) {
                var item = new MyListItem {
                    Title = MinecraftInstancePageActions.GetDisplayName(instance),
                    Info = MinecraftInstancePageActions.GetShortInfo(instance),
                    Type = "Radio",
                    Height = 46,
                    Margin = new Thickness(7, 1, 7, 1),
                    Tag = instance,
                    ToolTip = MinecraftInstancePageActions.GetInstanceFolder(instance),
                    Checked = ReferenceEquals(instance, _selected)
                };
                item.Click += (_, _) => SelectInstance(instance);
                _items.Add(item);
                PanList.Children.Add(item);
            }
        }

        _status = new TextBlock {
            Margin = new Thickness(13, 10, 13, 8),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            Text = _selected is null ? "请选择一个实例。" : MinecraftInstancePageActions.Describe(_selected)
        };
        PanList.Children.Add(_status);

        BuildActions();
    }

    private void BuildActions() {
        var panel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 6, 8, 14)
        };
        _btnLaunch = new MyButton {
            Text = "启动",
            Height = 34,
            MinWidth = 64,
            Margin = new Thickness(2, 0, 4, 0),
            ColorType = "Highlight"
        };
        _btnLaunch.Click += async (_, _) => {
            var instance = _selected;
            var button = _btnLaunch;
            if (instance is null || button is null) return;
            button.IsEnabled = false;
            try {
                var process = await MinecraftInstancePageActions.LaunchAsync(instance);
                if (process is not null) {
                    SetStatus($"已启动 {MinecraftInstancePageActions.GetDisplayName(instance)}（PID {process.Id}）。");
                }
            } finally {
                button.IsEnabled = _selected is not null && _selected.Version.Error is null;
            }
        };
        _btnOpen = new MyButton {
            Text = "打开文件夹",
            Height = 34,
            MinWidth = 84,
            Margin = new Thickness(2, 0, 4, 0)
        };
        _btnOpen.Click += (_, _) => {
            if (_selected is not null) MinecraftInstancePageActions.OpenFolder(_selected);
        };
        var refresh = new MyButton {
            Text = "刷新",
            Height = 34,
            MinWidth = 64,
            Margin = new Thickness(2, 0, 2, 0)
        };
        refresh.Click += async (_, _) => {
            refresh.IsEnabled = false;
            try {
                await RefreshAsync();
            } finally {
                refresh.IsEnabled = true;
            }
        };
        panel.Children.Add(_btnLaunch);
        panel.Children.Add(_btnOpen);
        panel.Children.Add(refresh);
        PanList.Children.Add(panel);
        UpdateActions();
    }

    private void SelectInstance(MinecraftInstanceLocation instance) {
        _selected = instance;
        AppServices.SelectedInstanceName = instance.Version.Folder;
        AppServices.SelectedMinecraftPath = instance.Folder.Path;
        foreach (var item in _items) {
            item.Checked = ReferenceEquals(item.Tag, instance);
        }
        SetStatus(MinecraftInstancePageActions.Describe(instance));
        UpdateActions();
    }

    private void UpdateActions() {
        var enabled = _selected is not null && _selected.Version.Error is null;
        if (_btnLaunch is not null) _btnLaunch.IsEnabled = enabled;
        if (_btnOpen is not null) _btnOpen.IsEnabled = _selected is not null;
    }

    private void SetStatus(string text) {
        if (_status is not null) _status.Text = text;
    }

    private void BuildError(Exception exception) {
        PanList.Children.Clear();
        _items.Clear();
        _selected = null;
        PanList.Children.Add(new TextBlock {
            Text = "Minecraft 实例",
            Margin = new Thickness(13, 18, 5, 8),
            FontSize = 18,
            FontWeight = FontWeights.SemiBold
        });
        PanList.Children.Add(new TextBlock {
            Text = "读取实例失败：" + exception.Message,
            Margin = new Thickness(13, 4, 10, 8),
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.Firebrick
        });
        _status = new TextBlock {
            Margin = new Thickness(13, 10, 13, 8),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            Text = "请检查 Minecraft 文件夹是否存在且可访问。"
        };
        PanList.Children.Add(_status);
        BuildActions();
    }

}

internal static class MinecraftInstancePageActions {

    public static string GetDisplayName(MinecraftInstanceLocation instance) {
        return string.IsNullOrWhiteSpace(instance.Version.Id)
            ? instance.Version.Folder
            : instance.Version.Id;
    }

    public static string GetShortInfo(MinecraftInstanceLocation instance) {
        var type = string.IsNullOrWhiteSpace(instance.Version.Type) ? "未知类型" : instance.Version.Type;
        var error = instance.Version.Error is null ? "" : " · " + instance.Version.Error;
        return $"{type} · {instance.Folder.Name}{error}";
    }

    public static string Describe(MinecraftInstanceLocation instance) {
        var type = string.IsNullOrWhiteSpace(instance.Version.Type) ? "未知类型" : instance.Version.Type;
        var release = instance.Version.ReleaseTime?.ToString("yyyy-MM-dd") ?? "未知";
        var lines = new List<string> {
            GetDisplayName(instance),
            $"{type} · Java {instance.Version.JavaVersionMajor} · {instance.Version.LibrariesCount} libraries",
            $"发布时间：{release}",
            instance.Folder.Name,
            GetInstanceFolder(instance)
        };
        if (!string.IsNullOrWhiteSpace(instance.Version.Error)) lines.Add("错误：" + instance.Version.Error);
        return string.Join(Environment.NewLine, lines);
    }

    public static string GetInstanceFolder(MinecraftInstanceLocation instance) {
        return Path.Combine(instance.Folder.Versions, instance.Version.Folder);
    }

    public static void OpenFolder(MinecraftInstanceLocation instance) {
        try {
            var folder = GetInstanceFolder(instance);
            if (!Directory.Exists(folder)) folder = instance.Folder.Versions;
            if (!Directory.Exists(folder)) folder = instance.Folder.Path;
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        } catch (Exception ex) {
            Logger.Error(ex, "打开 Minecraft 实例文件夹失败", LogBehavior.Toast);
            NotificationService.Error("打开文件夹失败：" + ex.Message);
        }
    }

    public static async Task<Process?> LaunchAsync(MinecraftInstanceLocation instance) {
        if (instance.Version.Error is not null) {
            NotificationService.Warning("当前实例不可用：" + instance.Version.Error, "启动");
            return null;
        }
        try {
            var java = await Task.Run(() => JavaScanner.FindBest(
                JavaScanner.DefaultRoots(AppServices.Paths.Base, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                instance.Version.JavaVersionMajor));
            if (java is null) {
                NotificationService.Warning("未找到可用的 Java，请先安装 Java 或在设置中配置。", "启动");
                return null;
            }
            var login = AppServices.CurrentLogin ?? OfflineLoginService.Create(GetOfflineName());
            var instanceFolder = GetInstanceFolder(instance);
            LaunchLogService.Clear();
            LaunchLogService.WriteLine($"[启动] 版本：{GetDisplayName(instance)}");
            LaunchLogService.WriteLine($"[启动] 目录：{instanceFolder}");
            LaunchLogService.WriteLine($"[启动] Java：{java.JavaExe}");
            var process = await Task.Run(() => LaunchService.Launch(
                instance.Folder.Path,
                instanceFolder,
                instance.Version.Folder,
                instance.Version.JsonPath,
                login,
                java.JavaExe));
            NotificationService.Info($"已启动 {GetDisplayName(instance)}（PID {process.Id}）。", "启动");
            return process;
        } catch (Exception ex) {
            Logger.Error(ex, "启动 Minecraft 实例失败", LogBehavior.Toast);
            LaunchLogService.WriteLine("[启动] 失败：" + ex.Message);
            NotificationService.Error("启动失败：" + ex.Message, "启动");
            return null;
        }
    }

    private static string GetOfflineName() {
        var name = Environment.UserName;
        return string.IsNullOrWhiteSpace(name) ? "Player" : name;
    }

}
