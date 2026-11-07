using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>变更类型。</summary>
public enum RecentChangeKind {
    ModInstall,
    ModUpdate,
    ModRemove,
    SettingChange,
    InstanceCreate,
    Other
}

/// <summary>一条最近发生的改动。</summary>
public sealed class RecentChange {

    public DateTime Time { get; init; } = DateTime.Now;
    public RecentChangeKind Kind { get; init; }
    public string Target { get; init; } = "";
    public string Detail { get; init; } = "";

}

/// <summary>
/// 记录「最近做了什么改动」，崩溃时据此告诉用户可能是哪一步引入的问题。
/// 只保留最近若干条，避免无限增长。
/// </summary>
public static class RecentChangeLog {

    public const int MaxEntries = 100;

    /// <summary>追加一条记录；文件不存在时自动创建。</summary>
    public static void Record(string path, RecentChange change) {
        ArgumentNullException.ThrowIfNull(change);
        try {
            var all = Load(path).ToList();
            all.Add(change);
            if (all.Count > MaxEntries) all = all.Skip(all.Count - MaxEntries).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var array = new JArray(all.Select(item => new JObject {
                ["time"] = item.Time.ToString("O"),
                ["kind"] = item.Kind.ToString(),
                ["target"] = item.Target,
                ["detail"] = item.Detail
            }));
            File.WriteAllText(path, array.ToString(Formatting.None), new System.Text.UTF8Encoding(false));
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "记录最近改动失败");
        }
    }

    /// <summary>读取全部记录（按时间升序）；文件缺失或损坏时返回空。</summary>
    public static IReadOnlyList<RecentChange> Load(string path) {
        if (!File.Exists(path)) return [];
        try {
            var text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return [];
            return (JArray.Parse(text) ?? [])
                .OfType<JObject>()
                .Select(item => new RecentChange {
                    Time = DateTime.TryParse(item["time"]?.ToString(), out var time) ? time : DateTime.MinValue,
                    Kind = Enum.TryParse<RecentChangeKind>(item["kind"]?.ToString(), out var kind)
                        ? kind
                        : RecentChangeKind.Other,
                    Target = item["target"]?.ToString() ?? "",
                    Detail = item["detail"]?.ToString() ?? ""
                })
                .OrderBy(change => change.Time)
                .ToList();
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取最近改动失败");
            return [];
        }
    }

    /// <summary>筛选窗口期内的记录（例如崩溃前 2 小时）。</summary>
    public static IReadOnlyList<RecentChange> Within(
        IEnumerable<RecentChange>? changes,
        TimeSpan window,
        DateTime? now = null) {
        if (changes is null) return [];
        var reference = now ?? DateTime.Now;
        return changes
            .Where(change => change.Time >= reference - window && change.Time <= reference)
            .OrderBy(change => change.Time)
            .ToList();
    }

    /// <summary>把改动整理成给崩溃报告用的多行文本；没有改动时返回空串。</summary>
    public static string Describe(IReadOnlyList<RecentChange>? changes) {
        if (changes is null || changes.Count == 0) return "";
        var lines = changes
            .Select(change => $"{change.Time:HH:mm} {Label(change.Kind)} {change.Target}"
                              + (string.IsNullOrWhiteSpace(change.Detail) ? "" : $"（{change.Detail}）"))
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public static string Label(RecentChangeKind kind) => kind switch {
        RecentChangeKind.ModInstall => "安装 Mod",
        RecentChangeKind.ModUpdate => "更新 Mod",
        RecentChangeKind.ModRemove => "删除 Mod",
        RecentChangeKind.SettingChange => "修改设置",
        RecentChangeKind.InstanceCreate => "新建实例",
        _ => "其他改动"
    };

}
