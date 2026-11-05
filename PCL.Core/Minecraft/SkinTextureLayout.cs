namespace PCL.Core.Minecraft;

/// <summary>皮肤图上的一个矩形区域。</summary>
public readonly record struct PixelRegion(int X, int Y, int Width, int Height);

/// <summary>一个身体部件在画布上的摆放。</summary>
public readonly record struct SkinPartPlacement(string Name, PixelRegion Source, int DestX, int DestY);

/// <summary>
/// Minecraft 皮肤纹理的坐标定义。支持 64×64（现代）与 64×32（旧版）两种尺寸。
/// 这些是纯数值，便于在没有图形环境时验证。
/// </summary>
public static class SkinTextureLayout {

    public const int ModernWidth = 64;
    public const int ModernHeight = 64;
    public const int LegacyWidth = 64;
    public const int LegacyHeight = 32;

    /// <summary>是否为可识别的皮肤尺寸。</summary>
    public static bool IsValidSize(int width, int height) =>
        (width == ModernWidth && height == ModernHeight)
        || (width == LegacyWidth && height == LegacyHeight);

    /// <summary>是否为 64×32 的旧版皮肤（无独立左手左腿）。</summary>
    public static bool IsLegacy(int width, int height) => width == LegacyWidth && height == LegacyHeight;

    /// <summary>头部正面（8×8）。</summary>
    public static readonly PixelRegion HeadFace = new(8, 8, 8, 8);

    /// <summary>头部外层（帽子/头发，8×8）。</summary>
    public static readonly PixelRegion HeadOverlay = new(40, 8, 8, 8);

    /// <summary>身体正面（8×12）。</summary>
    public static readonly PixelRegion BodyFront = new(20, 20, 8, 12);

    /// <summary>右臂正面（4×12）。</summary>
    public static readonly PixelRegion RightArm = new(44, 20, 4, 12);

    /// <summary>左臂正面（4×12，仅 64×64 有）。</summary>
    public static readonly PixelRegion LeftArm = new(36, 52, 4, 12);

    /// <summary>右腿正面（4×12）。</summary>
    public static readonly PixelRegion RightLeg = new(4, 20, 4, 12);

    /// <summary>左腿正面（4×12，仅 64×64 有）。</summary>
    public static readonly PixelRegion LeftLeg = new(20, 52, 4, 12);

    /// <summary>头像画布的皮肤像素边长（8×8 的头部）。</summary>
    public const int AvatarPixels = 8;

    /// <summary>全身画布的皮肤像素尺寸：宽 16（手+身+手），高 32（头+身+腿）。</summary>
    public const int BodyPixelsWidth = 16;
    public const int BodyPixelsHeight = 32;

    /// <summary>
    /// 全身正面视图的拼接布局。<paramref name="legacy"/> 为真时，左手左腿复用右侧纹理。
    /// 返回的坐标是皮肤像素单位，调用方按比例放大即可。
    /// </summary>
    public static IReadOnlyList<SkinPartPlacement> BodyPlacements(bool legacy) {
        var placements = new List<SkinPartPlacement> {
            new("head", HeadFace, 4, 0),
            new("body", BodyFront, 4, 8),
            new("rightArm", RightArm, 0, 8),
            new("rightLeg", RightLeg, 4, 20)
        };
        if (legacy) {
            // 旧版皮肤左右共用同一块纹理
            placements.Add(new SkinPartPlacement("leftArm", RightArm, 12, 8));
            placements.Add(new SkinPartPlacement("leftLeg", RightLeg, 8, 20));
        } else {
            placements.Add(new SkinPartPlacement("leftArm", LeftArm, 12, 8));
            placements.Add(new SkinPartPlacement("leftLeg", LeftLeg, 8, 20));
        }
        return placements;
    }

    /// <summary>
    /// 校验布局是否落在纹理范围内（防止越界读取）。
    /// </summary>
    public static bool IsWithinBounds(SkinPartPlacement placement, int textureWidth, int textureHeight) =>
        placement.Source.X >= 0
        && placement.Source.Y >= 0
        && placement.Source.X + placement.Source.Width <= textureWidth
        && placement.Source.Y + placement.Source.Height <= textureHeight;

}
