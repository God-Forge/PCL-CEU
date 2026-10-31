using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class MinecraftVersionInfo {

    public string Folder { get; init; } = "";
    public string JsonPath { get; init; } = "";
    public string Id { get; init; } = "";
    public string InheritsFrom { get; init; } = "";
    public string Type { get; init; } = "";
    public DateTime? ReleaseTime { get; init; }
    public string MainClass { get; init; } = "";
    public string Jar { get; init; } = "";
    public string AssetIndex { get; init; } = "";
    public int LibrariesCount { get; init; }
    public string? Error { get; init; }

}

/// <summary>
/// 只读扫描 versions 目录与版本 JSON，不创建 McInstance、不访问 UI。
/// </summary>
public static class MinecraftVersionScanner {

    public static IReadOnlyList<MinecraftVersionInfo> Scan(string mcFolder) {
        var versionsFolder = System.IO.Path.Combine(mcFolder, "versions");
        if (!Directory.Exists(versionsFolder)) return Array.Empty<MinecraftVersionInfo>();

        return Directory.EnumerateDirectories(versionsFolder, "*", SearchOption.TopDirectoryOnly)
            .Select(ScanVersion)
            .OrderByDescending(info => info.ReleaseTime ?? DateTime.MinValue)
            .ToList();
    }

    public static MinecraftVersionInfo ScanVersion(string versionFolder) {
        var folder = System.IO.Path.GetFileName(versionFolder.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        var jsonPath = System.IO.Path.Combine(versionFolder, folder + ".json");
        if (!File.Exists(jsonPath)) {
            jsonPath = Directory.EnumerateFiles(versionFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? "";
        }
        if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath)) {
            return new MinecraftVersionInfo { Folder = folder, Error = "缺少版本 JSON。" };
        }

        try {
            var json = JObject.Parse(File.ReadAllText(jsonPath));
            return new MinecraftVersionInfo {
                Folder = folder,
                JsonPath = jsonPath,
                Id = json["id"]?.ToString() ?? folder,
                InheritsFrom = json["inheritsFrom"]?.ToString() ?? "",
                Type = json["type"]?.ToString() ?? "",
                ReleaseTime = DateTime.TryParse(json["releaseTime"]?.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var time)
                    ? time.ToLocalTime()
                    : null,
                MainClass = json["mainClass"]?.ToString() ?? "",
                Jar = json["jar"]?.ToString() ?? "",
                AssetIndex = json["assetIndex"]?["id"]?.ToString() ?? "",
                LibrariesCount = (json["libraries"] as JArray)?.Count ?? 0
            };
        } catch (Exception ex) {
            return new MinecraftVersionInfo {
                Folder = folder,
                JsonPath = jsonPath,
                Error = "版本 JSON 解析失败：" + ex.Message
            };
        }
    }

}
