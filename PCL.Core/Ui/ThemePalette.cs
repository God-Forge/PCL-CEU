namespace PCL.Core.Ui;

/// <summary>不依赖 UI 框架的 RGB 颜色。</summary>
public readonly record struct RgbColor(byte R, byte G, byte B) {

    /// <summary>按比例混合两种颜色，<paramref name="weight"/> 为 0 时返回 <paramref name="from"/>。</summary>
    public static RgbColor Mix(RgbColor from, RgbColor to, double weight) {
        var w = Math.Clamp(weight, 0, 1);
        return new RgbColor(
            (byte)Math.Round(from.R + (to.R - from.R) * w),
            (byte)Math.Round(from.G + (to.G - from.G) * w),
            (byte)Math.Round(from.B + (to.B - from.B) * w));
    }

    /// <summary>按 HSL 生成颜色。色相 0~360，饱和度与亮度 0~100。</summary>
    public static RgbColor FromHsl(double hue, double saturation, double lightness) {
        var h = ((hue % 360) + 360) % 360;
        var s = Math.Clamp(saturation, 0, 100) / 100;
        var l = Math.Clamp(lightness, 0, 100) / 100;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = l - c / 2;
        var (r, g, b) = h switch {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };
        return new RgbColor(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

}

/// <summary>
/// 主题色板。由 VB 版 ModSecret 的 ThemeRefresh 迁移而来：
/// CEU 版固定蓝色主色相（210）且不提供主题切换。
/// </summary>
public sealed class ThemePalette {

    /// <summary>CEU 固定主色相。</summary>
    public const int DefaultHue = 210;

    /// <summary>CEU 固定饱和度。</summary>
    public const int DefaultSaturation = 85;

    /// <summary>主背景纯色（未开启彩色背景时使用）。</summary>
    public static readonly RgbColor PlainFormBackground = new(245, 245, 245);

    public int Hue { get; private init; }
    public int Saturation { get; private init; }
    public int LightAdjust { get; private init; }

    public RgbColor Color1 { get; private init; }
    public RgbColor Color2 { get; private init; }
    public RgbColor Color3 { get; private init; }
    public RgbColor Color4 { get; private init; }
    public RgbColor Color5 { get; private init; }
    public RgbColor Color6 { get; private init; }
    public RgbColor Color7 { get; private init; }
    public RgbColor Color8 { get; private init; }
    public RgbColor ColorBg0 { get; private init; }
    public RgbColor ColorBg1 { get; private init; }

    public RgbColor Gray1 { get; } = new(64, 64, 64);
    public RgbColor Gray2 { get; } = new(115, 115, 115);
    public RgbColor Gray3 { get; } = new(140, 140, 140);
    public RgbColor Gray4 { get; } = new(166, 166, 166);
    public RgbColor Gray5 { get; } = new(204, 204, 204);
    public RgbColor Gray6 { get; } = new(235, 235, 235);
    public RgbColor Gray7 { get; } = new(240, 240, 240);
    public RgbColor Gray8 { get; } = new(245, 245, 245);

    /// <summary>按资源字典键名生成的主色，例如 <c>ColorBrush1</c>。</summary>
    public IReadOnlyDictionary<string, RgbColor> BrushColors => new Dictionary<string, RgbColor> {
        ["ColorBrush1"] = Color1,
        ["ColorBrush2"] = Color2,
        ["ColorBrush3"] = Color3,
        ["ColorBrush4"] = Color4,
        ["ColorBrush5"] = Color5,
        ["ColorBrush6"] = Color6,
        ["ColorBrush7"] = Color7,
        ["ColorBrush8"] = Color8,
        ["ColorBrushBg0"] = ColorBg0,
        ["ColorBrushBg1"] = ColorBg1,
        ["ColorBrushGray1"] = Gray1,
        ["ColorBrushGray2"] = Gray2,
        ["ColorBrushGray3"] = Gray3,
        ["ColorBrushGray4"] = Gray4,
        ["ColorBrushGray5"] = Gray5,
        ["ColorBrushGray6"] = Gray6,
        ["ColorBrushGray7"] = Gray7,
        ["ColorBrushGray8"] = Gray8
    };

    /// <summary>
    /// 生成色板。默认值即 CEU 的固定蓝色主题。
    /// </summary>
    public static ThemePalette Create(int hue = DefaultHue, int saturation = DefaultSaturation, int lightAdjust = 0) {
        var color1 = RgbColor.FromHsl(hue, saturation * 0.2, 25 + lightAdjust * 0.3);
        var color2 = RgbColor.FromHsl(hue, saturation, 45 + lightAdjust);
        var color3 = RgbColor.FromHsl(hue, saturation, 55 + lightAdjust);
        var color4 = RgbColor.FromHsl(hue, saturation, 65 + lightAdjust);
        var color5 = RgbColor.FromHsl(hue, saturation, 80 + lightAdjust * 0.4);
        var color6 = RgbColor.FromHsl(hue, saturation, 91 + lightAdjust * 0.1);
        var color7 = RgbColor.FromHsl(hue, saturation, 95);
        var color8 = RgbColor.FromHsl(hue, saturation, 97);
        var bg0 = RgbColor.Mix(
            RgbColor.Mix(color4, color5, 0.5),
            new RgbColor(166, 166, 166),
            0.2);
        return new ThemePalette {
            Hue = hue,
            Saturation = saturation,
            LightAdjust = lightAdjust,
            Color1 = color1,
            Color2 = color2,
            Color3 = color3,
            Color4 = color4,
            Color5 = color5,
            Color6 = color6,
            Color7 = color7,
            Color8 = color8,
            ColorBg0 = bg0,
            ColorBg1 = color7
        };
    }

    /// <summary>
    /// 顶部标题条的三段渐变颜色（对应 VB 版 ThemeRefresh 里的顶条背景）。
    /// <paramref name="delta"/> 为色相偏移量，三段分别为 -delta / 0 / +delta。
    /// </summary>
    public static IReadOnlyList<RgbColor> TopbarGradient(
        double hue, double saturation, double lightAdjust, double delta) {
        var offset = Math.Abs(delta);
        return [
            RgbColor.FromHsl(hue - offset, saturation, 48 + lightAdjust),
            RgbColor.FromHsl(hue, saturation, 54 + lightAdjust),
            RgbColor.FromHsl(hue + offset, saturation, 48 + lightAdjust)
        ];
    }

    /// <summary>
    /// 彩色主背景的对角渐变颜色（对应 UiBackgroundColorful 开启时的背景）。
    /// </summary>
    public static IReadOnlyList<RgbColor> FormGradient(double hue, double saturation) {
        var desaturated = Math.Clamp(saturation * 0.8, 0, 100);
        return [
            RgbColor.FromHsl(hue - 15, desaturated, 91),
            RgbColor.FromHsl(hue, desaturated, 91),
            RgbColor.FromHsl(hue + 15, desaturated, 91)
        ];
    }

    /// <summary>按开关返回主背景颜色：彩色时返回渐变中间色，否则返回纯色。</summary>
    public static RgbColor FormBackground(bool colorful, double hue, double saturation) =>
        colorful ? RgbColor.FromHsl(hue, Math.Clamp(saturation * 0.8, 0, 100), 91) : PlainFormBackground;

}
