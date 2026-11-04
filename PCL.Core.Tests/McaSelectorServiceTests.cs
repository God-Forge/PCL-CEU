using PCL.Core.Tools;

namespace PCL.Core.Tests;

public class McaSelectorServiceTests {

    [Test]
    public async Task 解压内置MCA_Selector() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-mca-" + Guid.NewGuid().ToString("N"));
        try {
            var folder = McaSelectorService.EnsureExtracted(root);
            await Assert.That(File.Exists(Path.Combine(folder, "MCA Selector.exe"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(folder, "app", "mcaselector-2.9-all.jar"))).IsTrue();
            await Assert.That(File.ReadAllText(Path.Combine(folder, ".pcl-version")).Trim()).IsEqualTo("2.9");
            await Assert.That(McaSelectorService.EnsureExtracted(root)).IsEqualTo(folder);
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
