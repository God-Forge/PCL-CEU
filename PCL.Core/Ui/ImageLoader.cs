using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCL.Core.Ui;

/// <summary>
/// 图片加载与缓存。由 VB 版 MyBitmap 迁移而来，直接使用 WPF 图像类型，不依赖 System.Drawing。
/// WebP 需要额外解码器，这里会返回 <c>null</c> 而不是抛出异常。
/// </summary>
public sealed class ImageLoader {

    private readonly ConcurrentDictionary<string, BitmapSource> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已缓存的条目数。</summary>
    public int Count => _cache.Count;

    /// <summary>
    /// 从文件加载图片并缓存。文件不存在、格式不支持或 WebP 时返回 <c>null</c>。
    /// </summary>
    public BitmapSource? Load(string? path) {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var fullPath = Path.GetFullPath(path);
        if (_cache.TryGetValue(fullPath, out var cached)) return cached;
        try {
            if (!File.Exists(fullPath)) return null;
            byte[] header = new byte[12];
            using (var stream = File.OpenRead(fullPath)) {
                var read = stream.Read(header, 0, header.Length);
                if (read < header.Length) return null;
            }
            if (ImageGeometry.IsWebP(header)) {
                Logger.Warn("暂不支持 WebP 图片：" + fullPath);
                return null;
            }
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(fullPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            _cache[fullPath] = image;
            return image;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "加载图片失败：" + fullPath);
            return null;
        }
    }

    /// <summary>裁剪图片，越界部分会被钳制。</summary>
    public static BitmapSource? Clip(BitmapSource? source, int x, int y, int width, int height) {
        if (source is null) return null;
        var rect = ImageGeometry.ClampClip(x, y, width, height, source.PixelWidth, source.PixelHeight);
        if (rect.Width == 0 || rect.Height == 0) return null;
        var cropped = new CroppedBitmap(source, new System.Windows.Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));
        cropped.Freeze();
        return cropped;
    }

    /// <summary>旋转 90 度：1 为顺时针，-1 为逆时针。</summary>
    public static BitmapSource? Rotate(BitmapSource? source, int quarterTurns) {
        if (source is null) return null;
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0) return source;
        var rotated = new TransformedBitmap(source, new RotateTransform(turns * 90));
        rotated.Freeze();
        return rotated;
    }

    /// <summary>保存图片，按扩展名选择编码器。</summary>
    public static bool Save(BitmapSource? source, string path) {
        if (source is null || string.IsNullOrWhiteSpace(path)) return false;
        try {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            BitmapEncoder encoder = ImageGeometry.NormalizeSaveExtension(path) switch {
                ".jpg" => new JpegBitmapEncoder(),
                ".bmp" => new BmpBitmapEncoder(),
                ".gif" => new GifBitmapEncoder(),
                ".tiff" => new TiffBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = File.Create(path);
            encoder.Save(stream);
            return true;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "保存图片失败：" + path);
            return false;
        }
    }

    public void Clear() => _cache.Clear();

}
