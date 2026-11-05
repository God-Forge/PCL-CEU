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

    /// <summary>玩家头部渲染地址。</summary>
    public static string Head(string? uuid, int scale = 8) {
        var normalized = Normalize(uuid);
        var bounded = Math.Clamp(scale, 1, 16);
        return $"https://crafatar.com/renders/head/{normalized}?scale={bounded}&overlay";
    }

    /// <summary>披风渲染地址。没有披风时服务会返回 404，由调用方降级处理。</summary>
    public static string Cape(string? uuid, int scale = 8) {
        var normalized = Normalize(uuid);
        var bounded = Math.Clamp(scale, 1, 16);
        return $"https://crafatar.com/renders/cape/{normalized}?scale={bounded}";
    }

    /// <summary>
    /// 生成适合预览窗口的图片地址列表：头部、全身，以及在有披风时附加披风。
    /// </summary>
    public static IReadOnlyList<(string Name, string Url)> PreviewImages(string? uuid, bool hasCape) {
        if (!IsValidUuid(uuid)) return [];
        var images = new List<(string Name, string Url)> {
            ("头部", Head(uuid, 12)),
            ("全身", Render(uuid, 10))
        };
        if (hasCape) images.Add(("披风", Cape(uuid, 10)));
        return images;
    }

}
