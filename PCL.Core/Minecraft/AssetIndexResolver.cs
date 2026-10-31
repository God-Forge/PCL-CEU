using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class AssetObjectInfo {

    public string LocalPath { get; init; } = "";
    public string SourcePath { get; init; } = "";
    public long Size { get; init; }
    public string Hash { get; init; } = "";

}

/// <summary>
/// 将 assets index 的 objects 解析为本地路径（支持 map_to_resources 与 virtual）。
/// </summary>
public static class AssetIndexResolver {

    public static IReadOnlyList<AssetObjectInfo> ResolveObjects(JObject indexJson, string instancePathIndie, string mcFolder) {
        var result = new List<AssetObjectInfo>();
        if (indexJson["objects"] is not JObject objects) return result;

        var mapToResources = indexJson["map_to_resources"]?.ToObject<bool>() == true;
        var virtualAssets = indexJson["virtual"]?.ToObject<bool>() == true;
        foreach (var property in objects.Properties()) {
            var hash = property.Value["hash"]?.ToString() ?? "";
            var size = property.Value["size"]?.ToObject<long>() ?? 0;
            string path;
            if (mapToResources) {
                path = Path.Combine(instancePathIndie, "resources", property.Name);
            } else if (virtualAssets) {
                path = Path.Combine(mcFolder, "assets", "virtual", "legacy", property.Name);
            } else {
                var prefix = hash.Length >= 2 ? hash.Substring(0, 2) : hash;
                path = Path.Combine(mcFolder, "assets", "objects", prefix, hash);
            }
            result.Add(new AssetObjectInfo {
                LocalPath = path.Replace('/', Path.DirectorySeparatorChar),
                SourcePath = property.Name,
                Hash = hash,
                Size = size
            });
        }
        return result;
    }

}
