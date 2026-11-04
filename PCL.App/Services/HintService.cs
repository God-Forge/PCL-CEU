using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MeloongCore;
using PCL.Core.Ui;

namespace PCL.App.Services;

/// <summary>
/// 窗口左下角的提示条。队列入队与去重由 <see cref="HintQueue"/> 负责，这里只做展示。
/// </summary>
public static class HintService {

    private static readonly HintQueue Queue = new();
    private static readonly Dictionary<HintEntry, Border> Controls = new();
    private static readonly Dictionary<HintEntry, CancellationTokenSource> Lifetimes = new();

    private static Panel? _panel;

    /// <summary>绑定提示面板，通常在主窗口构造时调用。</summary>
    public static void Initialize(Panel panel) {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
    }

    /// <summary>弹出一条提示。空文本或超出上限时会被丢弃。</summary>
    public static void Show(string? text, HintKind kind = HintKind.Blue, bool log = true) {
        var result = Queue.Enqueue(text, kind, log);
        if (result.Entry is null) return;
        if (log) Logger.Info("弹出提示：" + result.Entry.Text);
        DispatcherService.Invoke(() => Present(result));
    }

    private static void Present(HintEnqueueResult result) {
        if (_panel is null || result.Entry is null) return;
        var entry = result.Entry;
        if (result.Outcome == HintOutcome.Added) {
            var border = CreateBorder(entry);
            Controls[entry] = border;
            _panel.Children.Add(border);
            border.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
        }
        // 新增与重复都会重置存活时间
        ScheduleRemoval(entry);
    }

    private static Border CreateBorder(HintEntry entry) {
        var brush = new LinearGradientBrush {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops = {
                new GradientStop(KindColor(entry.Kind), 0),
                new GradientStop(KindColor(entry.Kind, 0.15), 1)
            }
        };
        return new Border {
            Background = brush,
            CornerRadius = new CornerRadius(0, 6, 6, 0),
            Margin = new Thickness(-12, 0, 0, 6),
            MaxWidth = 460,
            Opacity = 0,
            Child = new TextBlock {
                Text = entry.Text,
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(16, 5, 12, 5),
                TextTrimming = TextTrimming.CharacterEllipsis
            }
        };
    }

    private static void ScheduleRemoval(HintEntry entry) {
        if (Lifetimes.Remove(entry, out var previous)) {
            previous.Cancel();
            previous.Dispose();
        }
        var cancellation = new CancellationTokenSource();
        Lifetimes[entry] = cancellation;
        _ = Task.Delay(entry.DisplayMilliseconds, cancellation.Token).ContinueWith(task => {
            if (task.IsCanceled) return;
            DispatcherService.Invoke(() => Dismiss(entry));
        }, TaskScheduler.Default);
    }

    private static void Dismiss(HintEntry entry) {
        if (Lifetimes.Remove(entry, out var cancellation)) {
            cancellation.Dispose();
        }
        if (!Controls.Remove(entry, out var border)) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => _panel?.Children.Remove(border);
        border.BeginAnimation(UIElement.OpacityProperty, fade);
        Queue.Remove(entry);
    }

    private static Color KindColor(HintKind kind, double lighten = 0) {
        var color = kind switch {
            HintKind.Green => Color.FromRgb(29, 160, 29),
            HintKind.Red => Color.FromRgb(255, 43, 0),
            _ => Color.FromRgb(10, 142, 252)
        };
        if (lighten <= 0) return color;
        return Color.FromRgb(
            (byte)Math.Min(255, color.R + (255 - color.R) * lighten),
            (byte)Math.Min(255, color.G + (255 - color.G) * lighten),
            (byte)Math.Min(255, color.B + (255 - color.B) * lighten));
    }

}
