using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Download;

using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ListBox = System.Windows.Controls.ListBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Size = System.Windows.Size;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public partial class PageSpeedRight : PCL.App.Controls.MyPageRight {

    private bool _subscribed;

    public PageSpeedRight() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            PanBack.ScrollToHome();
            Subscribe();
            var state = DownloadSpeedTestCoordinator.CurrentState;
            Render(state);
            if (!state.Attempted) _ = RunAsync();
        };
        Unloaded += (_, _) => Unsubscribe();
    }

    private void Subscribe() {
        if (_subscribed) return;
        DownloadSpeedTestCoordinator.StateChanged += OnStateChanged;
        _subscribed = true;
    }

    private void Unsubscribe() {
        if (!_subscribed) return;
        DownloadSpeedTestCoordinator.StateChanged -= OnStateChanged;
        _subscribed = false;
    }

    private void OnStateChanged(DownloadSpeedTestState state) {
        _ = DispatcherService.InvokeAsync(() => Render(state));
    }

    private async Task RunAsync() {
        try {
            await DownloadSpeedTestCoordinator.RunAsync();
        } catch (Exception ex) {
            Render(new DownloadSpeedTestState {
                Attempted = true,
                Completed = true,
                Total = DownloadSourceSpeedTester.DefaultTargets().Count,
                Current = "测速失败",
                Error = ex.Message
            });
        }
    }

    private void Render(DownloadSpeedTestState state) {
        var offset = PanBack.VerticalOffset;
        PanMain.Children.Clear();
        PanMain.Children.Add(BuildOverviewCard(state));
        PanMain.Children.Add(BuildResultsCard(state));
        PanBack.ScrollToVerticalOffset(offset);
    }

    private UIElement BuildOverviewCard(DownloadSpeedTestState state) {
        var card = new MyCard {
            Title = "下载源测速",
            Margin = new Thickness(0, 0, 0, 15)
        };
        var panel = new StackPanel { Margin = new Thickness(20, 8, 20, 16) };
        panel.Children.Add(new TextBlock {
            Text = GetStatusText(state),
            TextWrapping = TextWrapping.Wrap,
            Foreground = state.Error is null ? Brushes.Gray : Brushes.Firebrick
        });
        var progress = new ProgressBar {
            Height = 6,
            Minimum = 0,
            Maximum = 1,
            Value = state.Progress,
            Margin = new Thickness(0, 12, 0, 12)
        };
        panel.Children.Add(progress);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var run = new MyButton {
            Text = state.Running ? "正在测速" : "重新测速",
            Height = 34,
            MinWidth = 96,
            ColorType = "Highlight",
            IsEnabled = !state.Running
        };
        run.Click += async (_, _) => await RunAsync();
        actions.Children.Add(run);
        panel.Children.Add(actions);
        card.Children.Add(panel);
        return card;
    }

    private static string GetStatusText(DownloadSpeedTestState state) {
        if (state.Error is not null) return "测速失败：" + state.Error;
        if (state.Running) {
            var current = string.IsNullOrWhiteSpace(state.Current) ? "准备中" : state.Current;
            return $"正在测试 {current}（{state.Finished} / {state.Total}）……";
        }
        if (!state.Attempted) return "尚未开始测速。";
        var success = state.Results.Count(result => result.Success);
        var best = state.Results
            .Where(result => result.Success)
            .OrderBy(result => result.ElapsedMilliseconds)
            .FirstOrDefault();
        if (best is null) return $"测速完成，但没有可用的下载源（{state.Finished} / {state.Total}）。";
        return $"测速完成，最快下载源为 {best.Name}（{best.ElapsedMilliseconds} ms），成功 {success} / {state.Total}。";
    }

    private UIElement BuildResultsCard(DownloadSpeedTestState state) {
        var card = new MyCard {
            Title = "测速结果",
            Margin = new Thickness(0, 0, 0, 15)
        };
        var panel = new StackPanel { Margin = new Thickness(20, 8, 20, 16) };
        if (state.Results.Count == 0) {
            panel.Children.Add(new TextBlock {
                Text = state.Running ? "正在等待第一个测速结果……" : "暂无测速结果。",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap
            });
        } else {
            foreach (var result in state.Results) {
                panel.Children.Add(BuildResultRow(result));
            }
        }
        card.Children.Add(panel);
        return card;
    }

    private static UIElement BuildResultRow(DownloadSpeedResult result) {
        var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        var name = new TextBlock {
            Text = result.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = result.Url
        };
        Grid.SetColumn(name, 0);
        row.Children.Add(name);
        var detail = new TextBlock {
            Text = result.Success ? "可用" : "失败：" + (string.IsNullOrWhiteSpace(result.Error) ? "未知错误" : result.Error),
            Foreground = result.Success ? Brushes.ForestGreen : Brushes.Firebrick,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = result.Error
        };
        Grid.SetColumn(detail, 1);
        row.Children.Add(detail);
        var elapsed = new TextBlock {
            Text = result.Success ? result.ElapsedMilliseconds + " ms" : "—",
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = result.Success ? Brushes.Gray : Brushes.Firebrick
        };
        Grid.SetColumn(elapsed, 2);
        row.Children.Add(elapsed);
        return row;
    }

}
