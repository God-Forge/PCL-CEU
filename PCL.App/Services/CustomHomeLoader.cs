using System.IO;
using System.Windows;
using System.Windows.Markup;
using MeloongCore;

namespace PCL.App.Services;

/// <summary>
/// 加载用户自定义主页（PCL\Custom.xaml）。
/// 解析失败时回退到默认启动页，绝不因为用户的 XAML 出错而让启动器打不开。
/// </summary>
public static class CustomHomeLoader {

    /// <summary>自定义主页文件路径。</summary>
    public static string ResolvePath(string pclRoot) => Path.Combine(pclRoot, "Custom.xaml");

    /// <summary>是否存在自定义主页文件。</summary>
    public static bool Exists(string pclRoot) => File.Exists(ResolvePath(pclRoot));

    /// <summary>
    /// 尝试加载自定义主页。文件不存在、内容为空或解析失败都返回 <c>null</c>。
    /// </summary>
    public static UIElement? TryLoad(string pclRoot) {
        var path = ResolvePath(pclRoot);
        if (!File.Exists(path)) return null;
        try {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return null;
            var element = XamlReader.Parse(text);
            if (element is not UIElement ui) {
                Logger.Warn("自定义主页的根元素不是 UIElement，已忽略");
                return null;
            }
            Logger.Info("已加载自定义主页：" + path);
            return ui;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            // 用户手写的 XAML 很容易有语法错误，这里只记录并回退
            Logger.Error(ex, "自定义主页解析失败，已回退默认页面：" + path);
            return null;
        }
    }

}
