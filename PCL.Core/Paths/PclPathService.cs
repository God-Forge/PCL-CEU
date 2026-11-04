namespace PCL.Core.Paths;

/// <summary>
/// PCL 的目录布局：程序目录、PCL 数据目录、缓存、实例、资源与库目录。
/// </summary>
public sealed class PclPathService {

    public string Base { get; }
    public string Minecraft { get; }
    public string Pcl { get; }
    public string Temp { get; }
    public string TempCache { get; }
    public string Versions { get; }
    public string Libraries { get; }
    public string Assets { get; }
    public string Java { get; }
    public string Logs { get; }

    public PclPathService(string baseDirectory, string? cacheDirectory = null) {
        Base = PathUtils.AddSlashSuffix(Path.GetFullPath(baseDirectory));
        Pcl = Path.Combine(Base, "PCL") + Path.DirectorySeparatorChar;
        Temp = PathUtils.AddSlashSuffix(
            string.IsNullOrWhiteSpace(cacheDirectory)
                ? Path.Combine(Path.GetTempPath(), "PCL")
                : cacheDirectory!);
        TempCache = Path.Combine(Temp, "Cache") + Path.DirectorySeparatorChar;
        Minecraft = Path.Combine(Base, ".minecraft") + Path.DirectorySeparatorChar;
        Versions = Path.Combine(Minecraft, "versions") + Path.DirectorySeparatorChar;
        Libraries = Path.Combine(Minecraft, "libraries") + Path.DirectorySeparatorChar;
        Assets = Path.Combine(Minecraft, "assets") + Path.DirectorySeparatorChar;
        Java = Path.Combine(Pcl, "Java") + Path.DirectorySeparatorChar;
        Logs = Path.Combine(Pcl, "Logs") + Path.DirectorySeparatorChar;
    }

    public string GetInstancePath(string name) => Path.Combine(Versions, name) + Path.DirectorySeparatorChar;

    public string GetInstanceJsonPath(string name) => Path.Combine(Versions, name, name + ".json");

    public string GetInstanceSetupPath(string name) => Path.Combine(Versions, name, "PCL", "Setup.ini");

    public string GetInstanceModsPath(string name) => Path.Combine(Versions, name, "mods") + Path.DirectorySeparatorChar;

    public string GetInstanceSavesPath(string name) => Path.Combine(Versions, name, "saves") + Path.DirectorySeparatorChar;

    public void EnsureDirectories() {
        foreach (var path in new[] { Minecraft, Pcl, Temp, TempCache, Versions, Libraries, Assets, Java, Logs }) {
            Directory.CreateDirectory(path);
        }
    }

}

