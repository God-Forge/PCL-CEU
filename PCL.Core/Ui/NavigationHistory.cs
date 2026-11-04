namespace PCL.Core.Ui;

/// <summary>
/// 页面导航历史。只记录页面标识，不依赖任何 UI 类型，便于测试。
/// </summary>
public sealed class NavigationHistory {

    /// <summary>最多保留的历史深度，超出后丢弃最旧的记录。</summary>
    public const int MaxDepth = 32;

    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    /// <summary>当前页面，栈空时为 <c>null</c>。</summary>
    public string? Current => _entries.Count > 0 ? _entries[^1] : null;

    /// <summary>是否还有上一页可返回。</summary>
    public bool CanGoBack => _entries.Count > 1;

    /// <summary>
    /// 进入新页面。与当前页面相同时不重复入栈（避免刷新页面时堆积历史）。
    /// </summary>
    public void Push(string? page) {
        if (string.IsNullOrWhiteSpace(page)) return;
        if (Current is not null && string.Equals(Current, page, StringComparison.Ordinal)) return;
        _entries.Add(page);
        if (_entries.Count > MaxDepth) _entries.RemoveRange(0, _entries.Count - MaxDepth);
    }

    /// <summary>返回上一页，返回新的当前页面；无法返回时返回 <c>null</c>。</summary>
    public string? Pop() {
        if (!CanGoBack) return null;
        _entries.RemoveAt(_entries.Count - 1);
        return Current;
    }

    /// <summary>重置为指定页面（通常是首页）。</summary>
    public void Reset(string? page = null) {
        _entries.Clear();
        Push(page);
    }

    public void Clear() => _entries.Clear();

}
