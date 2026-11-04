using PCL.Core.Java;

namespace PCL.Core.Tests;

public class JavaScannerTests {

    [Test]
    public async Task 扫描Java主目录() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-java-" + Guid.NewGuid().ToString("N"));
        try {
            var home = Path.Combine(root, "jdk-17");
            Directory.CreateDirectory(Path.Combine(home, "bin"));
            File.WriteAllText(Path.Combine(home, "bin", "java.exe"), "");
            File.WriteAllText(Path.Combine(home, "release"), "JAVA_VERSION=\"17.0.1\"\nOS_ARCH=\"x86_64\"\n");
            var result = JavaScanner.Scan([root]);
            await Assert.That(result.Count).IsEqualTo(1);
            await Assert.That(result[0].Major).IsEqualTo(17);
            await Assert.That(result[0].JavaHome).IsEqualTo(home);
        } finally {
            Directory.Delete(root, true);
        }
    }

}
