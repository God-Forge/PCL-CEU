using System.Text;
using PCL.App.Services;
using PCL.Core.Minecraft;

namespace PCL.App.Views.Migrated;

public partial class PageLaunchRight : PCL.App.Controls.MyPageRight {

    private const int MaximumCharacters = 120_000;
    private readonly StringBuilder _log = new();
    private bool _subscribed;

    public PageLaunchRight() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            Subscribe();
            _log.Clear();
            _log.Append(LaunchLogService.Text);
            TrimLog();
            UpdateLog();
        };
        Unloaded += (_, _) => Unsubscribe();
    }

    private void Subscribe() {
        if (_subscribed) return;
        LaunchLogService.LineWritten += OnLineWritten;
        LaunchLogService.Cleared += OnCleared;
        _subscribed = true;
    }

    private void Unsubscribe() {
        if (!_subscribed) return;
        LaunchLogService.LineWritten -= OnLineWritten;
        LaunchLogService.Cleared -= OnCleared;
        _subscribed = false;
    }

    private void OnLineWritten(string line) {
        _ = DispatcherService.InvokeAsync(() => {
            if (_log.Length > 0) _log.AppendLine();
            _log.Append(line);
            TrimLog();
            UpdateLog();
        });
    }

    private void OnCleared() {
        _ = DispatcherService.InvokeAsync(() => {
            _log.Clear();
            UpdateLog();
        });
    }

    private void TrimLog() {
        if (_log.Length <= MaximumCharacters) return;
        _log.Remove(0, _log.Length - MaximumCharacters);
    }

    private void UpdateLog() {
        LabLog.Text = _log.Length == 0
            ? "暂无启动日志。启动游戏后将在此显示输出。"
            : _log.ToString();
        LabLog.ScrollToEnd();
    }

}
