using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Minecraft;
using PCL.Core.Mods;

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

public partial class PageInstanceModDisabled : PCL.App.Controls.MyPageRight {

    private MyCard? _disabledCard;
    private StackPanel? _disabledRows;
    private int _loadVersion;

    public PageInstanceModDisabled() {
        InitializeComponent();
        BtnDownload.Click += (_, _) => NotificationService.Info("请先在下载页面安装 Forge、Fabric 等 Mod 加载器。");
        BtnVersion.Click += (_, _) => NotificationService.Info("请在实例页面切换到一个已安装 Mod 加载器的版本。");
        Unloaded += (_, _) => _loadVersion++;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _ = RefreshDisabledModsAsync();
        };
    }

    private async Task RefreshDisabledModsAsync() {
        var version = ++_loadVersion;
        var rows = EnsureRows();
        rows.Children.Clear();
        var instancePath = ResolveInstancePath();
        if (instancePath is null) {
            AddMessage(rows, "请先选择一个 Minecraft 实例。");
            return;
        }

        try {
            var folder = InstanceManagementService.GetFolders(instancePath).Mods;
            var result = await Task.Run(() => ModScanner.Scan(folder));
            if (version != _loadVersion) return;
            var disabled = result.Mods.Where(mod => !mod.Enabled).ToList();
            if (disabled.Count == 0) {
                AddMessage(rows, "没有已禁用的 Mod。");
                return;
            }
            foreach (var mod in disabled) rows.Children.Add(BuildRow(mod));
        } catch (Exception ex) {
            if (version == _loadVersion) AddMessage(rows, "读取禁用 Mod 失败：" + ex.Message);
        }
    }

    private StackPanel EnsureRows() {
        if (_disabledRows is not null) return _disabledRows;
        _disabledRows = new StackPanel { Margin = new Thickness(20, 10, 20, 15) };
        _disabledCard = new MyCard {
            Title = "已禁用的 Mod",
            Margin = new Thickness(0, 15, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _disabledCard.Children.Add(_disabledRows);
        PanMain.Children.Add(_disabledCard);
        return _disabledRows;
    }

    private static void AddMessage(System.Windows.Controls.Panel panel, string message) {
        panel.Children.Add(new TextBlock {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.Gray,
            Margin = new Thickness(2, 3, 2, 3)
        });
    }

    private UIElement BuildRow(ModInfo mod) {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5), MinHeight = 32 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var display = string.IsNullOrWhiteSpace(mod.Name) ? mod.FileName : mod.Name;
        var info = new TextBlock {
            Text = $"{display}  ·  {mod.Version}  ·  {mod.Loader}\r\n{mod.FileName}",
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = mod.FilePath
        };
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var openButton = new MyButton {
            Text = "打开位置",
            Height = 26,
            MinWidth = 82,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        openButton.Click += (_, _) => OpenModLocation(mod);
        Grid.SetColumn(openButton, 1);
        grid.Children.Add(openButton);

        var enableButton = new MyButton {
            Text = "重新启用",
            Height = 26,
            MinWidth = 82,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ColorType = "Highlight"
        };
        enableButton.Click += (_, _) => ReEnable(mod);
        Grid.SetColumn(enableButton, 2);
        grid.Children.Add(enableButton);

        return new Border {
            BorderBrush = Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private static string? ResolveInstancePath() {
        try {
            if (!string.IsNullOrWhiteSpace(AppServices.SelectedInstanceName)) return AppServices.SelectedInstancePath;
            var first = AppServices.MinecraftFolders.ScanAll().FirstOrDefault();
            if (first is null) return null;
            AppServices.SelectedInstanceName = first.Version.Folder;
            AppServices.SelectedMinecraftPath = first.Folder.Path;
            return System.IO.Path.Combine(first.Folder.Versions, first.Version.Folder);
        } catch {
            return null;
        }
    }

    private void ReEnable(ModInfo mod) {
        try {
            var source = mod.FilePath;
            if (!File.Exists(source)) {
                NotificationService.Warning("文件已不存在。");
                _ = RefreshDisabledModsAsync();
                return;
            }
            if (!source.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) return;
            var target = source.Substring(0, source.Length - ".disabled".Length);
            if (File.Exists(target)) {
                NotificationService.Warning("已存在同名启用文件，请手动处理后重试。");
                return;
            }
            File.Move(source, target);
            NotificationService.Info("已重新启用 " + System.IO.Path.GetFileName(target));
            _ = RefreshDisabledModsAsync();
        } catch (Exception ex) {
            NotificationService.Error("重新启用 Mod 失败：" + ex.Message);
        }
    }

    private static void OpenModLocation(ModInfo mod) {
        try {
            if (!File.Exists(mod.FilePath)) {
                NotificationService.Warning("文件已不存在。");
                return;
            }
            Process.Start(new ProcessStartInfo {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{mod.FilePath}\"",
                UseShellExecute = true
            });
        } catch (Exception ex) {
            NotificationService.Error("打开文件位置失败：" + ex.Message);
        }
    }

}
