using System.Collections.Concurrent;
using System.Diagnostics;

namespace PCL.Core.Minecraft;

/// <summary>
/// 游戏进程的运行时状态。
/// </summary>
public enum MinecraftGameState {
    /// <summary>已启动，正在加载。</summary>
    Loading,
    /// <summary>窗口已出现，正在游戏。</summary>
    Running,
    /// <summary>已判定为崩溃。</summary>
    Crashed,
    /// <summary>正常结束。</summary>
    Ended,
    /// <summary>被启动器强制结束。</summary>
    Canceled
}

/// <summary>
/// <see cref="GameProcessWatcher"/> 的行为配置。
/// </summary>
public sealed class GameProcessWatcherOptions {

    /// <summary>窗口标题模板，支持 <c>{date}</c> 与 <c>{time}</c>。为空则不修改标题。</summary>
    public string? WindowTitle { get; init; }

    /// <summary>窗口出现后是否最大化。</summary>
    public bool MaximizeWindow { get; init; }

    /// <summary>版本发布年份，用于兼容旧版非 0 退出码。</summary>
    public int? ReleaseYear { get; init; }

    /// <summary>主循环轮询间隔（毫秒）。</summary>
    public int PollIntervalMilliseconds { get; init; } = 64;

    /// <summary>保留的最近日志条数。</summary>
    public int LatestLogCapacity { get; init; } = 500;

    /// <summary>窗口标题刷新间隔（毫秒）。</summary>
    public int TitleRefreshMilliseconds { get; init; } = 1000;

    /// <summary>窗口出现后延迟最大化的时间（毫秒）。</summary>
    public int MaximizeDelayMilliseconds { get; init; } = 2000;

    /// <summary>是否枚举游戏窗口。无界面环境可关闭。</summary>
    public bool ProbeWindows { get; init; } = true;

    /// <summary>自定义窗口探测，用于测试或特殊环境。</summary>
    public Func<Process, MinecraftWindow?>? WindowProbe { get; init; }

}

/// <summary>
/// 监视一个 Minecraft 进程：读取日志、推进启动进度、探测游戏窗口并判定退出结果。
/// 由 VB 版 <c>ModWatcher.Watcher</c> 迁移而来。
/// </summary>
public sealed class GameProcessWatcher : IDisposable {

    private readonly Process _process;
    private readonly GameProcessWatcherOptions _options;
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly ConcurrentQueue<string> _latest = new();
    private readonly object _gate = new();

    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private MinecraftGameState _state = MinecraftGameState.Loading;
    private MinecraftLaunchProgress _progress = MinecraftLaunchProgress.None;
    private bool _windowFinished;
    private bool _windowAppeared;
    private bool _loaderWindowLogged;
    private bool _maximizeRequested;
    private double _reportedProgress = -1;
    private int _pollCount;

    public GameProcessWatcher(Process process, GameProcessWatcherOptions? options = null) {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _options = options ?? new GameProcessWatcherOptions();
    }

    /// <summary>被监视的进程。</summary>
    public Process Process => _process;

    /// <summary>当前状态。</summary>
    public MinecraftGameState State {
        get { lock (_gate) return _state; }
    }

    /// <summary>已推进到的启动阶段。</summary>
    public MinecraftLaunchProgress Progress => _progress;

    /// <summary>游戏窗口句柄，未找到时为 <see cref="IntPtr.Zero"/>。</summary>
    public IntPtr WindowHandle { get; private set; }

    /// <summary>最近的日志快照。</summary>
    public IReadOnlyList<string> LatestLog => _latest.ToArray();

    /// <summary>状态变更。</summary>
    public event EventHandler<MinecraftGameState>? StateChanged;

    /// <summary>启动进度（0 ~ 1）。</summary>
    public event Action<double>? ProgressChanged;

    /// <summary>收到一行游戏输出。</summary>
    public event Action<string>? LogReceived;

    /// <summary>找到游戏窗口。</summary>
    public event Action<IntPtr, string>? WindowFound;

    /// <summary>开始监视。重复调用无副作用。</summary>
    public void Start() {
        if (_loop is not null) return;
        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += OnDataReceived;
        _process.ErrorDataReceived += OnDataReceived;
        try {
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        } catch (InvalidOperationException ex) {
            // 输出流已被其他组件接管，退化为只做状态与窗口监视
            Logger.Warn(ex, "无法接管 Minecraft 输出流，日志监视已跳过");
        }
        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        _loop = Task.Run(() => RunAsync(token), CancellationToken.None);
    }

