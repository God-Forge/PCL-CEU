namespace PCL.Core.Tasks;

/// <summary>
/// C# 版的 LoaderTask，提供输入/输出、重载超时、强制重启、取消与进度。
/// </summary>
public sealed class LoaderTask<TIn, TOut> : TaskBase {

    private readonly Action<LoaderTask<TIn, TOut>> _action;
    private DateTime _lastFinishedUtc = DateTime.MinValue;

    public string Name { get; }
    public TIn Input { get; set; } = default!;
    public TOut Output { get; set; } = default!;
    public double ProgressWeight { get; set; } = 1;
    /// <summary>重载超时（毫秒）；-1 表示永不因超时跳过。</summary>
    public int ReloadTimeout { get; set; } = -1;
    public bool IsForceRestarting { get; private set; }
    public bool IsCanceled => CancellationToken.IsCancellationRequested;
    public void Report(double progress) => SetProgress(progress);

    public LoaderTask(string name, Action<LoaderTask<TIn, TOut>> action) {
        Name = name;
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public bool ShouldStart(TIn input, bool isForceRestart = false, bool ignoreReloadTimeout = false) {
        if (isForceRestart) return true;
        if (!EqualityComparer<TIn>.Default.Equals(input, Input)) return true;

        if (State is TaskState.Loading or TaskState.Finished) {
            if (ignoreReloadTimeout || ReloadTimeout < 0 || _lastFinishedUtc == DateTime.MinValue
                || (DateTime.UtcNow - _lastFinishedUtc).TotalMilliseconds < ReloadTimeout) {
                return false;
            }
        }
        return true;
    }

    public void Start(TIn input, bool isForceRestart = false) {
        if (!ShouldStart(input, isForceRestart)) return;
        if (State == TaskState.Loading) Cancel();
        Input = input;
        IsForceRestarting = isForceRestart;
        base.Start();
    }

    public override void Start() => Start(Input);

    public void Failed(Exception exception) => Fail(exception);

    public async Task<TOut> WaitForExitAsync(TIn input, bool isForceRestart = false, CancellationToken cancellationToken = default) {
        Start(input, isForceRestart);
        await base.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return Output;
    }

    public TOut WaitForExit(TIn input, bool isForceRestart = false) {
        return WaitForExitAsync(input, isForceRestart).GetAwaiter().GetResult();
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken) {
        return Task.Run(() => {
            cancellationToken.ThrowIfCancellationRequested();
            _action(this);
            _lastFinishedUtc = DateTime.UtcNow;
        }, cancellationToken);
    }

}

/// <summary>
/// 顺序执行的 C# LoaderCombo。
/// </summary>
public sealed class LoaderCombo : TaskCombo {

    public string Name { get; }

    public LoaderCombo(string name, IEnumerable<TaskBase> tasks) : base(tasks) {
        Name = name;
    }

    public LoaderCombo(string name, IEnumerable<ITaskControl> tasks) : base(tasks) {
        Name = name;
    }

    public LoaderCombo(string name, IEnumerable<(TaskBase Task, double Weight)> tasks) : base(tasks) {
        Name = name;
    }

    public LoaderCombo(string name, IEnumerable<(ITaskControl Task, double Weight)> tasks) : base(tasks) {
        Name = name;
    }

}
