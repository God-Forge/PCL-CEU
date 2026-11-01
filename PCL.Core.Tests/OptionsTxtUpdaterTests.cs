using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class OptionsTxtUpdaterTests {

    private static string CreateFile(string content) {
        var path = Path.Combine(Path.GetTempPath(), "pcl-options-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    [Test]
    public async Task 初次启动设置中文并全屏() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-options-" + Guid.NewGuid().ToString("N") + ".txt");
        try {
            var result = OptionsTxtUpdater.Update(path, false, true, 20, true, 0);
            await Assert.That(result.RequiredLanguage).IsEqualTo("zh_cn");
            await Assert.That(result.LanguageChanged).IsTrue();
            var content = File.ReadAllText(path);
            await Assert.That(content).Contains("lang:zh_cn");
            await Assert.That(content).Contains("fullscreen:true");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task 旧版本语言后两位大写() {
        var path = CreateFile("lang:en_us\r\n");
        try {
            var result = OptionsTxtUpdater.Update(path, true, true, 10, false, 1);
            await Assert.That(result.RequiredLanguage).IsEqualTo("en_US");
            await Assert.That(File.ReadAllText(path)).Contains("lang:en_US");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task 已有存档时保留当前语言() {
        var path = CreateFile("lang:en_us\r\n");
        try {
            var result = OptionsTxtUpdater.Update(path, true, true, 20, true, 2);
            await Assert.That(result.LanguageChanged).IsFalse();
            await Assert.That(File.ReadAllText(path)).Contains("fullscreen:false");
        } finally {
            File.Delete(path);
        }
    }

}
