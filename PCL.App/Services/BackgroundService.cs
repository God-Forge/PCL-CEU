using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Ui;

namespace PCL.App.Services;

/// <summary>
/// 把 PCL\Pictures 里的背景图应用到主界面。
/// 设置项沿用设置页已有的 UiBackgroundOpacity / UiBackgroundBlur / UiBackgroundSuit。
/// </summary>
public static class BackgroundService {

    /// <summary>背景图所在目录。</summary>
    public static string Folder => Path.Combine(AppServices.Paths.Pcl, "Pictures");

    /// <summary>
    /// 应用背景到指定容器；没有图片或读取失败时保持隐藏。
    /// </summary>
    public static bool Apply(Border? target) {
        if (target is null) return false;
        try {
            var image = BackgroundImageSelector.Pick(Folder, DateTime.Now);
            if (image is null) {
                target.Visibility = Visibility.Collapsed;
                return false;
            }
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(image, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var opacity = BackgroundImageSelector.NormalizeOpacity(
                AppServices.Settings.Get<int>("UiBackgroundOpacity"));
            var blur = BackgroundImageSelector.NormalizeBlur(
                AppServices.Settings.Get<int>("UiBackgroundBlur"));
            var suit = AppServices.Settings.Get<int>("UiBackgroundSuit");

            target.Background = new ImageBrush(bitmap) {
                Stretch = suit == 1 ? Stretch.Uniform : Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            target.Opacity = opacity <= 0 ? 1 : opacity;
            target.Effect = blur > 0 ? new BlurEffect { Radius = blur } : null;
            target.Visibility = Visibility.Visible;
            Logger.Info($"已应用背景图：{Path.GetFileName(image)}（透明度 {opacity:0.00}，模糊 {blur:0}）");
            return true;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "应用背景图失败");
            target.Visibility = Visibility.Collapsed;
            return false;
        }
    }

}
