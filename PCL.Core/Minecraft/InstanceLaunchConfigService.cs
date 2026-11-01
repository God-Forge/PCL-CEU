using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>
/// 导出/导入实例启动配置的 JSON 文件。
/// </summary>
public static class InstanceLaunchConfigService {

    public static void Export(string path, IReadOnlyDictionary<string, object?> values, string instanceName) {
        var json = new JObject {
            ["instance"] = instanceName,
            ["exported"] = DateTime.Now.ToString("O")
        };
        var settings = new JObject();
        foreach (var pair in values) settings[pair.Key] = pair.Value is null ? JValue.CreateNull() : JToken.FromObject(pair.Value);
        json["settings"] = settings;
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, json.ToString(), new UTF8Encoding(false));
    }

    public static IReadOnlyDictionary<string, JToken> Import(string path) {
        var json = JObject.Parse(File.ReadAllText(path));
        if (json["settings"] is not JObject settings) throw new InvalidDataException("启动配置文件缺少 settings。");
        return settings.Properties().ToDictionary(property => property.Name, property => property.Value);
    }

}
