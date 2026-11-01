using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class CrashHistoryEntry {

    [JsonProperty("time")] public DateTime Time { get; init; } = DateTime.Now;
    [JsonProperty("instance")] public string Instance { get; init; } = "";
    [JsonProperty("exitCode")] public int ExitCode { get; init; }
    [JsonProperty("reasons")] public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
    [JsonProperty("summary")] public string Summary { get; init; } = "";

}

/// <summary>
/// 保存最近崩溃记录，供崩溃历史与诊断摘要使用。
/// </summary>
public static class CrashHistoryService {

    private static readonly object Gate = new();

    public static IReadOnlyList<CrashHistoryEntry> List(string path) {
        lock (Gate) {
            return Load(path);
        }
    }

    public static CrashHistoryEntry? Latest(string path) {
        return List(path).FirstOrDefault();
    }

    public static void Add(string path, CrashHistoryEntry entry, int maximum = 50) {
        lock (Gate) {
            var entries = Load(path).ToList();
            entries.Insert(0, entry);
            if (entries.Count > maximum) entries.RemoveRange(maximum, entries.Count - maximum);
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonConvert.SerializeObject(entries, Formatting.Indented), new UTF8Encoding(false));
        }
    }

    public static string Format(string path, int maximum = 10) {
        var entries = List(path).Take(maximum).ToList();
        if (entries.Count == 0) return "暂无崩溃记录。";
        return string.Join("\r\n\r\n", entries.Select(entry => {
            var reasons = entry.Reasons.Count == 0 ? "未确定原因" : string.Join("；", entry.Reasons);
            var exit = entry.ExitCode == 0 ? "" : $"（退出码 {entry.ExitCode}：{ExitCodeAnalyzer.Analyze(entry.ExitCode)?.Description ?? "未知"}）";
            return $"{entry.Time:yyyy-MM-dd HH:mm:ss}  {entry.Instance}{exit}\r\n原因：{reasons}";
        }));
    }

    private static List<CrashHistoryEntry> Load(string path) {
        if (!File.Exists(path)) return new List<CrashHistoryEntry>();
        try {
            var array = JArray.Parse(File.ReadAllText(path));
            return array.OfType<JObject>().Select(token => new CrashHistoryEntry {
                Time = token["time"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                Instance = token["instance"]?.ToString() ?? "",
                ExitCode = token["exitCode"]?.ToObject<int>() ?? 0,
                Reasons = token["reasons"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                Summary = token["summary"]?.ToString() ?? ""
            }).ToList();
        } catch {
            return new List<CrashHistoryEntry>();
        }
    }

}
