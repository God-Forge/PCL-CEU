using PCL.Core.Java;

namespace PCL.Core.Tests;

public class JavaInspectTests {

    [Test]
    public async Task 空路径返回空() {
        await Assert.That(JavaScanner.Inspect(null)).IsNull();
        await Assert.That(JavaScanner.Inspect("")).IsNull();
        await Assert.That(JavaScanner.Inspect("   ")).IsNull();
    }

    [Test]
    public async Task 不存在的路径返回空() {
        await Assert.That(JavaScanner.Inspect(Path.Combine(Path.GetTempPath(), "no-such-java-" + Guid.NewGuid().ToString("N")))).IsNull();
    }

    [Test]
    public async Task 目录里没有java时返回空() {
        var folder = Path.Combine(Path.GetTempPath(), "pcl-java-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            await Assert.That(JavaScanner.Inspect(folder)).IsNull();
        } finally {
            Directory.Delete(folder, true);
        }
    }

}
