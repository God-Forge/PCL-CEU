using System.IO.Compression;

namespace PCL.Core.Import;

/// <summary>拖入内容的类型。</summary>
public enum DropImportKind {
    Unknown,
    /// <summary>整合包（zip，含 modrinth.index.json 或 manifest.json）。</summary>
    Modpack,
    /// <summary>单个 Mod 文件（jar / litemod）。</summary>
    ModFile,
    /// <summary>资源包（zip，含 pack.mcmeta）。</summary>
    ResourcePack,
    /// <summary>存档（含 level.dat 的目录）。</summary>
    World,
    /// <summary>Java 包（.javapkg）。</summary>
    JavaPackage,
    /// <summary>版本包（.payload）。</summary>
    Payload,
    /// <summary>图片。</summary>
    Image,
    /// <summary>Minecraft 实例目录。</summary>
    InstanceFolder
}

/// <summary>
/// 判断拖入的文件或目录属于什么类型，供拖拽导入使用。
/// 对应 VB 版 FormMain 里根据扩展名分派的逻辑。
/// </summary>
public static class DropImportClassifier {

    public static DropImportKind Classify(string? path) {
        if (string.IsNullOrWhiteSpace(path)) return DropImportKind.Unknown;
        try {
            if (Directory.Exists(path)) {
                if (File.Exists(Path.Combine(path, "level.dat"))) return DropImportKind.World;
                if (Directory.Exists(Path.Combine(path, "versions"))) return DropImportKind.InstanceFolder;
                return DropImportKind.Unknown;
            }
            if (!File.Exists(path)) return DropImportKind.Unknown;
            return Path.GetExtension(path).ToLowerInvariant() switch {
                ".jar" or ".litemod" => DropImportKind.ModFile,
                ".javapkg" => DropImportKind.JavaPackage,
                ".payload" => DropImportKind.Payload,
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".webp" => DropImportKind.Image,
                ".zip" => ClassifyZip(path),
                _ => DropImportKind.Unknown
            };
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "识别拖入内容失败：" + path);
            return DropImportKind.Unknown;
        }
    }

    /// <summary>给用户看的类型描述。</summary>
    public static string Describe(DropImportKind kind) => kind switch {
        DropImportKind.Modpack => "整合包",
        DropImportKind.ModFile => "Mod 文件",
        DropImportKind.ResourcePack => "资源包",
        DropImportKind.World => "存档",
        DropImportKind.JavaPackage => "Java 包",
        DropImportKind.Payload => "版本包",
        DropImportKind.Image => "图片",
        DropImportKind.InstanceFolder => "Minecraft 实例",
        _ => "未知内容"
    };

    private static DropImportKind ClassifyZip(string path) {
        try {
            using var archive = ZipFile.OpenRead(path);
            if (archive.GetEntry("modrinth.index.json") is not null
                || archive.GetEntry("manifest.json") is not null) {
                return DropImportKind.Modpack;
            }
            if (archive.GetEntry("pack.mcmeta") is not null) return DropImportKind.ResourcePack;
            return DropImportKind.Unknown;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取压缩包失败：" + path);
            return DropImportKind.Unknown;
        }
    }

}
