namespace PCL.Core.Minecraft;

/// <summary>
/// LiteLoader 安装：下载官方安装器并运行，最后在 versions 目录里定位生成的版本。
/// </summary>
public static class LiteLoaderInstaller {

    private const string DownloadRoot = "https://dl.liteloader.com/redist/";

    /// <summary>构造安装器下载地址，例如 <c>https://dl.liteloader.com/redist/1.7.10/liteloader-installer-1.7.10-04.jar</c>。</summary>
    public static string BuildDownloadUrl(string minecraftVersion, string fileName) {
        if (string.IsNullOrWhiteSpace(minecraftVersion)) {
            throw new ArgumentException("游戏版本不能为空", nameof(minecraftVersion));
        }
        if (string.IsNullOrWhiteSpace(fileName)) {
            throw new ArgumentException("安装器文件名不能为空", nameof(fileName));
        }
        return $"{DownloadRoot}{minecraftVersion.Trim()}/{fileName.Trim()}";
    }

    /// <summary>
    /// 运行安装器并通过扫描版本目录判定结果（LiteLoader 的版本目录名不稳定）。
    /// </summary>
    public static async Task<JarInstallerResult> InstallAsync(
        string javaExePath,
        string installerPath,
        string mcFolder,
        string minecraftVersion,
        int timeoutSeconds = 300,
        CancellationToken cancellationToken = default) {
        if (!File.Exists(installerPath)) {
            return new JarInstallerResult { Success = false, Message = "安装器不存在：" + installerPath };
        }
        if (!File.Exists(javaExePath)) {
            return new JarInstallerResult { Success = false, Message = "找不到 Java：" + javaExePath };
        }
        var before = ListVersionFolders(mcFolder);
        var execution = await JarInstallerRunner
            .ExecuteAsync(javaExePath, installerPath, mcFolder, timeoutSeconds, cancellationToken)
            .ConfigureAwait(false);
        var created = FindLiteLoaderVersion(mcFolder, minecraftVersion, before);
        if (created is null) {
            return new JarInstallerResult {
                Success = false,
                Message = execution.Succeeded
                    ? "安装器已运行，但没有找到 LiteLoader 版本。"
                    : execution.Error ?? "安装器运行失败。",
                Output = execution.Output
            };
        }
        return new JarInstallerResult {
            Success = true,
            VersionFolder = Path.Combine(mcFolder, "versions", created),
            Message = "安装完成：" + created,
            Output = execution.Output
        };
    }

    /// <summary>
    /// 在 versions 目录里查找属于指定原版版本的 LiteLoader 实例。
    /// 优先返回本次新增的目录。
    /// </summary>
    public static string? FindLiteLoaderVersion(string mcFolder, string minecraftVersion, IReadOnlySet<string>? exclude = null) {
        var versionsRoot = Path.Combine(mcFolder, "versions");
        if (!Directory.Exists(versionsRoot)) return null;
        foreach (var folder in Directory.EnumerateDirectories(versionsRoot)) {
            var name = Path.GetFileName(folder);
            if (exclude is not null && exclude.Contains(name)) continue;
            var jsonPath = Path.Combine(folder, name + ".json");
            if (!File.Exists(jsonPath)) continue;
            string text;
            try {
                text = File.ReadAllText(jsonPath);
            } catch {
                continue;
            }
            if (!text.Contains("LiteLoader", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(minecraftVersion)
                && !text.Contains(minecraftVersion, StringComparison.OrdinalIgnoreCase)) continue;
            return name;
        }
        return null;
    }

    private static HashSet<string> ListVersionFolders(string mcFolder) {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versionsRoot = Path.Combine(mcFolder, "versions");
        if (!Directory.Exists(versionsRoot)) return set;
        foreach (var folder in Directory.EnumerateDirectories(versionsRoot)) {
            set.Add(Path.GetFileName(folder));
        }
        return set;
    }

}
