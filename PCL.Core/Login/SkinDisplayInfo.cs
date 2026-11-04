using PCL.Core.Minecraft;

namespace PCL.Core.Login;

/// <summary>
/// 皮肤页展示所需的信息。集中处理「已登录 / 未登录」的降级，避免 UI 各自判断。
/// </summary>
public sealed class SkinDisplayInfo {

    public string Name { get; init; } = "";
    public string Uuid { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public string RenderUrl { get; init; } = "";

    /// <summary>是否有可展示的皮肤（需要合法的 UUID）。</summary>
    public bool HasSkin => !string.IsNullOrEmpty(Uuid) && SkinUrlBuilder.IsValidUuid(Uuid);

    /// <summary>界面上显示的状态文字。</summary>
    public string StatusText => HasSkin ? Name : "未登录";

    /// <summary>从登录状态构造展示信息；未登录或 UUID 非法时返回占位信息。</summary>
    public static SkinDisplayInfo From(LoginProfile? profile) {
        if (profile is null) return Placeholder();
        var uuid = SkinUrlBuilder.Normalize(profile.Uuid);
        if (!SkinUrlBuilder.IsValidUuid(uuid)) {
            return new SkinDisplayInfo {
                Name = string.IsNullOrWhiteSpace(profile.Name) ? "未登录" : profile.Name,
                Uuid = uuid
            };
        }
        return new SkinDisplayInfo {
            Name = profile.Name,
            Uuid = uuid,
            AvatarUrl = SkinUrlBuilder.Avatar(uuid, 64),
            RenderUrl = SkinUrlBuilder.Render(uuid)
        };
    }

    public static SkinDisplayInfo Placeholder() => new() { Name = "未登录" };

}
