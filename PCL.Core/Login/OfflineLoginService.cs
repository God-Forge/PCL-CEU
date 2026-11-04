using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.Login;

public sealed class LoginProfile {

    public string Name { get; init; } = "";
    public string Uuid { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string Xuid { get; init; } = "";
    public string UserType { get; init; } = "legacy";

}

/// <summary>
/// 离线登录：与 PCL 的离线 UUID 规则一致，不访问网络。
/// </summary>
public static class OfflineLoginService {

    public static LoginProfile Create(string name) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("用户名不能为空。", nameof(name));
        var uuid = CreateOfflineUuid(name);
        return new LoginProfile {
            Name = name,
            Uuid = uuid,
            AccessToken = Guid.NewGuid().ToString("N"),
            UserType = "legacy"
        };
    }

    public static string CreateOfflineUuid(string name) {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return hex.Substring(0, 8) + "-" + hex.Substring(8, 4) + "-" + hex.Substring(12, 4) + "-" + hex.Substring(16, 4) + "-" + hex.Substring(20);
    }

}
