namespace PCL.Core.Audio;

/// <summary>
/// 背景音乐播放列表：负责文件过滤、随机/顺序排序与播放队列的重建。
/// 由 VB 版 ModMusic 的列表部分迁移而来，音频解码交给上层。
/// </summary>
public sealed class MusicPlaylist {

    /// <summary>不视为音乐文件的扩展名（VB 版按黑名单过滤文件夹里的杂项）。</summary>
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase) {
        ".ini", ".jpg", ".jpeg", ".txt", ".cfg", ".lrc", ".db", ".png"
    };

    private readonly IReadOnlyList<string> _all;
    private readonly bool _shuffle;
    private readonly Random _random;
    private readonly List<string> _queue = [];

    private MusicPlaylist(IReadOnlyList<string> all, bool shuffle, Random? random) {
        _all = all;
        _shuffle = shuffle;
        _random = random ?? Random.Shared;
        Rebuild();
    }

    /// <summary>全部可用音乐文件。</summary>
    public IReadOnlyList<string> AllFiles => _all;

    /// <summary>剩余待播放的队列。</summary>
    public IReadOnlyList<string> Queue => _queue;

    /// <summary>当前正在播放的文件，未开始播放时为 <c>null</c>。</summary>
    public string? Current { get; private set; }

    public bool IsEmpty => _all.Count == 0;

    /// <summary>从文件集合创建播放列表。</summary>
    public static MusicPlaylist FromFiles(IEnumerable<string> files, bool shuffle = false, Random? random = null) {
        ArgumentNullException.ThrowIfNull(files);
        var list = files
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new MusicPlaylist(list, shuffle, random);
    }

    /// <summary>扫描目录创建播放列表。目录不存在时返回空列表。</summary>
    public static MusicPlaylist FromFolder(string folder, bool shuffle = false, Random? random = null) {
        ArgumentNullException.ThrowIfNull(folder);
        if (!Directory.Exists(folder)) return FromFiles([], shuffle, random);
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(IsMusicFile)
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);
        return FromFiles(files, shuffle, random);
    }

    /// <summary>判断扩展名是否属于音乐文件。</summary>
    public static bool IsMusicFile(string path) =>
        !string.IsNullOrWhiteSpace(path) && !IgnoredExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// 取下一首。队列用尽时自动重建，并避免让刚播完的那首重新排到最前面。
    /// 列表为空时返回 <c>null</c>。
    /// </summary>
    public string? Next() {
        if (_all.Count == 0) return null;
        if (_queue.Count == 0) Rebuild();
        var previous = Current;
        var next = _queue[0];
        _queue.RemoveAt(0);
        if (_queue.Count == 0) Rebuild(previous);
        Current = next;
        return next;
    }

    /// <summary>重新生成队列。</summary>
    public void Reset() {
        _queue.Clear();
        Rebuild(Current);
    }

    private void Rebuild(string? avoidFirst = null) {
        var ordered = _shuffle ? Shuffle(_all, _random) : _all.ToList();
        if (avoidFirst is not null && ordered.Count > 1
            && string.Equals(ordered[0], avoidFirst, StringComparison.OrdinalIgnoreCase)) {
            ordered.RemoveAt(0);
            ordered.Add(avoidFirst);
        }
        _queue.Clear();
        _queue.AddRange(ordered);
    }

    private static List<string> Shuffle(IReadOnlyList<string> source, Random random) {
        var result = source.ToList();
        for (var i = result.Count - 1; i > 0; i--) {
            var j = random.Next(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return result;
    }

}
