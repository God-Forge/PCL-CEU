using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class InstanceCloneOptions {

    public bool IncludeSaves { get; init; }
    public bool IncludeMods { get; init; } = true;
    public bool IncludeConfig { get; init; } = true;
    public bool IncludeResourcePacks { get; init; } = true;
    public bool IncludeShaderPacks { get; init; } = true;
    public bool Overwrite { get; init; }

}

/// <summary>
/// 克隆 Minecraft 实例，并修正版本 JSON 的 id。
/// </summary>
public static class InstanceCloneService {

    public static string Clone(string sourceFolder, string targetFolder, InstanceCloneOptions options) {
        sourceFolder = Path.GetFullPath(sourceFolder).TrimEnd(Path.DirectorySeparatorChar);
        targetFolder = Path.GetFullPath(targetFolder).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(sourceFolder)) throw new DirectoryNotFoundException("源实例不存在：" + sourceFolder);
        if (Directory.Exists(targetFolder)) {
            if (!options.Overwrite) throw new IOException("目标实例已存在：" + targetFolder);
            Directory.Delete(targetFolder, true);
        }

        var sourceName = Path.GetFileName(sourceFolder);
        var targetName = Path.GetFileName(targetFolder);
        Directory.CreateDirectory(targetFolder);
        foreach (var file in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)) {
            var relative = file.Substring(sourceFolder.Length + 1);
            var top = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
            if (!ShouldCopy(top, options)) continue;
            var destination = Path.Combine(targetFolder, relative);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.Copy(file, destination, true);
        }

        var sourceJson = Path.Combine(sourceFolder, sourceName + ".json");
        if (!File.Exists(sourceJson)) {
            sourceJson = Directory.EnumerateFiles(sourceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                         ?? throw new FileNotFoundException("源实例缺少版本 JSON。", sourceJson);
        }
        var targetJson = Path.Combine(targetFolder, targetName + ".json");
        var json = JObject.Parse(File.ReadAllText(sourceJson));
        json["id"] = targetName;
        File.WriteAllText(targetJson, json.ToString());
        var oldJson = Path.Combine(targetFolder, Path.GetFileName(sourceJson));
        if (!string.Equals(oldJson, targetJson, StringComparison.OrdinalIgnoreCase)) File.Delete(oldJson);
        return targetFolder;
    }

    private static bool ShouldCopy(string top, InstanceCloneOptions options) {
        if (top.Equals("saves", StringComparison.OrdinalIgnoreCase)) return options.IncludeSaves;
        if (top.Equals("mods", StringComparison.OrdinalIgnoreCase)) return options.IncludeMods;
        if (top.Equals("config", StringComparison.OrdinalIgnoreCase)) return options.IncludeConfig;
        if (top.Equals("resourcepacks", StringComparison.OrdinalIgnoreCase)) return options.IncludeResourcePacks;
        if (top.Equals("shaderpacks", StringComparison.OrdinalIgnoreCase)) return options.IncludeShaderPacks;
        if (top.Equals("logs", StringComparison.OrdinalIgnoreCase)
            || top.Equals("crash-reports", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

}

