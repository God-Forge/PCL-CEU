namespace PCL.Core.Modpack;

/// <summary>整合包导入前的预览信息。</summary>
public sealed class ModpackPreview {

    public bool Valid { get; init; }
    public string? Error { get; init; }

    /// <summary>整合包名称（清单里的 name）。</summary>
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";

    /// <summary>目标组成（原版版本 + 加载器）。</summary>
    public Minecraft.InstallTarget Target { get; init; } = new();

    /// <summary>需要下载的文件数与其中必选的数目。</summary>
    public int FileCount { get; init; }
    public int RequiredFileCount { get; init; }

    public string TargetFolder { get; init; } = "";

    /// <summary>目标实例是否已存在（存在则导入会写入该目录）。</summary>
    public bool TargetExists { get; init; }

}

/// <summary>
/// 整合包导入前的预检：解析清单、推导目标、检查目录冲突。
/// 只读操作，不修改任何文件。
/// </summary>
public static class ModpackPreviewService {

    public static ModpackPreview Preview(string? zipPath, string? versionsFolder, string? instanceName = null) {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) {
            return new ModpackPreview { Valid = false, Error = "整合包文件不存在。" };
        }
        if (string.IsNullOrWhiteSpace(versionsFolder)) {
            return new ModpackPreview { Valid = false, Error = "版本目录不能为空。" };
        }
        ModpackManifest? manifest;
        try {
            manifest = ModpackManifestParser.ReadFromZip(zipPath);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取整合包清单失败：" + zipPath);
            return new ModpackPreview { Valid = false, Error = "无法读取整合包清单：" + ex.Message };
        }
        if (manifest is null) {
            return new ModpackPreview { Valid = false, Error = "不是可识别的整合包（缺少 modrinth.index.json 或 manifest.json）。" };
        }
        string target;
        try {
            target = ModpackInstallPlanner.ResolveTargetFolder(versionsFolder, zipPath, instanceName);
        } catch (ArgumentException ex) {
            return new ModpackPreview { Valid = false, Error = ex.Message };
        }
        return new ModpackPreview {
            Valid = true,
            Name = string.IsNullOrWhiteSpace(manifest.Name)
                ? Path.GetFileNameWithoutExtension(zipPath)
                : manifest.Name,
            Version = manifest.Version,
            Target = new Minecraft.InstallTarget {
                MinecraftVersion = manifest.MinecraftVersion ?? "",
                ForgeVersion = manifest.Loader == "Forge" ? manifest.LoaderVersion : null,
                NeoForgeVersion = manifest.Loader == "NeoForge" ? manifest.LoaderVersion : null,
                FabricVersion = manifest.Loader == "Fabric" ? manifest.LoaderVersion : null
            },
            FileCount = manifest.Files.Count,
            RequiredFileCount = manifest.Files.Count(file => file.Required),
            TargetFolder = target,
            TargetExists = Directory.Exists(target)
        };
    }

    /// <summary>把预览格式化成给用户确认的文本。</summary>
    public static string Format(ModpackPreview? preview) {
        if (preview is null) return "未获取到整合包信息。";
        if (!preview.Valid) return preview.Error ?? "整合包无效。";
        var lines = new List<string> {
            "名称：" + (string.IsNullOrWhiteSpace(preview.Version) ? preview.Name : $"{preview.Name} {preview.Version}"),
            "目标：" + Minecraft.InstallTargetComparer.Describe(preview.Target),
            $"文件：{preview.FileCount} 个（必选 {preview.RequiredFileCount} 个）",
            "位置：" + preview.TargetFolder
        };
        lines.Add(preview.TargetExists
            ? "注意：目标实例已存在，导入会写入该目录。"
            : "目标实例将新建。");
        return string.Join(Environment.NewLine, lines);
    }

}
