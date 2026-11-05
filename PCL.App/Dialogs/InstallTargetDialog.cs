using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.Minecraft;
using PCL.Core.Modpack;

namespace PCL.App.Dialogs;

/// <summary>
/// 导入整合包前的目标确认与编辑：展示预检信息，允许逐组件修改并恢复初始值。
/// 对应 VB 版 PageDownloadModify 的对比与恢复行为。
/// </summary>
public sealed class InstallTargetDialog : Window {

    private static readonly InstallTargetComponent[] Components = [
        InstallTargetComponent.Minecraft,
        InstallTargetComponent.Forge,
        InstallTargetComponent.NeoForge,
        InstallTargetComponent.Fabric,
        InstallTargetComponent.LiteLoader,
        InstallTargetComponent.OptiFine
    ];

    private readonly InstallTargetEditor _editor;
    private readonly Dictionary<InstallTargetComponent, TextBox> _boxes = new();
    private readonly Dictionary<InstallTargetComponent, Button> _restoreButtons = new();

    /// <summary>用户在对话框中确认后的目标；取消时为 <c>null</c>。</summary>
    public InstallTarget? Result { get; private set; }

    public InstallTargetDialog(ModpackPreview preview) {
        ArgumentNullException.ThrowIfNull(preview);
        _editor = new InstallTargetEditor(preview.Target);
        Title = "确认安装目标";
        Width = 620;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));
        Content = BuildContent(preview);
        Loaded += (_, _) => Refresh();
    }

    private UIElement BuildContent(ModpackPreview preview) {
        var root = new StackPanel { Margin = new Thickness(20) };

        root.Children.Add(new TextBlock {
            Text = string.IsNullOrWhiteSpace(preview.Version) ? preview.Name : $"{preview.Name} {preview.Version}",
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        });
        root.Children.Add(new TextBlock {
            Text = $"将下载 {preview.FileCount} 个文件（必选 {preview.RequiredFileCount} 个）"
                   + (preview.TargetExists ? "　目标实例已存在，导入会写入该目录。" : "　目标实例将新建。"),
            FontSize = 12,
            Foreground = preview.TargetExists
                ? new SolidColorBrush(Color.FromRgb(190, 90, 20))
                : new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            Margin = new Thickness(0, 6, 0, 14),
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new TextBlock {
            Text = "组件（修改后点击“恢复”可回到整合包声明的版本）",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        root.Children.Add(new TextBlock {
            Text = "注意：整合包的文件始终按清单安装；这里调整的只是目标版本记录，"
                   + "对应加载器需导入完成后在实例设置中安装。",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 120, 30)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        for (var index = 0; index < Components.Length; index++) {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            AddRow(grid, Components[index], index);
        }
        root.Children.Add(grid);

        var buttons = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var restoreAll = new Button { Content = "全部恢复", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
        restoreAll.Click += (_, _) => {
            _editor.RestoreAll();
            Refresh();
        };
        var cancel = new Button { Content = "取消", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => Close();
        var confirm = new Button { Content = "开始导入", Width = 100, Height = 30, IsDefault = true };
        confirm.Click += (_, _) => {
            CommitEdits();
            Result = _editor.Current;
            Close();
        };
        buttons.Children.Add(restoreAll);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        root.Children.Add(buttons);
        return root;
    }

    private void AddRow(Grid grid, InstallTargetComponent component, int row) {
        var name = new TextBlock {
            Text = InstallTargetComparer.Describe(component),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13
        };
        Grid.SetRow(name, row);
        Grid.SetColumn(name, 0);
        grid.Children.Add(name);

        var initial = new TextBlock {
            Text = _editor.Initial.Get(component) ?? "—",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 140))
        };
        Grid.SetRow(initial, row);
        Grid.SetColumn(initial, 1);
        grid.Children.Add(initial);

        var box = new TextBox {
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Text = _editor.Current.Get(component) ?? ""
        };
        box.TextChanged += (_, _) => {
            var comparison = _editor.Comparisons.FirstOrDefault(item => item.Component == component);
            if (comparison is null) return;
            // 只更新按钮可用性，避免每次输入都重建状态
            if (_restoreButtons.TryGetValue(component, out var restore)) {
                restore.IsEnabled = !string.Equals(box.Text.Trim(), comparison.Initial ?? "", StringComparison.OrdinalIgnoreCase);
            }
        };
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 2);
        grid.Children.Add(box);
        _boxes[component] = box;

        var restoreButton = new Button { Content = "恢复", Width = 56, Height = 26, IsEnabled = false };
        restoreButton.Click += (_, _) => {
            _editor.Restore(component);
            Refresh();
        };
        Grid.SetRow(restoreButton, row);
        Grid.SetColumn(restoreButton, 3);
        grid.Children.Add(restoreButton);
        _restoreButtons[component] = restoreButton;
    }

    /// <summary>把界面上的编辑结果写回编辑器。</summary>
    private void CommitEdits() {
        foreach (var (component, box) in _boxes) {
            _editor.Set(component, box.Text);
        }
    }

    private void Refresh() {
        foreach (var (component, box) in _boxes) {
            var value = _editor.Current.Get(component) ?? "";
            if (!string.Equals(box.Text, value, StringComparison.Ordinal)) box.Text = value;
        }
        foreach (var (component, button) in _restoreButtons) {
            var comparison = _editor.Comparisons.FirstOrDefault(item => item.Component == component);
            button.IsEnabled = comparison is not null
                               && !string.Equals(_boxes[component].Text.Trim(), comparison.Initial ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

}
