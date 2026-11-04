using PCL.App.Controls;
using System.Windows;
using PCL.App.Services;
using PCL.Core.Net;

namespace PCL.App.Views.Migrated;

public partial class PageResource : PCL.App.Controls.MyScrollViewer {

    public string? PageType { get; set; }
    public string? TypeName { get; set; }
    public string? TypeNameSpaced { get; set; }

    private int _offset;
    private int _lastCount;

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

    private string ProjectType => PageType switch {
        "ResourcePack" => "resourcepack",
        "Shader" => "shader",
        "DataPack" => "datapack",
        "ModPack" => "modpack",
        _ => "mod"
    };

    private async Task RunSearchAsync(int offset) {
        _offset = Math.Max(0, offset);
        PanLoad.Visibility = Visibility.Visible;
        PanContent.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;
        try {
            var projects = await new ModrinthSearchService().SearchAsync(TextSearchName.Text.Trim(), ProjectType, _offset);
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
                    Text = $"{project.Author} · {project.Downloads:N0} 次下载",
                    FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray
                });
                var open = new PCL.App.Controls.MyButton {
                    Text = "打开页面", Height = 28, Width = 90,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                open.Click += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = $"https://modrinth.com/{ProjectType}/{project.Slug}",
                    UseShellExecute = true
                });
                panel.Children.Add(open);
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
                HintError.Text = "没有找到匹配的项目。";
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




