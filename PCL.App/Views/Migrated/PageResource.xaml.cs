using PCL.App.Controls;
using System.Windows;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Resources;

namespace PCL.App.Views.Migrated;

public partial class PageResource : PCL.App.Controls.MyScrollViewer {

    public string? PageType { get; set; }
    public string? TypeName { get; set; }
    public string? TypeNameSpaced { get; set; }

    private int _offset;
    private int _lastCount;
    private readonly ResourceSearchService _search = ResourceSearchService.CreateDefault();

    public PageResource() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            BtnSearchRun.Click += (_, _) => _ = RunSearchAsync(0);
            BtnPageLeft.Click += (_, _) => _ = RunSearchAsync(Math.Max(0, _offset - 20));
            BtnPageRight.Click += (_, _) => _ = RunSearchAsync(_offset + 20);
            TextSearchName.KeyDown += (_, e) => {
                if (e.Key == System.Windows.Input.Key.Enter) _ = RunSearchAsync(0);
            };
            _ = RunSearchAsync(0);
        };
    }

    /// <summary>把资源安装到当前选中的实例。</summary>
    private async Task InstallProjectAsync(ResourceProject project) {
        var instancePath = AppServices.SelectedInstancePath;
        if (string.IsNullOrEmpty(instancePath)) {
            NotificationService.Warning("请先在实例页选择一个实例。", "安装资源");
            return;
        }
        var choice = DialogService.ShowText(
            $"将把《{project.Title}》安装到当前实例，是否继续？", "安装资源", "安装", "取消");
        if (choice != 1) return;
        try {
            var instance = string.IsNullOrWhiteSpace(AppServices.SelectedInstanceName)
                ? null
                : AppServices.MinecraftFolders.FindInstance(AppServices.SelectedInstanceName);
            var result = await new ResourceInstaller().InstallProjectAsync(
                project, instancePath, instance?.Version.Id);
            if (result.Success) {
                Logger.Info("已安装资源：" + string.Join(", ", result.Installed));
                NotificationService.Info(result.Message, "安装资源");
            } else {
                NotificationService.Error(result.Message, "安装资源");
            }
        } catch (Exception ex) {
            Logger.Error(ex, "安装资源失败");
            NotificationService.Error("安装失败：" + ex.Message, "安装资源");
        }
    }

    /// <summary>读取「版本」下拉：选了具体版本才作为筛选条件，默认项不过滤。</summary>
    private string? SelectedMinecraftVersion() {
        if (TextSearchVersion.SelectedIndex <= 0) return null;
        var text = TextSearchVersion.Text?.Trim();
        return string.IsNullOrWhiteSpace(text) || text.StartsWith("全部", StringComparison.Ordinal) ? null : text;
    }

    /// <summary>读取「类型」下拉的 Tag（形如 406/worldgen），空选项不过滤。</summary>
    private ResourceCategoryFilter? SelectedCategory() {
        if (ComboSearchTag.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return null;
        var filter = ResourceCategoryFilter.Parse(item.Tag as string ?? item.Tag?.ToString());
        return filter.IsEmpty ? null : filter;
    }

    /// <summary>读取「来源」下拉，全部来源时返回 null。</summary>
    private IReadOnlyList<ResourceSource>? SelectedSources() {
        if (ComboSearchSource.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return null;
        return item.Tag?.ToString() switch {
            "1" => [ResourceSource.CurseForge],
            "2" => [ResourceSource.Modrinth],
            _ => null
        };
    }

    private ResourceKind Kind => PageType switch {
        "ResourcePack" => ResourceKind.ResourcePack,
        "Shader" => ResourceKind.ShaderPack,
        "DataPack" => ResourceKind.DataPack,
        "ModPack" => ResourceKind.Modpack,
        _ => ResourceKind.Mod
    };

    private async Task RunSearchAsync(int offset) {
        _offset = Math.Max(0, offset);
        PanLoad.Visibility = Visibility.Visible;
        PanContent.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;
        try {
            var result = await _search.SearchAsync(new ResourceSearchQuery {
                Text = TextSearchName.Text.Trim(),
                Kind = Kind,
                Offset = _offset,
                Limit = 20,
                MinecraftVersion = SelectedMinecraftVersion(),
                Category = SelectedCategory(),
                Sources = SelectedSources()
            });
            var projects = result.Projects;
            _lastCount = projects.Count;
            PanProjects.Children.Clear();
            foreach (var project in projects) {
                var panel = new System.Windows.Controls.StackPanel();
                panel.Children.Add(new System.Windows.Controls.TextBlock {
                    Text = project.Title, FontSize = 15, FontWeight = FontWeights.SemiBold
                });
                panel.Children.Add(new System.Windows.Controls.TextBlock {
                    Text = project.Description, Foreground = System.Windows.Media.Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4)
                });
                panel.Children.Add(new System.Windows.Controls.TextBlock {
                    Text = $"{project.Author} · {project.Downloads:N0} 次下载 · {project.Source}",
                    FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray
                });
                var open = new PCL.App.Controls.MyButton {
                    Text = "打开页面", Height = 28, Width = 90,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                var projectUrl = project.ProjectUrl;
                open.Click += (_, _) => {
                    if (string.IsNullOrEmpty(projectUrl)) return;
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = projectUrl,
                        UseShellExecute = true
                    });
                };
                panel.Children.Add(open);
                if (ResourceInstallPaths.CanInstallAsFile(project.Kind)) {
                    var install = new PCL.App.Controls.MyButton {
                        Text = "安装", Height = 28, Width = 90,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        Margin = new Thickness(90, 8, 0, 0)
                    };
                    install.Click += async (_, _) => await InstallProjectAsync(project);
                    panel.Children.Add(install);
                }
                PanProjects.Children.Add(new System.Windows.Controls.Border {
                    Background = System.Windows.Media.Brushes.White,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(14),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = panel
                });
            }
            LabPage.Text = (_offset / 20 + 1).ToString();
            BtnPageLeft.IsEnabled = _offset > 0;
            BtnPageRight.IsEnabled = _lastCount >= 20;
            PanLoad.Visibility = Visibility.Collapsed;
            PanContent.Visibility = Visibility.Visible;
            if (projects.Count == 0) {
                HintError.Text = "没有找到匹配的项目。"
                                 + (result.HasFailure ? Environment.NewLine + "部分来源不可用：" + string.Join("；", result.Failures) : "");
                HintError.Theme = "Yellow";
                HintError.Visibility = Visibility.Visible;
            } else if (result.HasFailure) {
                HintError.Text = "部分来源不可用：" + string.Join("；", result.Failures);
                HintError.Theme = "Yellow";
                HintError.Visibility = Visibility.Visible;
            }
        } catch (Exception ex) {
            PanLoad.Visibility = Visibility.Collapsed;
            PanContent.Visibility = Visibility.Visible;
            HintError.Text = "搜索失败：" + ex.Message;
            HintError.Theme = "Red";
            HintError.Visibility = Visibility.Visible;
        }
    }

}




