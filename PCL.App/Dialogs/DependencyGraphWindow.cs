using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.Resources;

namespace PCL.App.Dialogs;

/// <summary>
/// 依赖图窗口：把依赖解析结果按层级画出来，根在左侧、依赖逐层向右。
/// 只做展示，不提供编辑。
/// </summary>
public sealed class DependencyGraphWindow : Window {

    private const double NodeWidth = 150;
    private const double NodeHeight = 34;

    public DependencyGraphWindow(ResourceDependencyPlan plan, string title = "依赖关系") {
        ArgumentNullException.ThrowIfNull(plan);
        Title = title;
        Width = 940;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(250, 250, 252));
        Content = BuildContent(plan);
    }

    private UIElement BuildContent(ResourceDependencyPlan plan) {
        var root = new DockPanel { Margin = new Thickness(16) };

        var header = new TextBlock {
            Text = $"共 {plan.Files.Count} 个文件，{plan.Edges.Count} 条依赖关系"
                   + (plan.Unresolved.Count > 0 ? $"；{plan.Unresolved.Count} 个依赖未能解析" : ""),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = plan.Unresolved.Count > 0
                ? new SolidColorBrush(Color.FromRgb(190, 90, 20))
                : new SolidColorBrush(Color.FromRgb(110, 110, 110))
        };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);

        var footer = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var close = new Button { Content = "关闭", Width = 90, Height = 30, IsDefault = true };
        close.Click += (_, _) => Close();
        footer.Children.Add(close);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var graph = DependencyGraphLayout.Build(plan, 890, 420);
        var canvas = new Canvas {
            Background = Brushes.White,
            ClipToBounds = true
        };
        canvas.Width = graph.Width;
        canvas.Height = graph.Height;

        // 先画连线，保证节点盖在线之上
        foreach (var edge in graph.Edges) {
            var from = graph.Find(edge.FromId);
            var to = graph.Find(edge.ToId);
            if (from is null || to is null) continue;
            canvas.Children.Add(new System.Windows.Shapes.Line {
                X1 = from.X + NodeWidth / 2,
                Y1 = from.Y,
                X2 = to.X - NodeWidth / 2,
                Y2 = to.Y,
                Stroke = new SolidColorBrush(Color.FromRgb(180, 198, 220)),
                StrokeThickness = 1.2
            });
        }

        foreach (var node in graph.Nodes) {
            canvas.Children.Add(BuildNode(node));
        }

        root.Children.Add(new ScrollViewer {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = canvas
        });
        return root;
    }

    private static UIElement BuildNode(DependencyGraphNode node) {
        var border = new Border {
            Width = NodeWidth,
            Height = NodeHeight,
            CornerRadius = new CornerRadius(6),
            Background = node.IsRoot
                ? new SolidColorBrush(Color.FromRgb(220, 236, 255))
                : new SolidColorBrush(Color.FromRgb(245, 247, 250)),
            BorderBrush = node.IsRoot
                ? new SolidColorBrush(Color.FromRgb(70, 130, 220))
                : new SolidColorBrush(Color.FromRgb(210, 216, 226)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock {
                Text = node.Label,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            },
            ToolTip = node.Id
        };
        Canvas.SetLeft(border, node.X - NodeWidth / 2);
        Canvas.SetTop(border, node.Y - NodeHeight / 2);
        return border;
    }

}
