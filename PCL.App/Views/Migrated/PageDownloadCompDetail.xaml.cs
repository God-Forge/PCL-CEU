using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Resources;
using System.Windows;
using System.Windows.Controls;

namespace PCL.App.Views.Migrated;

public partial class PageDownloadCompDetail : PCL.App.Controls.MyPageRight {

    public string? ProjectName { get; set; }
    public string? ProjectDescription { get; set; }
    public string? ProjectUrl { get; set; }
    public string? WikiUrl { get; set; }
    public string? ProjectSlug { get; set; }
    public string? ProjectType { get; set; }

    public PageDownloadCompDetail() {
        InitializeComponent();
        BtnIntroWeb.Click += (_, _) => OpenProjectWebsite();
        BtnIntroWiki.Click += (_, _) => OpenWikiWebsite();
        BtnIntroCopy.Click += (_, _) => CopyProjectName();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            RefreshDisplay();
        };
    }

    public void SetProject(
        string? name,
        string? description = null,
        string? url = null,
        string? wikiUrl = null,
        string? slug = null,
        string? projectType = null) {
        ProjectName = name;
        ProjectDescription = description;
        ProjectUrl = url;
        WikiUrl = wikiUrl;
        ProjectSlug = slug;
        ProjectType = projectType;
        RefreshDisplay();
    }

    public async Task<bool> LoadProjectAsync(string? query, string? projectType = "mod") {
        if (string.IsNullOrWhiteSpace(query)) return false;
        try {
            var type = string.IsNullOrWhiteSpace(projectType) ? "mod" : projectType!.Trim();
            var kind = ParseKind(type);
            var result = await new ModrinthResourceService().SearchAsync(new ResourceSearchQuery {
                Text = query!.Trim(),
                Kind = kind,
                Limit = 1
            });
            if (result.Projects.Count == 0) {
                NotificationService.Info("没有找到匹配的项目。", "项目详情");
                return false;
            }
            var project = result.Projects[0];
            SetProject(
                project.Title,
                project.Description,
                project.ProjectUrl,
                null,
                project.Slug,
                type);
            return true;
        } catch (Exception ex) {
            NotificationService.Warning("加载项目详情失败：" + ex.Message, "项目详情");
            return false;
        }
    }

    private static ResourceKind ParseKind(string type) => type.ToLowerInvariant() switch {
        "resourcepack" => ResourceKind.ResourcePack,
        "shader" => ResourceKind.ShaderPack,
        "datapack" => ResourceKind.DataPack,
        "modpack" => ResourceKind.Modpack,
        _ => ResourceKind.Mod
    };

    public void RefreshDisplay() {
        if (!Dispatcher.CheckAccess()) {
            Dispatcher.Invoke(RefreshDisplay);
            return;
        }
        PanLoad.Visibility = Visibility.Collapsed;
        PanMain.Visibility = Visibility.Visible;
        HintAlternative.Visibility = Visibility.Collapsed;
        CardFilter.Visibility = Visibility.Collapsed;
        PanResults.Children.Clear();

        var name = string.IsNullOrWhiteSpace(ProjectName) ? "未选择项目" : ProjectName!;
        var description = string.IsNullOrWhiteSpace(ProjectDescription)
            ? "暂无可显示的项目详情。"
            : ProjectDescription!;
        BtnIntroWeb.Text = string.IsNullOrWhiteSpace(ProjectUrl) && string.IsNullOrWhiteSpace(ProjectSlug)
            ? "打开项目网页"
            : "转到项目网页";
        BtnIntroWiki.Visibility = string.IsNullOrWhiteSpace(WikiUrl) ? Visibility.Collapsed : Visibility.Visible;

        var card = new MyCard { Title = "项目详情", Margin = new Thickness(0, 0, 0, 15) };
        var panel = new StackPanel { Margin = new Thickness(20, 38, 18, 14) };
        panel.Children.Add(new TextBlock {
            Text = name,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock {
            Text = description,
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = System.Windows.Media.Brushes.Gray
        });
        if (!string.IsNullOrWhiteSpace(ProjectType) || !string.IsNullOrWhiteSpace(ProjectSlug)) {
            panel.Children.Add(new TextBlock {
                Text = (ProjectType ?? "") + (string.IsNullOrWhiteSpace(ProjectSlug) ? "" : " · " + ProjectSlug),
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.Gray
            });
        }
        card.Children.Add(panel);
        PanResults.Children.Add(card);

        var hint = new MyHint {
            Text = "当前为迁移占位详情页。可通过宿主页面传入项目信息，或使用上方的网页、复制按钮。",
            Theme = "Blue",
            Margin = new Thickness(0, 0, 0, 12)
        };
        PanResults.Children.Insert(0, hint);
    }

    private void OpenProjectWebsite() {
        var target = ResolveProjectUrl();
        if (string.IsNullOrWhiteSpace(target)) {
            NotificationService.Info("暂无可打开的项目网页地址。", "项目详情");
            return;
        }
        OpenWebsite(target!);
    }

    private void OpenWikiWebsite() {
        if (string.IsNullOrWhiteSpace(WikiUrl)) {
            NotificationService.Info("暂无可打开的百科页面地址。", "项目详情");
            return;
        }
        var target = WikiUrl!.Trim();
        if (int.TryParse(target, out var wikiId)) {
            target = "https://www.mcmod.cn/class/" + wikiId + ".html";
        }
        OpenWebsite(target);
    }

    private void CopyProjectName() {
        try {
            var name = string.IsNullOrWhiteSpace(ProjectName) ? ProjectSlug : ProjectName;
            if (string.IsNullOrWhiteSpace(name)) {
                NotificationService.Info("暂无可复制的项目名称。", "项目详情");
                return;
            }
            System.Windows.Clipboard.SetText(name);
            NotificationService.Info("已复制项目名称。", "项目详情");
        } catch (Exception ex) {
            NotificationService.Warning("复制项目名称失败：" + ex.Message, "项目详情");
        }
    }

    private string? ResolveProjectUrl() {
        if (!string.IsNullOrWhiteSpace(ProjectUrl)) return ProjectUrl;
        if (string.IsNullOrWhiteSpace(ProjectSlug)) return null;
        var type = string.IsNullOrWhiteSpace(ProjectType) ? "mod" : ProjectType!.Trim().ToLowerInvariant();
        return "https://modrinth.com/" + type + "/" + Uri.EscapeDataString(ProjectSlug!.Trim());
    }

    private static void OpenWebsite(string url) {
        try {
            if (!url.Contains("://", StringComparison.Ordinal)) url = "https://" + url.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
                NotificationService.Warning("网页地址无效：" + url, "项目详情");
                return;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        } catch (Exception ex) {
            NotificationService.Warning("打开网页失败：" + ex.Message, "项目详情");
        }
    }

}
