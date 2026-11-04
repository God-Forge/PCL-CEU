using PCL.Core.Paths;
using PCL.Core.Settings;

namespace PCL.Core.Minecraft;

public sealed class MinecraftFolder {

    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsDefault { get; init; }

    public string Versions => System.IO.Path.Combine(Path, "versions") + System.IO.Path.DirectorySeparatorChar;
    public string Libraries => System.IO.Path.Combine(Path, "libraries") + System.IO.Path.DirectorySeparatorChar;
    public string Assets => System.IO.Path.Combine(Path, "assets") + System.IO.Path.DirectorySeparatorChar;

}

public sealed class MinecraftInstanceLocation {

    public MinecraftFolder Folder { get; init; } = new();
    public MinecraftVersionInfo Version { get; init; } = new();

}

/// <summary>
/// 管理默认 .minecraft 与用户添加的额外 Minecraft 文件夹。
/// </summary>
public sealed class MinecraftFolderService {

    private readonly PclPathService _paths;
    private readonly SettingsService _settings;

    public MinecraftFolderService(PclPathService paths, SettingsService settings) {
        _paths = paths;
        _settings = settings;
    }

    public MinecraftFolder Default => new() {
        Name = "默认",
        Path = _paths.Minecraft,
        IsDefault = true
    };

    public IReadOnlyList<MinecraftFolder> GetFolders() {
        var folders = new List<MinecraftFolder> { Default };
        var parsed = McFolderListParser.Parse(_settings.Get<string>("LaunchFolders") ?? "");
        foreach (var entry in parsed.Entries) {
            var path = entry.Path.Replace("$", _paths.Base);
            if (string.IsNullOrWhiteSpace(path)) continue;
            path = PathUtils.AddSlashSuffix(Path.GetFullPath(path));
            if (folders.Any(folder => string.Equals(folder.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            folders.Add(new MinecraftFolder { Name = entry.Name, Path = path });
        }
        return folders;
    }

    public void AddFolder(string name, string path) {
        path = PathUtils.AddSlashSuffix(Path.GetFullPath(path));
        var folders = GetFolders().Where(folder => !folder.IsDefault).ToList();
        folders.RemoveAll(folder => string.Equals(folder.Path, path, StringComparison.OrdinalIgnoreCase));
        folders.Add(new MinecraftFolder { Name = name, Path = path });
        Save(folders);
    }

    public void RemoveFolder(string path) {
        var folders = GetFolders().Where(folder => !folder.IsDefault
            && !string.Equals(folder.Path, PathUtils.AddSlashSuffix(Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase)).ToList();
        Save(folders);
    }

    public IReadOnlyList<MinecraftInstanceLocation> ScanAll() {
        var result = new List<MinecraftInstanceLocation>();
        foreach (var folder in GetFolders()) {
            foreach (var version in MinecraftVersionScanner.Scan(folder.Path)) {
                result.Add(new MinecraftInstanceLocation { Folder = folder, Version = version });
            }
        }
        return result;
    }

    public MinecraftInstanceLocation? FindInstance(string name) {
        return ScanAll().FirstOrDefault(location =>
            string.Equals(location.Version.Folder, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(location.Version.Id, name, StringComparison.OrdinalIgnoreCase));
    }

    private void Save(IEnumerable<MinecraftFolder> folders) {
        var entries = folders.Select(folder => new McFolderEntry { Name = folder.Name, Path = folder.Path }).ToList();
        _settings.Set("LaunchFolders", McFolderListParser.Format(entries));
    }

}
