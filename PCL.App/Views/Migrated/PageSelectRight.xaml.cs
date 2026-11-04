using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MeloongCore;
using PCL.App.Controls;
using PCL.App.Services;
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

public partial class PageSelectRight : PCL.App.Controls.MyPageRight {

    private IReadOnlyList<MinecraftInstanceLocation> _instances = Array.Empty<MinecraftInstanceLocation>();
    private MinecraftInstanceLocation? _selected;
    private TextBlock? _status;
    private bool _loading;
    private bool _loadFailed;

    public PageSelectRight() {
        InitializeComponent();
        BtnEmptyDownload.Click += BtnEmptyDownload_Click;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _ = RefreshAsync();
        };
    }

    private async Task RefreshAsync() {
        if (_loading) return;
        _loading = true;
        BeginLoading();
        try {
            var instances = await Task.Run(() => AppServices.MinecraftFolders.ScanAll());
            _instances = instances;
            _selected = FindSelected(instances);
            if (_selected is not null) {
                AppServices.SelectedInstanceName = _selected.Version.Folder;
                AppServices.SelectedMinecraftPath = _selected.Folder.Path;
            }
            _loadFailed = false;
            Render(true);
        } catch (Exception ex) {
            Logger.Error(ex, "加载 Minecraft 实例列表失败", LogBehavior.Toast);
            _loadFailed = true;
            RenderError(ex);
        } finally {
            _loading = false;
            PanLoad.Visibility = Visibility.Collapsed;
            PanAllBack.Visibility = Visibility.Visible;
        }
    }

    private void BeginLoading() {
        PanAllBack.Visibility = Visibility.Collapsed;
        PanLoad.Visibility = Visibility.Visible;
        PanEmpty.Visibility = Visibility.Collapsed;
        PanBack.Visibility = Visibility.Visible;
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

    private void Render(bool scrollToHome) {
        var offset = PanBack.VerticalOffset;
        PanMain.Children.Clear();
        _status = null;

        if (_instances.Count == 0) {
            PanBack.Visibility = Visibility.Collapsed;
            PanEmpty.Visibility = Visibility.Visible;
            LabEmptyTitle.Text = "无可用版本";
            LabEmptyContent.Text = "未找到任何版本的游戏，请先下载任意版本的游戏。" + Environment.NewLine
                + "若有已存在的游戏，请先在启动页添加 .minecraft 文件夹。";
            BtnEmptyDownload.Text = "下载游戏";
            BtnEmptyDownload.Visibility = Visibility.Visible;
            return;
        }

        PanBack.Visibility = Visibility.Visible;
        PanEmpty.Visibility = Visibility.Collapsed;
        PanMain.Children.Add(BuildInstanceCard());
        if (_selected is not null) PanMain.Children.Add(BuildDetailCard(_selected));
        _status = new TextBlock {
            Margin = new Thickness(2, 0, 2, 10),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = Brushes.Gray,
            Text = _selected is null ? "请选择一个实例。" : "当前选择：" + MinecraftInstancePageActions.GetDisplayName(_selected)
        };
        PanMain.Children.Add(_status);
        PanMain.Children.Add(BuildActions());

        if (scrollToHome) PanBack.ScrollToHome();
        else PanBack.ScrollToVerticalOffset(offset);
    }

    private UIElement BuildInstanceCard() {
        var card = new MyCard {
            Title = $"实例列表 ({_instances.Count})",
            Margin = new Thickness(0, 0, 0, 15)
        };
        var panel = new StackPanel { Margin = new Thickness(12, 8, 12, 12) };
        foreach (var instance in _instances) {
            var item = new MyListItem {
                Title = MinecraftInstancePageActions.GetDisplayName(instance),
                Info = MinecraftInstancePageActions.GetShortInfo(instance),
                Type = "Radio",
                Height = 44,
                Margin = new Thickness(3, 1, 3, 1),
                Tag = instance,
                ToolTip = MinecraftInstancePageActions.GetInstanceFolder(instance),
                Checked = ReferenceEquals(instance, _selected)
            };
            item.Click += (_, _) => SelectInstance(instance);
            panel.Children.Add(item);
        }
        card.Children.Add(panel);
        return card;
    }

    private UIElement BuildDetailCard(MinecraftInstanceLocation instance) {
        var card = new MyCard {
            Title = "版本信息",
            Margin = new Thickness(0, 0, 0, 15)
        };
        var panel = new StackPanel { Margin = new Thickness(20, 8, 20, 16) };
        AddDetailRow(panel, "版本", MinecraftInstancePageActions.GetDisplayName(instance));
        AddDetailRow(panel, "类型", string.IsNullOrWhiteSpace(instance.Version.Type) ? "未知类型" : instance.Version.Type);
        AddDetailRow(panel, "Minecraft 文件夹", instance.Folder.Name);
        AddDetailRow(panel, "发布时间", instance.Version.ReleaseTime?.ToString("yyyy-MM-dd HH:mm") ?? "未知");
        AddDetailRow(panel, "Java 版本", instance.Version.JavaVersionMajor <= 0 ? "未知" : instance.Version.JavaVersionMajor.ToString());
        AddDetailRow(panel, "依赖库", instance.Version.LibrariesCount + " 个");
        AddDetailRow(panel, "继承版本", string.IsNullOrWhiteSpace(instance.Version.InheritsFrom) ? "无" : instance.Version.InheritsFrom);
        AddDetailRow(panel, "实例目录", MinecraftInstancePageActions.GetInstanceFolder(instance));
        if (!string.IsNullOrWhiteSpace(instance.Version.Error)) {
            panel.Children.Add(new TextBlock {
                Text = "状态：" + instance.Version.Error,
                Margin = new Thickness(0, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Firebrick
            });
        }
        card.Children.Add(panel);
        return card;
    }

    private static void AddDetailRow(System.Windows.Controls.Panel panel, string label, string value) {
        var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var title = new TextBlock {
            Text = label,
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(title, 0);
        row.Children.Add(title);
        var content = new TextBlock {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(content, 1);
        row.Children.Add(content);
        panel.Children.Add(row);
    }

    private UIElement BuildActions() {
        var panel = new StackPanel {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(2, 0, 2, 16)
        };
        var launch = new MyButton {
            Text = "启动",
            Height = 34,
            MinWidth = 80,
            Margin = new Thickness(0, 0, 8, 0),
            ColorType = "Highlight",
            IsEnabled = _selected is not null && _selected.Version.Error is null
        };
        launch.Click += async (_, _) => {
            var instance = _selected;
            if (instance is null) return;
            launch.IsEnabled = false;
            try {
                var process = await MinecraftInstancePageActions.LaunchAsync(instance);
                if (process is not null && _status is not null) {
                    _status.Text = $"已启动 {MinecraftInstancePageActions.GetDisplayName(instance)}（PID {process.Id}）。";
                }
            } finally {
                launch.IsEnabled = _selected is not null && _selected.Version.Error is null;
            }
        };
        var open = new MyButton {
            Text = "打开文件夹",
            Height = 34,
            MinWidth = 104,
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = _selected is not null
        };
        open.Click += (_, _) => {
            if (_selected is not null) MinecraftInstancePageActions.OpenFolder(_selected);
        };
        var refresh = new MyButton {
            Text = "刷新",
            Height = 34,
            MinWidth = 80
        };
        refresh.Click += async (_, _) => {
            refresh.IsEnabled = false;
            try {
                await RefreshAsync();
            } finally {
                refresh.IsEnabled = true;
            }
        };
        panel.Children.Add(launch);
        panel.Children.Add(open);
        panel.Children.Add(refresh);
        return panel;
    }

    private void SelectInstance(MinecraftInstanceLocation instance) {
        _selected = instance;
        AppServices.SelectedInstanceName = instance.Version.Folder;
        AppServices.SelectedMinecraftPath = instance.Folder.Path;
        Render(false);
    }

    private void RenderError(Exception exception) {
        PanMain.Children.Clear();
        _status = null;
        PanBack.Visibility = Visibility.Collapsed;
        PanEmpty.Visibility = Visibility.Visible;
        LabEmptyTitle.Text = "读取失败";
        LabEmptyContent.Text = "无法读取 Minecraft 实例：" + exception.Message;
        BtnEmptyDownload.Text = "重试";
        BtnEmptyDownload.Visibility = Visibility.Visible;
    }

    private void BtnEmptyDownload_Click(object sender, RoutedEventArgs e) {
        if (_loadFailed) {
            _ = RefreshAsync();
            return;
        }
        NotificationService.Info("请先在下载页面安装一个 Minecraft 版本。", "下载游戏");
    }

}
