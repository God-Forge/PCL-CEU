namespace PCL.Core.Minecraft;

/// <summary>
/// 玩家皮肤/头像地址构造。迁移自 VB 版登录页的皮肤显示逻辑，保持纯函数便于测试。
/// </summary>
public static class SkinUrlBuilder {

    private const string AvatarEndpoint = "https://crafatar.com/avatars/";
    private const string RenderEndpoint = "https://crafatar.com/renders/body/";

    /// <summary>去掉 UUID 中的横线并转小写。</summary>
    public static string Normalize(string? uuid) =>
        (uuid ?? "").Replace("-", "").Trim().ToLowerInvariant();

    /// <summary>是否为合法的 32 位十六进制 UUID（允许带横线）。</summary>
    public static bool IsValidUuid(string? uuid) {
        var normalized = Normalize(uuid);
        return normalized.Length == 32 && normalized.All(Uri.IsHexDigit);
    }

    /// <summary>玩家头像地址。</summary>
    public static string Avatar(string? uuid, int size = 64) {
        var normalized = Normalize(uuid);
        var bounded = Math.Clamp(size, 8, 512);
        return $"{AvatarEndpoint}{normalized}?size={bounded}&overlay";
    }

    /// <summary>玩家全身渲染地址。</summary>
    public static string Render(string? uuid, int scale = 8) {
        var normalized = Normalize(uuid);
        var bounded = Math.Clamp(scale, 1, 16);
        return $"{RenderEndpoint}{normalized}?scale={bounded}";
    }

}
