using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class ClientJarInfo {

    public string Url { get; init; } = "";
    public string? Sha1 { get; init; }
    public long Size { get; init; } = -1;

}

/// <summary>
/// 读取版本 JSON 中 downloads.client 的信息。
/// </summary>
public static class ClientJarInfoResolver {

    public static ClientJarInfo? Resolve(JObject versionJson) {
        if (versionJson["downloads"]?["client"] is not JObject client) return null;
        var url = client["url"]?.ToString();
        if (string.IsNullOrWhiteSpace(url)) return null;
        return new ClientJarInfo {
            Url = url!,
            Sha1 = client["sha1"]?.ToString(),
            Size = client["size"]?.ToObject<long>() ?? -1
        };
    }

}
