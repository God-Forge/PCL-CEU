using System.Text.RegularExpressions;

namespace PCL.Core.Ui;

/// <summary>提示条类型，对应 VB 版 HintType。</summary>
public enum HintKind {
    Blue,
    Green,
    Red
}

/// <summary>入队结果。</summary>
public enum HintOutcome {
    /// <summary>新增了一条提示。</summary>
    Added,
    /// <summary>已存在相同内容，刷新了它的显示时间。</summary>
    Refreshed,
    /// <summary>被丢弃（空文本或超出上限）。</summary>
    Dropped
}

/// <summary>一条提示。</summary>
public sealed class HintEntry {

    public string Text { get; init; } = "";
    public HintKind Kind { get; init; }
    public bool Log { get; init; }
    public int DisplayMilliseconds { get; init; }

    /// <summary>重复出现时递增，用于触发界面重播动画。</summary>
    public int Revision { get; internal set; }

}

/// <summary>入队结果与对应的提示条目。</summary>
public sealed record HintEnqueueResult(HintOutcome Outcome, HintEntry? Entry);

/// <summary>
/// 提示条队列：负责文本归一化、去重、容量限制与显示时长计算。
/// 由 VB 版 ModMain 的 Hint 逻辑迁移而来，界面展示交给上层。
/// </summary>
public sealed class HintQueue {

    /// <summary>同时显示的提示上限，超过直接丢弃。</summary>
    public const int MaxVisible = 20;

    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);

    private readonly List<HintEntry> _visible = [];

    /// <summary>当前仍在显示的提示。</summary>
    public IReadOnlyList<HintEntry> Visible => _visible;

    /// <summary>把多行文本压成一行并合并多余空白。</summary>
    public static string Normalize(string? text) {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return WhitespacePattern.Replace(text.ReplaceLineEndings(" "), " ").Trim();
    }

    /// <summary>
    /// 显示时长：基础 800 毫秒，按文本长度每字 180 毫秒，长度限制在 5 ~ 23 之间。
    /// </summary>
    public static int ComputeDisplayMilliseconds(string text, double speed = 1) {
        var length = Math.Clamp(text?.Length ?? 0, 5, 23);
        var baseValue = 800 + length * 180;
        return (int)Math.Round(baseValue * (speed <= 0 ? 1 : speed));
    }

    /// <summary>
    /// 入队一条提示。相同内容会刷新已有条目的显示时间而不是新增。
    /// </summary>
    public HintEnqueueResult Enqueue(string? text, HintKind kind = HintKind.Blue, bool log = true, double speed = 1) {
        var normalized = Normalize(text);
        if (normalized.Length == 0) return new HintEnqueueResult(HintOutcome.Dropped, null);
        var existing = _visible.FirstOrDefault(entry => string.Equals(entry.Text, normalized, StringComparison.Ordinal));
        if (existing is not null) {
            existing.Revision++;
            return new HintEnqueueResult(HintOutcome.Refreshed, existing);
        }
        if (_visible.Count >= MaxVisible) return new HintEnqueueResult(HintOutcome.Dropped, null);
        var entry = new HintEntry {
            Text = normalized,
            Kind = kind,
            Log = log,
            DisplayMilliseconds = ComputeDisplayMilliseconds(normalized, speed)
        };
        _visible.Add(entry);
        return new HintEnqueueResult(HintOutcome.Added, entry);
    }

    /// <summary>移除已消失的提示。</summary>
    public bool Remove(HintEntry? entry) => entry is not null && _visible.Remove(entry);

    public void Clear() => _visible.Clear();

}
