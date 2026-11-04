using System.Windows;
using System.Windows.Media.Animation;

namespace PCL.App.Services;

public static class PageAnimationService {

    public static void FadeIn(FrameworkElement element, int milliseconds = 180) {
        // 迁移期先保证内容立即可见，避免动画在部分渲染环境下停留在 Opacity=0。
        element.Opacity = 1;
    }

    public static void SlideIn(FrameworkElement element, double offset = 16, int milliseconds = 180) {
        element.Opacity = 0;
        element.RenderTransform = new System.Windows.Media.TranslateTransform(0, offset);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds)));
        element.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(milliseconds)) {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }

}

