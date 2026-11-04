using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCL.Core.Ui;

namespace PCL.App.Animations;

/// <summary>
/// 基于 WPF 动画系统的界面动效服务。
/// 时序由 <see cref="AnimationPlanner"/> 规划，这里只负责把步骤映射为 WPF 动画。
/// </summary>
public static class PclAnimationService {

    /// <summary>按已规划的步骤播放动画。</summary>
    public static void Play(FrameworkElement element, IEnumerable<ScheduledAnimationStep> scheduled) {
        ArgumentNullException.ThrowIfNull(element);
        foreach (var item in scheduled) {
            PlayStep(element, item);
        }
    }

    /// <summary>淡入：从当前值渐变到指定不透明度。</summary>
    public static void FadeTo(FrameworkElement element, double opacity, int milliseconds = 400, int delay = 0,
        EasingKind easing = EasingKind.FluentOut) {
        BeginDouble(element, UIElement.OpacityProperty, opacity, milliseconds, delay, easing);
    }

    /// <summary>缩放：作用于 ScaleTransform，自动包裹已有的 RenderTransform。</summary>
    public static void ScaleTo(FrameworkElement element, double scale, int milliseconds = 400, int delay = 0,
        EasingKind easing = EasingKind.FluentOut) {
        var transform = GetOrCreateTransform<ScaleTransform>(element);
        BeginDouble(transform, ScaleTransform.ScaleXProperty, scale, milliseconds, delay, easing);
        BeginDouble(transform, ScaleTransform.ScaleYProperty, scale, milliseconds, delay, easing);
    }

    /// <summary>平移：作用于 TranslateTransform。</summary>
    public static void TranslateTo(FrameworkElement element, double x, double y, int milliseconds = 400, int delay = 0,
        EasingKind easing = EasingKind.FluentOut) {
        var transform = GetOrCreateTransform<TranslateTransform>(element);
        BeginDouble(transform, TranslateTransform.XProperty, x, milliseconds, delay, easing);
        BeginDouble(transform, TranslateTransform.YProperty, y, milliseconds, delay, easing);
    }

    /// <summary>旋转：作用于 RotateTransform。</summary>
    public static void RotateTo(FrameworkElement element, double angle, int milliseconds = 400, int delay = 0,
        EasingKind easing = EasingKind.FluentOut) {
        var transform = GetOrCreateTransform<RotateTransform>(element);
        BeginDouble(transform, RotateTransform.AngleProperty, angle, milliseconds, delay, easing);
    }

    /// <summary>把缓动风格映射为 WPF 缓动函数。</summary>
    public static IEasingFunction? CreateEasing(EasingKind kind, double power = 1) {
        var amplitude = Math.Max(0.1, power);
        return kind switch {
            EasingKind.Linear => null,
            EasingKind.FluentIn => new QuarticEase { EasingMode = EasingMode.EaseIn },
            EasingKind.FluentOut => new QuarticEase { EasingMode = EasingMode.EaseOut },
            EasingKind.FluentInOut => new QuarticEase { EasingMode = EasingMode.EaseInOut },
            EasingKind.BackIn => new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = amplitude },
            EasingKind.BackOut => new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = amplitude },
            EasingKind.CarIn => new CubicEase { EasingMode = EasingMode.EaseIn },
            EasingKind.CarOut => new CubicEase { EasingMode = EasingMode.EaseOut },
            EasingKind.ElasticIn => new ElasticEase {
                EasingMode = EasingMode.EaseIn,
                Oscillations = 3,
                Springiness = Math.Clamp(amplitude, 1, 10)
            },
            EasingKind.ElasticOut => new ElasticEase {
                EasingMode = EasingMode.EaseOut,
                Oscillations = 3,
                Springiness = Math.Clamp(amplitude, 1, 10)
            },
            _ => null
        };
    }

    private static void PlayStep(FrameworkElement element, ScheduledAnimationStep scheduled) {
        var step = scheduled.Step;
        if (step.To is not { } value) return;
        switch (step.Property) {
            case "Opacity":
                BeginDouble(element, UIElement.OpacityProperty, value, scheduled.DurationMilliseconds,
                    scheduled.BeginMilliseconds, step.Easing, step.EasingPower);
                break;
            case "Width":
                BeginDouble(element, FrameworkElement.WidthProperty, value, scheduled.DurationMilliseconds,
                    scheduled.BeginMilliseconds, step.Easing, step.EasingPower);
                break;
            case "Height":
                BeginDouble(element, FrameworkElement.HeightProperty, value, scheduled.DurationMilliseconds,
                    scheduled.BeginMilliseconds, step.Easing, step.EasingPower);
                break;
            case "Scale":
                ScaleTo(element, value, scheduled.DurationMilliseconds, scheduled.BeginMilliseconds, step.Easing);
                break;
            case "TranslateX":
                BeginDouble(GetOrCreateTransform<TranslateTransform>(element), TranslateTransform.XProperty, value,
                    scheduled.DurationMilliseconds, scheduled.BeginMilliseconds, step.Easing, step.EasingPower);
                break;
            case "TranslateY":
                BeginDouble(GetOrCreateTransform<TranslateTransform>(element), TranslateTransform.YProperty, value,
                    scheduled.DurationMilliseconds, scheduled.BeginMilliseconds, step.Easing, step.EasingPower);
                break;
            case "Rotate":
                RotateTo(element, value, scheduled.DurationMilliseconds, scheduled.BeginMilliseconds, step.Easing);
                break;
        }
    }

    private static void BeginDouble(
        IAnimatable target,
        DependencyProperty property,
        double to,
        int duration,
        int delay,
        EasingKind easing,
        double power = 1) {
        var animation = new DoubleAnimation {
            To = to,
            Duration = TimeSpan.FromMilliseconds(Math.Max(0, duration)),
            BeginTime = TimeSpan.FromMilliseconds(Math.Max(0, delay)),
            EasingFunction = CreateEasing(easing, power),
            FillBehavior = FillBehavior.HoldEnd
        };
        target.BeginAnimation(property, animation);
    }

    private static T GetOrCreateTransform<T>(FrameworkElement element) where T : Transform, new() {
        if (element.RenderTransform is TransformGroup group) {
            if (group.Children.OfType<T>().FirstOrDefault() is { } existing) return existing;
            var added = new T();
            group.Children.Add(added);
            return added;
        }
        var created = new T();
        var newGroup = new TransformGroup();
        if (element.RenderTransform is { } current && !ReferenceEquals(current, Transform.Identity)) {
            newGroup.Children.Add(current);
        }
        newGroup.Children.Add(created);
        element.RenderTransform = newGroup;
        return created;
    }

}
