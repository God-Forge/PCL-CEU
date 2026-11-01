using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class AssetIndexInfo {

    public string Id { get; init; } = "";
    public string Url { get; init; } = "";

}

/// <summary>
/// 读取 assets index 信息中的 id 与 url。
/// </summary>
public static class AssetIndexInfoResolver {

    public static AssetIndexInfo Resolve(JToken indexInfo) {
        return new AssetIndexInfo {
            Id = indexInfo["id"]?.ToString() ?? "",
            Url = indexInfo["url"]?.ToString() ?? ""
        };
    }

}
