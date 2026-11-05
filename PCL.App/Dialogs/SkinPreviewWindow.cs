using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Login;
using PCL.Core.Minecraft;

namespace PCL.App.Dialogs;

/// <summary>
/// 皮肤预览窗口：展示头部、全身，以及有披风时的披风渲染。
/// 图片加载失败时显示占位文字，而不是空白。
/// </summary>
public sealed class SkinPreviewWindow : Window {

    private readonly StackPanel _panel = new() {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public SkinPreviewWindow(LoginProfile? profile) {
        Title = string.IsNullOrWhiteSpace(profile?.Name) ? "皮肤预览" : $"皮肤预览 - {profile!.Name}";
        Width = 480;
        Height = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));
        Content = new ScrollViewer {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _panel,
            Padding = new Thickness(16)
        };
        Loaded += (_, _) => LoadImages(profile);
    }

    private async void LoadImages(LoginProfile? profile) {
        // 先尝试本地渲染：有缓存时断网也能看
        byte[]? png = null;
        try {
            png = await SkinCacheService.DownloadAsync(AppServices.Paths.Pcl, profile?.Uuid);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "获取皮肤图失败");
        }
        if (png is not null) {
            if (SkinLocalRenderer.RenderAvatar(png, 96) is { } localAvatar) {
                _panel.Children.Add(BuildColumnFromSource("头像（本地）", localAvatar));
            }
            if (SkinLocalRenderer.RenderBody(png, 6) is { } localBody) {
                _panel.Children.Add(BuildColumnFromSource("全身（本地）", localBody));
            }
        }
        var images = SkinUrlBuilder.PreviewImages(profile?.Uuid, hasCape: true);
        if (images.Count == 0 && _panel.Children.Count == 0) {
            _panel.Children.Add(new TextBlock {
                Text = "当前账号没有可展示的皮肤（需要有效的 UUID）。",
                Foreground = Brushes.Gray,
                FontSize = 13
            });
            return;
        }
        foreach (var (name, url) in images) {
            _panel.Children.Add(BuildColumn(name, url));
        }
    }

    /// <summary>用已渲染好的位图构建一列。</summary>
    private UIElement BuildColumnFromSource(string name, System.Windows.Media.ImageSource source) {
        var image = new Image {
            Width = 120,
            Height = 220,
            Stretch = Stretch.Uniform,
            Source = source,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var caption = new TextBlock {
            Text = name,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
        };
        var stack = new StackPanel { Margin = new Thickness(10, 0, 10, 0), Width = 130 };
        stack.Children.Add(image);
        stack.Children.Add(caption);
        return stack;
    }

    private UIElement BuildColumn(string name, string url) {
        var image = new Image {
            Width = 120,
            Height = 220,
            Stretch = Stretch.Uniform,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var caption = new TextBlock {
            Text = name,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
        };
        var stack = new StackPanel { Margin = new Thickness(10, 0, 10, 0), Width = 130 };
        stack.Children.Add(image);
        stack.Children.Add(caption);
        _ = LoadImageAsync(image, caption, name, url);
        return stack;
    }

    private async Task LoadImageAsync(Image image, TextBlock caption, string name, string url) {
        try {
            var source = await SkinImageService.LoadAsync(url);
            if (source is null) {
                caption.Text = name + "（加载失败）";
                return;
            }
            image.Source = source;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "加载皮肤预览失败：" + url);
            caption.Text = name + "（加载失败）";
        }
    }

}
