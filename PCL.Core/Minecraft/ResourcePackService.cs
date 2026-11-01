namespace PCL.Core.Minecraft;

public sealed class ResourcePackInfo {

    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Enabled { get; init; }
    public bool IsShader { get; init; }
    public long Size { get; init; }

}

public sealed class ResourcePackScanResult {

    public IReadOnlyList<ResourcePackInfo> Packs { get; init; } = Array.Empty<ResourcePackInfo>();
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

}

/// <summary>
/// 扫描、启用与禁用资源包/光影包。
/// </summary>
public static class ResourcePackService {

    public static ResourcePackScanResult Scan(string instanceFolder) {
        var packs = new List<ResourcePackInfo>();
        var issues = new List<string>();
        foreach (var (folderName, isShader) in new[] { ("resourcepacks", false), ("shaderpacks", true) }) {
            var folder = Path.Combine(instanceFolder, folderName);
            if (!Directory.Exists(folder)) continue;
            foreach (var path in Directory.EnumerateFileSystemEntries(folder)) {
                var name = Path.GetFileName(path);
                if (Directory.Exists(path)) {
                    packs.Add(new ResourcePackInfo {
                        Name = name,
                        Path = path,
                        Enabled = true,
                        IsShader = isShader,
                        Size = 0
                    });
                    continue;
                }
                var enabled = !name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                var cleanName = enabled ? name : name.Substring(0, name.Length - ".disabled".Length);
                if (!cleanName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    issues.Add($"未识别的文件：{folderName}/{name}");
                    continue;
                }
                packs.Add(new ResourcePackInfo {
                    Name = Path.GetFileNameWithoutExtension(cleanName),
                    Path = path,
                    Enabled = enabled,
                    IsShader = isShader,
                    Size = new FileInfo(path).Length
                });
            }
        }
        foreach (var group in packs.GroupBy(pack => (pack.Name, pack.IsShader)).Where(group => group.Count() > 1)) {
            issues.Add($"存在同名资源包/光影：{group.Key.Name}");
        }
        return new ResourcePackScanResult { Packs = packs, Issues = issues };
    }

    public static string Toggle(string path) {
        if (File.Exists(path)) {
            if (path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) {
                var target = path.Substring(0, path.Length - ".disabled".Length);
                File.Move(path, target);
                return target;
            }
            var disabled = path + ".disabled";
            File.Move(path, disabled);
            return disabled;
        }
        throw new FileNotFoundException("资源包文件不存在。", path);
    }

    public static string Format(ResourcePackScanResult result) {
        if (result.Packs.Count == 0) return "未找到资源包或光影包。";
        var lines = result.Packs.Select(pack =>
            $"[{(pack.IsShader ? "光影" : "资源包")}/{(pack.Enabled ? "启用" : "禁用")}] {pack.Name}");
        var text = $"共找到 {result.Packs.Count} 个资源包/光影：\r\n" + string.Join("\r\n", lines.Take(30));
        if (result.Packs.Count > 30) text += $"\r\n... 另有 {result.Packs.Count - 30} 个";
        if (result.Issues.Count > 0) text += "\r\n\r\n问题：\r\n" + string.Join("\r\n", result.Issues.Take(10));
        return text;
    }

}
