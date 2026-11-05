namespace PCL.Core.Launch;

/// <summary>启动阶段。与 VB 版 PageLinkMain 的 LinkStates 对应。</summary>
public enum LaunchPhase {
    /// <summary>等待中（尚未开始加载）。</summary>
    Waiting,
    /// <summary>加载中。</summary>
    Loading,
    /// <summary>启动失败。</summary>
    Failed,
    /// <summary>启动完成。</summary>
    Finished
}

/// <summary>
/// 启动状态机：维护阶段、进度与失败原因，并拒绝非法的状态回退。
/// 对应 VB 版 PageLinkMain 的 ChangeState / UpdateProgressBar / SwitchToXxx。
/// </summary>
public sealed class LaunchStateMachine {

    private readonly object _gate = new();

    public LaunchPhase Phase { get; private set; } = LaunchPhase.Waiting;

    /// <summary>进度，范围 0~1。</summary>
    public double Progress { get; private set; }

    /// <summary>当前步骤说明，例如「正在检查 Java」。</summary>
    public string? Message { get; private set; }

    /// <summary>失败原因，仅在 <see cref="LaunchPhase.Failed"/> 时有值。</summary>
    public string? FailureReason { get; private set; }

    /// <summary>是否仍在进行中。</summary>
    public bool IsActive => Phase is LaunchPhase.Waiting or LaunchPhase.Loading;

    /// <summary>阶段变化时触发；参数为新阶段。</summary>
    public event Action<LaunchPhase>? PhaseChanged;

    /// <summary>
    /// 尝试切换阶段。相同阶段返回 <c>false</c> 且不触发事件；
    /// 已结束（完成/失败）时除非先重置，否则拒绝切换。
    /// </summary>
    public bool TryTransition(LaunchPhase next, string? message = null) {
        Action<LaunchPhase>? handler;
        lock (_gate) {
            if (Phase == next) return false;
            if (!CanTransition(Phase, next)) return false;
            Phase = next;
            Message = message;
            if (next == LaunchPhase.Waiting) {
                Progress = 0;
                FailureReason = null;
            }
            if (next == LaunchPhase.Finished) Progress = 1;
            handler = PhaseChanged;
        }
        handler?.Invoke(next);
        return true;
    }

    private static bool CanTransition(LaunchPhase from, LaunchPhase to) {
        if (from == to) return false;
        // 完成与失败是终态，必须重置才能重新开始
        if (from is LaunchPhase.Finished or LaunchPhase.Failed) return false;
        return to switch {
            LaunchPhase.Waiting => true,
            LaunchPhase.Loading => true,
            LaunchPhase.Failed => true,
            LaunchPhase.Finished => from == LaunchPhase.Loading,
            _ => false
        };
    }

    /// <summary>更新进度。只在加载中有效，值会被钳制到 0~1。</summary>
    public bool ReportProgress(double value, string? message = null) {
        lock (_gate) {
            if (Phase != LaunchPhase.Loading) return false;
            var clamped = double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
            var changed = Math.Abs(clamped - Progress) > 0.0001
                          || !string.Equals(message, Message, StringComparison.Ordinal);
            Progress = clamped;
            if (!string.IsNullOrWhiteSpace(message)) Message = message;
            return changed;
        }
    }

    /// <summary>标记失败并记录原因。</summary>
    public bool Fail(string? reason) {
        var changed = TryTransition(LaunchPhase.Failed, reason);
        if (changed) {
            lock (_gate) FailureReason = string.IsNullOrWhiteSpace(reason) ? "未知错误" : reason!.Trim();
        }
        return changed;
    }

    /// <summary>标记完成。</summary>
    public bool Finish() => TryTransition(LaunchPhase.Finished, "启动完成");

    /// <summary>重置回等待状态，用于重试。</summary>
    public void Reset() {
        lock (_gate) {
            Phase = LaunchPhase.Waiting;
            Progress = 0;
            Message = null;
            FailureReason = null;
        }
    }

    /// <summary>当前进度对应的百分比文本。</summary>
    public string ProgressText => Phase switch {
        LaunchPhase.Finished => "100%",
        LaunchPhase.Failed => "已中止",
        _ => (Progress * 100).ToString("0") + "%"
    };

}
