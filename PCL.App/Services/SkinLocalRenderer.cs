using System.IO;
using MeloongCore;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.Minecraft;

namespace PCL.App.Services;

/// <summary>
/// 从本地皮肤 PNG 渲染头像与全身，不依赖外部渲染服务。
/// 用于断网或外部服务不可用时仍能显示皮肤。
/// </summary>
public static class SkinLocalRenderer {

    /// <summary>把皮肤 PNG 解码成像素数组；失败返回 <c>null</c>。</summary>
    public static (byte[] Pixels, int Width, int Height)? Decode(byte[]? png) {
        if (png is null || png.Length == 0) return null;
        try {
            using var stream = new MemoryStream(png);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var stride = converted.PixelWidth * 4;
            var pixels = new byte[stride * converted.PixelHeight];
            converted.CopyPixels(pixels, stride, 0);
            if (!SkinTextureLayout.IsValidSize(converted.PixelWidth, converted.PixelHeight)) return null;
            return (pixels, converted.PixelWidth, converted.PixelHeight);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "解码皮肤失败");
            return null;
        }
    }

    /// <summary>渲染头像：头部正面叠加帽子层，输出 <paramref name="size"/> 见方。</summary>
    public static BitmapSource? RenderAvatar(byte[]? png, int size = 64) {
        var decoded = Decode(png);
        if (decoded is null) return null;
        var (pixels, width, height) = decoded.Value;
        const int sourceSize = SkinTextureLayout.AvatarPixels;
        var head = Extract(pixels, width, height, SkinTextureLayout.HeadFace);
        var overlay = Extract(pixels, width, height, SkinTextureLayout.HeadOverlay);
        if (head is null) return null;
        var composed = new byte[sourceSize * sourceSize * 4];
        for (var index = 0; index < composed.Length; index++) composed[index] = head[index];
        if (overlay is not null) {
            for (var pixel = 0; pixel < sourceSize * sourceSize; pixel++) {
                var offset = pixel * 4;
                var alpha = overlay[offset + 3];
                if (alpha == 0) continue;
                // 半透明叠加：按 alpha 混合
                var ratio = alpha / 255.0;
                for (var channel = 0; channel < 3; channel++) {
                    composed[offset + channel] = (byte)(overlay[offset + channel] * ratio
                                                        + composed[offset + channel] * (1 - ratio));
                }
                composed[offset + 3] = 255;
            }
        }
        return Scale(composed, sourceSize, sourceSize, Math.Max(1, size));
    }

    /// <summary>渲染全身平面视图：按 <see cref="SkinTextureLayout.BodyPlacements"/> 拼接各部件。</summary>
    public static BitmapSource? RenderBody(byte[]? png, int scale = 6) {
        var decoded = Decode(png);
        if (decoded is null) return null;
        var (pixels, width, height) = decoded.Value;
        var legacy = SkinTextureLayout.IsLegacy(width, height);
        var canvasWidth = SkinTextureLayout.BodyPixelsWidth;
        var canvasHeight = SkinTextureLayout.BodyPixelsHeight;
        var canvas = new byte[canvasWidth * canvasHeight * 4];
        foreach (var placement in SkinTextureLayout.BodyPlacements(legacy)) {
            if (!SkinTextureLayout.IsWithinBounds(placement, width, height)) continue;
            var part = Extract(pixels, width, height, placement.Source);
            if (part is null) continue;
            Blit(canvas, canvasWidth, part, placement.Source.Width, placement.Source.Height,
                placement.DestX, placement.DestY);
        }
        return Scale(canvas, canvasWidth, canvasHeight, Math.Max(1, scale));
    }

    private static byte[]? Extract(byte[] pixels, int width, int height, PixelRegion region) {
        if (region.X + region.Width > width || region.Y + region.Height > height) return null;
        var result = new byte[region.Width * region.Height * 4];
        for (var y = 0; y < region.Height; y++) {
            var sourceOffset = ((region.Y + y) * width + region.X) * 4;
            var targetOffset = y * region.Width * 4;
            Array.Copy(pixels, sourceOffset, result, targetOffset, region.Width * 4);
        }
        return result;
    }

    private static void Blit(byte[] canvas, int canvasWidth, byte[] part, int partWidth, int partHeight,
        int destX, int destY) {
        for (var y = 0; y < partHeight; y++) {
            var canvasOffset = ((destY + y) * canvasWidth + destX) * 4;
            var partOffset = y * partWidth * 4;
            Array.Copy(part, partOffset, canvas, canvasOffset, partWidth * 4);
        }
    }

    private static BitmapSource Scale(byte[] pixels, int width, int height, int scale) {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        if (scale == 1) return bitmap;
        var scaled = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
        scaled.Freeze();
        // 放大后保持像素锐利
        var result = new WriteableBitmap(scaled);
        RenderOptions.SetBitmapScalingMode(result, BitmapScalingMode.NearestNeighbor);
        result.Freeze();
        return result;
    }

}
