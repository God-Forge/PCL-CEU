using PCL.Core.Minecraft;
using PCL.Core.Paths;
using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class MinecraftFolderServiceTests {

    [Test]
    public async Task 扫描默认与自定义文件夹() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-folders-" + Guid.NewGuid().ToString("N"));
        try {
            var paths = new PclPathService(root, Path.Combine(root, "cache"));
            paths.EnsureDirectories();
            var defaultVersion = Path.Combine(paths.Minecraft, "versions", "demo");
            Directory.CreateDirectory(defaultVersion);
            File.WriteAllText(Path.Combine(defaultVersion, "demo.json"), """{"id":"demo","type":"release"}""");

            var customRoot = Path.Combine(root, "custom", ".minecraft");
            var customVersion = Path.Combine(customRoot, "versions", "other");
            Directory.CreateDirectory(customVersion);
            File.WriteAllText(Path.Combine(customVersion, "other.json"), """{"id":"other","type":"release"}""");

            var settings = new SettingsService(root, "TEST");
            settings.Register("LaunchFolders", "");
            var folders = new MinecraftFolderService(paths, settings);
            folders.AddFolder("自定义", customRoot);

            var all = folders.ScanAll();
            await Assert.That(all.Count).IsEqualTo(2);
            await Assert.That(folders.FindInstance("other")!.Folder.Name).IsEqualTo("自定义");
            await Assert.That(folders.GetFolders().Count).IsEqualTo(2);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
