using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class IniFileTests {

    [Test]
    public async Task 读写删除INI键() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-ini-" + Guid.NewGuid().ToString("N"));
        try {
            var ini = new IniFile(root);
            ini.Write("Setup", "A", "1");
            ini.Write("Setup", "B", "two:three");
            await Assert.That(ini.Read("Setup", "A")).IsEqualTo("1");
            await Assert.That(ini.Read("Setup", "B")).IsEqualTo("two:three");
            await Assert.That(ini.HasKey("Setup", "A")).IsTrue();
            ini.Write("Setup", "A", null);
            await Assert.That(ini.HasKey("Setup", "A")).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