    /// <summary>强制结束游戏进程。</summary>
    public void Kill() {
        SetState(MinecraftGameState.Canceled);
        try {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "强制结束 Minecraft 进程失败");
        }
    }

    public void Dispose() {
        try {
            _cancellation?.Cancel();
        } catch (ObjectDisposedException) {
            // 已释放
        }
        _process.OutputDataReceived -= OnDataReceived;
        _process.ErrorDataReceived -= OnDataReceived;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private async Task RunAsync(CancellationToken token) {
        try {
            while (!token.IsCancellationRequested) {
                DrainPendingLogs();
                if (State is MinecraftGameState.Ended or MinecraftGameState.Canceled) break;
                ProbeWindow();
                if (UpdateExitState()) break;
                RefreshTitle();
                _pollCount++;
                await Task.Delay(_options.PollIntervalMilliseconds, token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) {
            // 正常停止
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "Minecraft 日志监控主循环出错");
            SetState(MinecraftGameState.Ended);
        }
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e) {
        if (e.Data is null) return;
        _pending.Enqueue(e.Data);
    }

    private void DrainPendingLogs() {
        var received = false;
        while (_pending.TryDequeue(out var line)) {
            received = true;
            HandleLine(line);
        }
        if (received && State == MinecraftGameState.Loading) UpdateProgress();
    }

    private void HandleLine(string line) {
        var text = line.ReplaceLineEndings();
        _latest.Enqueue(text);
        while (_latest.Count > _options.LatestLogCapacity && _latest.TryDequeue(out _)) {
            // 丢弃最旧的日志
        }
        LaunchLogService.WriteLine(text);
        try {
            LogReceived?.Invoke(text);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理 Minecraft 日志回调时出错");
        }
        _progress = MinecraftLogRules.AdvanceProgress(text, _progress);
        switch (MinecraftLogRules.DetectSignal(text)) {
            case MinecraftLogSignal.CloseRequested:
                SetState(MinecraftGameState.Ended);
                break;
            case MinecraftLogSignal.CrashDetected:
                Crash();
                break;
        }
    }

    private void ProbeWindow() {
        if (!_options.ProbeWindows && _options.WindowProbe is null) return;
        if (_windowFinished || _process.HasExited) return;
        var window = _options.WindowProbe is { } probe ? probe(_process) : MinecraftWindowFinder.Find(_process);
        if (window is null) return;
        var found = window.Value;
        _windowAppeared = true;
        if (found.IsLoaderWindow) {
            if (!_loaderWindowLogged) {
                _loaderWindowLogged = true;
                Logger.Info($"Mod Loader 窗口已加载：{found.Title}");
            }
            UpdateProgress();
            return;
        }
        WindowHandle = found.Handle;
        _windowFinished = true;
        Logger.Info($"Minecraft 窗口已加载：{found.Title}（{found.Handle.ToInt64()}）");
        LaunchTimingService.Mark("窗口已加载");
        try {
            WindowFound?.Invoke(found.Handle, found.Title);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理窗口回调时出错");
        }
        UpdateProgress();
        if (_options.MaximizeWindow) RequestMaximize();
    }

    private void RequestMaximize() {
        if (_maximizeRequested) return;
        _maximizeRequested = true;
        var handle = WindowHandle;
        _ = Task.Delay(_options.MaximizeDelayMilliseconds).ContinueWith(_ => {
            if (State is MinecraftGameState.Canceled) return;
            try {
                if (!_process.HasExited) MinecraftWindowFinder.Maximize(handle);
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "最大化 Minecraft 窗口时出现错误");
            }
        }, TaskScheduler.Default);
    }

    private void RefreshTitle() {
        if (_options.WindowTitle is not { Length: > 0 } template) return;
        if (WindowHandle == IntPtr.Zero || State != MinecraftGameState.Running) return;
        if (_options.TitleRefreshMilliseconds > 0) {
            var interval = Math.Max(1, _options.TitleRefreshMilliseconds / Math.Max(1, _options.PollIntervalMilliseconds));
            if (_pollCount % interval != 0) return;
        }
        var now = DateTime.Now;
        var title = template
            .Replace("{date}", now.ToString("yyyy/M/d"), StringComparison.Ordinal)
            .Replace("{time}", now.ToString("HH:mm:ss"), StringComparison.Ordinal);
        MinecraftWindowFinder.TrySetTitle(WindowHandle, title);
    }

    private void UpdateProgress() {
        var value = MinecraftLogRules.EstimateProgress(_progress, _windowAppeared);
        if (value >= 0.95 && State == MinecraftGameState.Loading) SetState(MinecraftGameState.Running);
        if (Math.Abs(value - _reportedProgress) < 0.001) return;
        _reportedProgress = value;
        try {
            ProgressChanged?.Invoke(value);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理进度的回调时出错");
        }
    }

    /// <summary>检查进程是否已退出并更新状态。返回是否结束监视。</summary>
    private bool UpdateExitState() {
        if (!_process.HasExited) return false;
        // 进程退出后异步输出可能还没派发完，短暂等待并再取一次
        try {
            _process.WaitForExit(200);
        } catch (InvalidOperationException) {
            // 进程信息已不可用
        }
        DrainPendingLogs();
        var exitCode = 0;
        try {
            exitCode = _process.ExitCode;
        } catch (InvalidOperationException) {
            // 进程信息不可用，按 0 处理
        }
        var verdict = MinecraftExitClassifier.Classify(State, exitCode, _options.ReleaseYear);
        Logger.Info($"[MC] 进程已退出，退出码 {exitCode}，判定 {verdict}");
        if (MinecraftExitClassifier.IsCrash(verdict)) {
            Crash();
            return true;
        }
        if (State != MinecraftGameState.Crashed) SetState(MinecraftGameState.Ended);
        return true;
    }

    private void Crash() {
        if (State is MinecraftGameState.Crashed or MinecraftGameState.Ended or MinecraftGameState.Canceled) return;
        SetState(MinecraftGameState.Crashed);
        Logger.Warn("[MC] 已判定为崩溃");
    }

    private void SetState(MinecraftGameState state) {
        lock (_gate) {
            if (_state == state) return;
            _state = state;
        }
        var handler = StateChanged;
        if (handler is null) return;
        try {
            handler(this, state);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理 Minecraft 状态变更时出错");
        }
    }

}
