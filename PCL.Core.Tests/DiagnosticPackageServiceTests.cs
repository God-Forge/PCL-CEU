using System.IO.Compression;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class DiagnosticPackageServiceTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-diag-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 生成脱敏诊断包() {
        var root = CreateRoot();
        try {
            var instance = Path.Combine(root, "mc", "versions", "test");
            Directory.CreateDirectory(Path.Combine(instance, "logs"));
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            File.WriteAllText(Path.Combine(instance, "logs", "latest.log"),
                $"path={profile}\\mc\naccessToken: secret-token\nusername: Steve\n");

            var output = Path.Combine(root, "diag.zip");
            var result = DiagnosticPackageService.Create(instance, Path.Combine(root, "mc"), output);

            await Assert.That(File.Exists(result.Path)).IsTrue();
            await Assert.That(result.Size).IsGreaterThan(0);
            using var zip = ZipFile.OpenRead(output);
            await Assert.That(zip.GetEntry("manifest.json")).IsNotNull();
            await Assert.That(zip.GetEntry("system-info.txt")).IsNotNull();
            var logEntry = zip.Entries.FirstOrDefault(entry => entry.FullName.Replace('\\', '/') == "logs/latest.log");
            await Assert.That(logEntry).IsNotNull();
            using var reader = new StreamReader(logEntry!.Open());
            var text = reader.ReadToEnd();
            await Assert.That(text).Contains("%USERPROFILE%");
            await Assert.That(text.Contains("secret-token")).IsFalse();
            await Assert.That(text).Contains("username: ***");
        } finally {
            Directory.Delete(root, true);
        }
    }

}
