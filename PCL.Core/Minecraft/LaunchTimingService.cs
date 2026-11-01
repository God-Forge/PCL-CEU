using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace PCL.Core.Minecraft;

public sealed class LaunchTimingRecord {

    public string Instance { get; init; } = "";
    public DateTime Time { get; init; } = DateTime.Now;
    public long TotalMilliseconds { get; init; }
    public Dictionary<string, long> Marks { get; init; } = new();

}

/// <summary>
/// 记录启动阶段耗时并保存最近记录。
/// </summary>
public static class LaunchTimingService {

    private static readonly object Gate = new();
    private static Stopwatch? _watch;
    private static string _instance = "";
    private static readonly Dictionary<string, long> Marks = new();

    public static void Begin(string instanceName) {
        lock (Gate) {
            _instance = instanceName;
            Marks.Clear();
            _watch = Stopwatch.StartNew();
        }
    }

    public static void Mark(string name) {
        lock (Gate) {
            if (_watch is null) return;
            Marks[name] = _watch.ElapsedMilliseconds;
        }
    }

    public static LaunchTimingRecord? Finish(string historyPath) {
        lock (Gate) {
            if (_watch is null) return null;
            Mark("结束");
            var record = new LaunchTimingRecord {
                Instance = _instance,
                TotalMilliseconds = _watch.ElapsedMilliseconds,
                Marks = new Dictionary<string, long>(Marks)
            };
            _watch = null;
            var entries = Load(historyPath).ToList();
            entries.Insert(0, record);
            if (entries.Count > 50) entries.RemoveRange(50, entries.Count - 50);
            var directory = Path.GetDirectoryName(Path.GetFullPath(historyPath));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(historyPath, JsonConvert.SerializeObject(entries, Formatting.Indented), new UTF8Encoding(false));
            return record;
        }
    }

    public static IReadOnlyList<LaunchTimingRecord> List(string historyPath) {
        lock (Gate) {
            return Load(historyPath);
        }
    }

    public static string Format(IReadOnlyList<LaunchTimingRecord> records, int maximum = 10) {
        if (records.Count == 0) return "暂无启动耗时记录。";
        return string.Join("\r\n", records.Take(maximum).Select(record => {
            var marks = string.Join("，", record.Marks.Select(pair => $"{pair.Key} {pair.Value}ms"));
            return $"{record.Time:MM-dd HH:mm}  {record.Instance}  总计 {record.TotalMilliseconds}ms\r\n{marks}";
        }));
    }

    private static List<LaunchTimingRecord> Load(string path) {
        if (!File.Exists(path)) return new List<LaunchTimingRecord>();
        try {
            return JsonConvert.DeserializeObject<List<LaunchTimingRecord>>(File.ReadAllText(path)) ?? new List<LaunchTimingRecord>();
        } catch {
            return new List<LaunchTimingRecord>();
        }
    }

}
