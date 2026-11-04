namespace PCL.Core.Tasks;

public enum TaskState {
    Waiting,
    Loading,
    Finished,
    Failed,
    Canceled
}

public sealed class TaskStateChangedEventArgs : EventArgs {

    public TaskState OldState { get; }
    public TaskState NewState { get; }

    public TaskStateChangedEventArgs(TaskState oldState, TaskState newState) {
        OldState = oldState;
        NewState = newState;
    }

}

public sealed class TaskProgressChangedEventArgs : EventArgs {

    public double OldProgress { get; }
    public double NewProgress { get; }

    public TaskProgressChangedEventArgs(double oldProgress, double newProgress) {
        OldProgress = oldProgress;
        NewProgress = newProgress;
    }

}

/// <summary>
/// 供 VB LoaderBase 与后续 C# 任务迁移使用的统一状态/进度/取消接口。
/// </summary>
public interface ITaskProgress {

    TaskState State { get; }
    double Progress { get; }
    Exception? Error { get; }
    CancellationToken CancellationToken { get; }
    event EventHandler<TaskStateChangedEventArgs>? StateChanged;
    event EventHandler<TaskProgressChangedEventArgs>? ProgressChanged;

}

public interface ITaskControl : ITaskProgress {

    void Start();
    void Cancel();
    Task WaitForExitAsync(CancellationToken cancellationToken = default);

}

public abstract class TaskBase : ITaskControl, IDisposable {

    private TaskState _state = TaskState.Waiting;
    private double _progress = -1;
    private CancellationTokenSource? _cancellation;
    private bool _disposed;

    public TaskState State => _state;
    public double Progress => _progress;
    public Exception? Error { get; private set; }
    public CancellationToken CancellationToken => _cancellation?.Token ?? CancellationToken.None;
    public event EventHandler<TaskStateChangedEventArgs>? StateChanged;
    public event EventHandler<TaskProgressChangedEventArgs>? ProgressChanged;

    public virtual void Start() {
        if (_state == TaskState.Loading) return;
        if (_disposed) throw new ObjectDisposedException(GetType().Name);
        Error = null;
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        SetProgress(-1);
        SetState(TaskState.Loading);
        _ = RunAsync(_cancellation.Token);
    }

    protected abstract Task ExecuteAsync(CancellationToken cancellationToken);

    private async Task RunAsync(CancellationToken cancellationToken) {
        try {
            await ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested) SetState(TaskState.Finished);
        } catch (OperationCanceledException) {
            SetState(TaskState.Canceled);
        } catch (Exception ex) {
            Error = ex;
            SetState(TaskState.Failed);
        }
    }

    public void Cancel() {
        _cancellation?.Cancel();
        if (_state == TaskState.Loading) SetState(TaskState.Canceled);
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default) {
        while (_state == TaskState.Loading) {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellationToken).ConfigureAwait(false);
        }
        if (_state == TaskState.Failed && Error is not null) throw Error;
    }

    protected void SetProgress(double value) {        var progress = Math.Max(-1, Math.Min(1, value));
        if (Math.Abs(progress - _progress) < 0.000001) return;
        var old = _progress;
        _progress = progress;
        ProgressChanged?.Invoke(this, new TaskProgressChangedEventArgs(old, progress));
    }

    protected void SetState(TaskState state) {
        if (_state == state) return;
        var old = _state;
        _state = state;
        StateChanged?.Invoke(this, new TaskStateChangedEventArgs(old, state));
    }

    protected void Finish() => SetState(TaskState.Finished);

    protected void BeginTask() {
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        SetProgress(-1);
        SetState(TaskState.Loading);
    }

    protected void Fail(Exception exception) {
        Error = exception;
        SetState(TaskState.Failed);
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

}

/// <summary>
/// 可直接用于包装现有异步逻辑的任务，也可作为 C# 任务的状态容器。
/// </summary>
public sealed class ProgressTask : TaskBase {

    private sealed class DirectProgress(Action<double> report) : IProgress<double> {
        public void Report(double value) => report(value);
    }

    private readonly Func<CancellationToken, IProgress<double>, Task>? _action;

    public ProgressTask() { }

    public ProgressTask(Func<CancellationToken, IProgress<double>, Task> action) {
        _action = action;
    }

    public void Report(double progress) => SetProgress(progress);
    public void Begin() => BeginTask();
    public void Complete() => Finish();
    public void FailTask(Exception exception) => Fail(exception);
    public void MarkCanceled() => SetState(TaskState.Canceled);

    protected override Task ExecuteAsync(CancellationToken cancellationToken) {
        return _action is null ? Task.CompletedTask : _action(cancellationToken, new DirectProgress(Report));
    }

}

/// <summary>
/// 顺序执行一组任务，按 ProgressWeight 汇总进度。
/// </summary>
public class TaskCombo : TaskBase {

    private readonly IReadOnlyList<(ITaskControl Task, double Weight)> _tasks;

    public TaskCombo(IEnumerable<TaskBase> tasks) {
        _tasks = tasks.Cast<ITaskControl>().Select(task => (task, 1d)).ToList();
    }

    public TaskCombo(IEnumerable<ITaskControl> tasks) {
        _tasks = tasks.Select(task => (task, 1d)).ToList();
    }

    public TaskCombo(IEnumerable<(ITaskControl Task, double Weight)> tasks) {
        _tasks = tasks.ToList();
    }

    public TaskCombo(IEnumerable<(TaskBase Task, double Weight)> tasks) {
        _tasks = tasks.Select(item => ((ITaskControl)item.Task, item.Weight)).ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken) {
        var totalWeight = _tasks.Sum(item => Math.Max(0.0001, item.Weight));
        var completedWeight = 0d;
        foreach (var (task, weight) in _tasks) {
            cancellationToken.ThrowIfCancellationRequested();
            var safeWeight = Math.Max(0.0001, weight);
            task.Start();
            while (task.State == TaskState.Loading) {
                cancellationToken.ThrowIfCancellationRequested();
                var taskProgress = task.Progress < 0 ? 0 : task.Progress;
                SetProgress((completedWeight + safeWeight * taskProgress) / totalWeight);
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
            }
            if (task.State == TaskState.Failed && task.Error is not null) throw task.Error;
            if (task.State == TaskState.Canceled) throw new OperationCanceledException(cancellationToken);
            completedWeight += safeWeight;
            SetProgress(completedWeight / totalWeight);
        }
    }

}
