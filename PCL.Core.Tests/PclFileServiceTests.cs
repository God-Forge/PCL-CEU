using PCL.Core.IO;

namespace PCL.Core.Tests;

public class PclFileServiceTests {

    [Test]
    public async Task 读写JSON与哈希() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var path = Path.Combine(root, "data", "value.json");
            PclFileService.WriteJson(path, new { Name = "PCL", Value = 1 });
            var value = PclFileService.ReadJson<Dictionary<string, object>>(path);
            await Assert.That(value!["Name"].ToString()).IsEqualTo("PCL");
            await Assert.That(PclFileService.ComputeSha256(path).Length).IsEqualTo(64);
        } finally {
            PclFileService.TryDelete(root);
        }
    }

    [Test]
    public async Task 复制目录并过滤() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-file-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        var target = Path.Combine(root, "target");
        Directory.CreateDirectory(source);
        try {
            File.WriteAllText(Path.Combine(source, "a.txt"), "a");
            File.WriteAllText(Path.Combine(source, "b.log"), "b");
            PclFileService.CopyDirectory(source, target, path => path.EndsWith(".txt"));
            await Assert.That(File.Exists(Path.Combine(target, "a.txt"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(target, "b.log"))).IsFalse();
        } finally {
            PclFileService.TryDelete(root);
        }
    }

}
