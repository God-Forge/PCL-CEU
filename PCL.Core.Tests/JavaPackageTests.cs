using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Java;

namespace PCL.Core.Tests;

public class JavaPackageTests {

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-javapkg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateJavaHome(string root, string version, bool jdk, string arch = "x86_64") {
        var home = Path.Combine(root, jdk ? "jdk" : "jre");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "bin", "java.exe"), "fake java");
        if (jdk) File.WriteAllText(Path.Combine(home, "bin", "javac.exe"), "fake javac");
        File.WriteAllLines(Path.Combine(home, "release"), [
            $"JAVA_VERSION=\"{version}\"",
            $"OS_ARCH=\"{arch}\"",
            "OS_NAME=\"Windows\"",
            "IMPLEMENTOR=\"Test Vendor\""
        ]);
        return home;
    }

    [Test]
    public async Task 打包与解包JDK() {
        var root = CreateRoot();
        try {
            var home = CreateJavaHome(root, "21.0.1", true);
            var info = JavaPackageService.Detect(home);
            await Assert.That(info.Type).IsEqualTo(JavaPackageType.Jdk);
            await Assert.That(JavaPackageService.GetDefaultFileName(info)).IsEqualTo("21.0.1.jdk.javapkg");

            var package = Path.Combine(root, "21.0.1.jdk.javapkg");
            JavaPackageService.CreatePackage(home, package);
            var read = JavaPackageService.ReadInfo(package);
            await Assert.That(read.JavaVersion).IsEqualTo("21.0.1");
            await Assert.That(read.Type).IsEqualTo(JavaPackageType.Jdk);
            await Assert.That(read.Arch).IsEqualTo("x64");

            var target = Path.Combine(root, "extracted");
            JavaPackageService.Extract(package, target);
            await Assert.That(File.Exists(Path.Combine(target, "bin", "java.exe"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(target, "bin", "javac.exe"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(target, "javapkg.json"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 识别JRE命名() {
        var root = CreateRoot();
        try {
            var home = CreateJavaHome(root, "1.8.0_402", false, "amd64");
            var info = JavaPackageService.Detect(home);
            await Assert.That(info.Type).IsEqualTo(JavaPackageType.Jre);
            await Assert.That(info.Arch).IsEqualTo("x64");
            await Assert.That(JavaPackageService.GetDefaultFileName(info)).IsEqualTo("1.8.0_402.jre.javapkg");
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 拒绝路径穿越() {
        var root = CreateRoot();
        var package = Path.Combine(root, "evil.javapkg");
        try {
            using (var zip = ZipFile.Open(package, ZipArchiveMode.Create)) {
                var manifest = new JObject {
                    ["format"] = "pcl-javapkg",
                    ["version"] = 1,
                    ["javaVersion"] = "21",
                    ["type"] = "jdk"
                }.ToString(Newtonsoft.Json.Formatting.None);
                var manifestEntry = zip.CreateEntry("javapkg.json");
                using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false))) writer.Write(manifest);
                var javaEntry = zip.CreateEntry("bin/java.exe");
                using (var writer = new StreamWriter(javaEntry.Open(), new UTF8Encoding(false))) writer.Write("java");
                var evil = zip.CreateEntry("../evil.txt");
                using (var writer = new StreamWriter(evil.Open(), new UTF8Encoding(false))) writer.Write("bad");
            }

            await Assert.That(() => JavaPackageService.Extract(package, Path.Combine(root, "out"))).Throws<InvalidDataException>();
            await Assert.That(File.Exists(Path.Combine(root, "evil.txt"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
