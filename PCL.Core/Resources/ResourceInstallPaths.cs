namespace PCL.Core.Resources;

/// <summary>
/// 资源类型到实例内目录的映射。迁移自 VB 版 ModDownloadLib 的目标路径逻辑。
/// </summary>
public static class ResourceInstallPaths {

    /// <summary>资源类型对应的子目录名。</summary>
    public static string FolderName(ResourceKind kind) => kind switch {
        ResourceKind.ResourcePack => "resourcepacks",
        ResourceKind.ShaderPack => "shaderpacks",
        ResourceKind.DataPack => "datapacks",
        _ => "mods"
    };

    /// <summary>
    /// 计算安装目录。整合包比较特殊，需要由专用流程处理，这里直接返回实例目录。
    /// </summary>
    public static string Resolve(string instanceFolder, ResourceKind kind) {
        if (string.IsNullOrWhiteSpace(instanceFolder)) {
            throw new ArgumentException("实例目录不能为空", nameof(instanceFolder));
        }
        return kind == ResourceKind.Modpack
            ? instanceFolder
            : Path.Combine(instanceFolder, FolderName(kind));
    }

    /// <summary>该类型是否可以按「下载单个文件」的方式安装。</summary>
    public static bool CanInstallAsFile(ResourceKind kind) => kind != ResourceKind.Modpack;

}
