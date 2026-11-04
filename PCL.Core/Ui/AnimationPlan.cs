namespace PCL.Core.Ui;

/// <summary>
/// 缓动风格，对应 VB 版 AniEase 系列。App 层负责映射到 WPF 的缓动函数。
/// </summary>
public enum EasingKind {
    Linear,
    /// <summary>先慢后快。</summary>
    FluentIn,
    /// <summary>先快后慢。</summary>
    FluentOut,
    /// <summary>两端慢中间快。</summary>
    FluentInOut,
    BackIn,
    BackOut,
    CarIn,
    CarOut,
    ElasticIn,
    ElasticOut
}

/// <summary>
/// 一个动画步骤的描述，与具体 UI 框架无关。
/// </summary>
public sealed class AnimationStep {

    /// <summary>属性标识，例如 "Opacity"、"Width"、"TranslateX"。</summary>
    public string Property { get; init; } = "";

    /// <summary>目标值；为 <c>null</c> 表示由调用方按当前值补全。</summary>
    public double? To { get; init; }

    public int DurationMilliseconds { get; init; } = 400;
    public int DelayMilliseconds { get; init; }
    public EasingKind Easing { get; init; } = EasingKind.Linear;

    /// <summary>缓动强度，1 为默认。</summary>
    public double EasingPower { get; init; } = 1;

}

/// <summary>已排好时间的动画步骤。</summary>
public sealed record ScheduledAnimationStep(AnimationStep Step, int BeginMilliseconds, int EndMilliseconds) {

    public int DurationMilliseconds => EndMilliseconds - BeginMilliseconds;

}

/// <summary>
/// 动画时序规划：决定每一步何时开始、何时结束。纯计算，便于测试。
/// </summary>
public static class AnimationPlanner {

    /// <summary>顺序执行：每一步接在上一步结束之后，但仍会叠加自身的延迟。</summary>
    public static IReadOnlyList<ScheduledAnimationStep> Sequential(IEnumerable<AnimationStep> steps) {
        ArgumentNullException.ThrowIfNull(steps);
        var scheduled = new List<ScheduledAnimationStep>();
        var cursor = 0;
        foreach (var step in steps) {
            var begin = cursor + Math.Max(0, step.DelayMilliseconds);
            var end = begin + Math.Max(0, step.DurationMilliseconds);
            scheduled.Add(new ScheduledAnimationStep(step, begin, end));
            cursor = end;
        }
        return scheduled;
    }

    /// <summary>并行执行：全部从 0 开始，各自叠加延迟。</summary>
    public static IReadOnlyList<ScheduledAnimationStep> Parallel(IEnumerable<AnimationStep> steps) {
        ArgumentNullException.ThrowIfNull(steps);
        return steps
            .Select(step => {
                var begin = Math.Max(0, step.DelayMilliseconds);
                return new ScheduledAnimationStep(step, begin, begin + Math.Max(0, step.DurationMilliseconds));
            })
            .ToList();
    }

    /// <summary>整组动画的总时长。</summary>
    public static int TotalDuration(IEnumerable<ScheduledAnimationStep> scheduled) {
        ArgumentNullException.ThrowIfNull(scheduled);
        var total = 0;
        foreach (var step in scheduled) total = Math.Max(total, step.EndMilliseconds);
        return total;
    }

}
