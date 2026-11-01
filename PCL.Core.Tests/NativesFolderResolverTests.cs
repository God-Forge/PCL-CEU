using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class NativesFolderResolverTests {

    private static string Short(string path) => path;

    [Test]
    public async Task GBK编码使用版本目录() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-folder-" + Guid.NewGuid().ToString("N"));
        try {
            var result = NativesFolderResolver.Resolve(
                Path.Combine(root, "versions", "1.20.1") + "\\", "1.20.1", true,
                root + "\\", "C:\\", Short);
            await Assert.That(result).IsEqualTo(Path.Combine(root, "versions", "1.20.1") + "\\1.20.1-natives\\");
            await Assert.That(Directory.Exists(result)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 非ASCII路径回退到AppData() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-folder-" + Guid.NewGuid().ToString("N"));
        try {
            var result = NativesFolderResolver.Resolve(
                root + "\\中文\\", "1.20.1", false, root + "\\app\\", "C:\\", Short);
            await Assert.That(result).IsEqualTo(root + "\\app\\.minecraft\\bin\\natives\\");
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task AppData也非ASCII时回退系统盘() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-natives-folder-" + Guid.NewGuid().ToString("N"));
        try {
            var result = NativesFolderResolver.Resolve(
                root + "\\中文\\", "1.20.1", false, root + "\\中文\\app\\", root + "\\", Short);
            await Assert.That(result).IsEqualTo(root + "\\ProgramData\\PCL\\natives\\");
        } finally {
            Directory.Delete(root, true);
        }
    }

}
