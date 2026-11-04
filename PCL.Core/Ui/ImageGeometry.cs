namespace PCL.Core.Ui;

/// <summary>裁剪区域。</summary>
public readonly record struct ClipRect(int X, int Y, int Width, int Height);

/// <summary>
/// 图片相关的几何计算。由 VB 版 MyBitmap 的裁剪与格式判断迁移而来，纯计算便于测试。
/// </summary>
public static class ImageGeometry {

    /// <summary>
    /// 把裁剪请求钳制到图片范围内。完全越界时返回宽度或高度为 0 的区域。
    /// </summary>
    public static ClipRect ClampClip(int x, int y, int width, int height, int imageWidth, int imageHeight) {
        if (imageWidth <= 0 || imageHeight <= 0 || width <= 0 || height <= 0) return new ClipRect(0, 0, 0, 0);
        var left = Math.Clamp(x, 0, imageWidth);
        var top = Math.Clamp(y, 0, imageHeight);
        var right = Math.Clamp(x + width, 0, imageWidth);
        var bottom = Math.Clamp(y + height, 0, imageHeight);
        var w = Math.Max(0, right - left);
        var h = Math.Max(0, bottom - top);
        return new ClipRect(left, top, w, h);
    }

    /// <summary>
    /// 等比缩放到指定范围内。原图已在范围内时原样返回。
    /// </summary>
    public static (int Width, int Height) FitWithin(int width, int height, int maxWidth, int maxHeight) {
        if (width <= 0 || height <= 0) return (0, 0);
        if (maxWidth <= 0 || maxHeight <= 0) return (width, height);
        var scale = Math.Min((double)maxWidth / width, (double)maxHeight / height);
        if (scale >= 1) return (width, height);
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale))
        );
    }

    /// <summary>
    /// 是否为 WebP 文件：以 RIFF 开头且在第 8 字节处为 WEBP。
    /// </summary>
    public static bool IsWebP(ReadOnlySpan<byte> data) {
        if (data.Length < 12) return false;
        return data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
               && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P';
    }

    /// <summary>
    /// 按扩展名推断保存用的编码器格式，未识别时按 PNG 处理。
    /// </summary>
    public static string NormalizeSaveExtension(string? path) {
        var extension = Path.GetExtension(path ?? "").ToLowerInvariant();
        return extension switch {
            ".png" => ".png",
            ".jpg" or ".jpeg" => ".jpg",
            ".bmp" => ".bmp",
            ".gif" => ".gif",
            ".tif" or ".tiff" => ".tiff",
            _ => ".png"
        };
    }

}
