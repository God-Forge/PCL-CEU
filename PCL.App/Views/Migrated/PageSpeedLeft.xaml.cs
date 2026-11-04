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

public partial class PageSpeedLeft : PCL.App.Controls.MyPageLeft {

    private MyButton? _runButton;
    private bool _subscribed;

    public PageSpeedLeft() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            EnsureHeader();
            Subscribe();
            var state = DownloadSpeedTestCoordinator.CurrentState;
            Render(state);
            if (!state.Attempted) _ = RunAsync();
        };
        Unloaded += (_, _) => Unsubscribe();
    }

    private void EnsureHeader() {
        if (_runButton is not null) return;
        var panel = new StackPanel {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        };
        panel.Children.Add(new TextBlock {
            Text = "下载源测速",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        _runButton = new MyButton {
            Text = "重新测速",
            Height = 30,
            MinWidth = 80,
            Margin = new Thickness(12, 0, 0, 0)
        };
        _runButton.Click += async (_, _) => await RunAsync();
        panel.Children.Add(_runButton);
        Grid.SetRow(panel, 0);
        Children.Add(panel);

        foreach (var child in Children) {
            if (child is not TextBlock text) continue;
            switch (text.Text) {
                case "总进度":
                    text.Text = "测速进度";
                    break;
                case "下载速度":
                    text.Text = "响应耗时";
                    break;
                case "剩余文件":
                    text.Text = "已完成";
                    break;
                case "剩余线程":
                    text.Text = "当前源";
                    break;
            }
        }
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
            RenderError(ex.Message);
        }
    }

    private void Render(DownloadSpeedTestState state) {
        var total = state.Total <= 0 ? DownloadSourceSpeedTester.DefaultTargets().Count : state.Total;
        LabProgress.Text = state.Error is not null
            ? "失败"
            : state.Running
                ? (state.Progress * 100).ToString("0.00") + " %"
                : state.Completed ? "100 %" : "0 %";

        var current = state.Results.LastOrDefault();
        if (state.Running) {
            LabSpeed.Text = state.LastElapsedMilliseconds + " ms";
        } else if (current is not null) {
            var best = state.Results
                .Where(result => result.Success)
                .OrderBy(result => result.ElapsedMilliseconds)
                .FirstOrDefault();
            LabSpeed.Text = (best ?? current).ElapsedMilliseconds + " ms";
        } else {
            LabSpeed.Text = "0 ms";
        }

        LabFile.Text = state.Finished + " / " + total;
        LabThread.Text = state.Error is not null
            ? "测速失败"
            : state.Running
                ? (string.IsNullOrWhiteSpace(state.Current) ? "准备中" : state.Current)
                : state.Completed ? "已完成" : "等待开始";
        LabThread.ToolTip = state.Current;
        if (state.Error is null) LabThread.ClearValue(TextBlock.ForegroundProperty);
        else LabThread.Foreground = Brushes.Firebrick;
        if (_runButton is not null) _runButton.IsEnabled = !state.Running;
    }

    private void RenderError(string message) {
        LabProgress.Text = "失败";
        LabSpeed.Text = "0 ms";
        LabFile.Text = "0 / " + DownloadSourceSpeedTester.DefaultTargets().Count;
        LabThread.Text = "测速失败";
        LabThread.ToolTip = message;
        LabThread.Foreground = Brushes.Firebrick;
        if (_runButton is not null) _runButton.IsEnabled = true;
    }

}

public sealed class DownloadSpeedTestState {

    public bool Attempted { get; init; }
    public bool Running { get; init; }
    public bool Completed { get; init; }
    public int Total { get; init; }
    public int Finished { get; init; }
    public string Current { get; init; } = "";
    public long LastElapsedMilliseconds { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<DownloadSpeedResult> Results { get; init; } = Array.Empty<DownloadSpeedResult>();

    public double Progress {
        get {
            if (Total <= 0) return Completed ? 1 : 0;
            return Math.Clamp((double)Finished / Total, 0, 1);
        }
    }

}

internal static class DownloadSpeedTestCoordinator {

    private static readonly object SyncRoot = new();
    private static Task? _running;
    private static DownloadSpeedTestState _currentState = new();

    public static event Action<DownloadSpeedTestState>? StateChanged;

    public static DownloadSpeedTestState CurrentState => _currentState;

    public static Task RunAsync() {
        lock (SyncRoot) {
            if (_running is { IsCompleted: false }) return _running;
            _running = Task.Run(RunCoreAsync);
            return _running;
        }
    }

    private static async Task RunCoreAsync() {
        var targets = DownloadSourceSpeedTester.DefaultTargets();
        var results = new List<DownloadSpeedResult>();
        var tester = new DownloadSourceSpeedTester();
        Publish(new DownloadSpeedTestState {
            Attempted = true,
            Running = true,
            Total = targets.Count,
            Current = "准备中",
            Results = Array.Empty<DownloadSpeedResult>()
        });
        try {
            for (var index = 0; index < targets.Count; index++) {
                var target = targets[index];
                Publish(new DownloadSpeedTestState {
                    Attempted = true,
                    Running = true,
                    Total = targets.Count,
                    Finished = index,
                    Current = target.Name,
                    Results = results.ToList()
                });
                var partial = await tester.TestAllAsync(new[] { target }, 10000).ConfigureAwait(false);
                results.AddRange(partial);
                var elapsed = partial.FirstOrDefault()?.ElapsedMilliseconds ?? 0;
                Publish(new DownloadSpeedTestState {
                    Attempted = true,
                    Running = true,
                    Total = targets.Count,
                    Finished = index + 1,
                    Current = target.Name,
                    LastElapsedMilliseconds = elapsed,
                    Results = results.ToList()
                });
            }
            Publish(new DownloadSpeedTestState {
                Attempted = true,
                Running = false,
                Completed = true,
                Total = targets.Count,
                Finished = targets.Count,
                Current = "已完成",
                LastElapsedMilliseconds = results.Where(result => result.Success)
                    .Select(result => result.ElapsedMilliseconds)
                    .DefaultIfEmpty(0)
                    .Min(),
                Results = results.ToList()
            });
        } catch (Exception ex) {
            Publish(new DownloadSpeedTestState {
                Attempted = true,
                Running = false,
                Completed = true,
                Total = targets.Count,
                Finished = results.Count,
                Current = "测速失败",
                Error = ex.Message,
                Results = results.ToList()
            });
        } finally {
            lock (SyncRoot) _running = null;
        }
    }

    private static void Publish(DownloadSpeedTestState state) {
        _currentState = state;
        try {
            StateChanged?.Invoke(state);
        } catch {
        }
    }

}
