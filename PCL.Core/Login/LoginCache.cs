using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Login;

/// <summary>
/// 登录信息的序列化与还原，用于把登录状态持久化到设置中。
/// 与 VB 版 CacheMs* / CacheNide* 等键的持久化行为对应。
/// </summary>
public static class LoginCache {

    /// <summary>序列化为单行 JSON；传入 <c>null</c> 时返回空串（表示清除）。</summary>
    public static string Serialize(LoginProfile? profile) {
        if (profile is null) return "";
        var json = new JObject {
            ["name"] = profile.Name,
            ["uuid"] = profile.Uuid,
            ["accessToken"] = profile.AccessToken,
            ["clientId"] = profile.ClientId,
            ["xuid"] = profile.Xuid,
            ["userType"] = profile.UserType
        };
        return json.ToString(Formatting.None);
    }

    /// <summary>
    /// 从 JSON 还原登录信息。空串、格式错误或缺少用户名时返回 <c>null</c>。
    /// </summary>
    public static LoginProfile? Deserialize(string? text) {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try {
            var json = JObject.Parse(text);
            var name = json["name"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return null;
            return new LoginProfile {
                Name = name,
                Uuid = json["uuid"]?.ToString() ?? "",
                AccessToken = json["accessToken"]?.ToString() ?? "",
                ClientId = json["clientId"]?.ToString() ?? "",
                Xuid = json["xuid"]?.ToString() ?? "",
                UserType = json["userType"]?.ToString() ?? "legacy"
            };
        } catch (JsonException ex) {
            Logger.Warn(ex, "解析登录缓存失败，已忽略");
            return null;
        }
    }

}
