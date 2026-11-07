using System.IO.Compression;
using PCL.Core.Transfer;

namespace PCL.Core.Tests;

public class MachineTransferServiceTests {

    private static string CreateInstance(out string minecraft) {
        minecraft = Path.Combine(Path.GetTempPath(), "pcl-transfer-" + Guid.NewGuid().ToString("N"));
        var instance = Path.Combine(minecraft, "versions", "test");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "test.json"),
            """{"id":"test","mainClass":"net.minecraft.client.main.Main","libraries":[]}""");
        File.WriteAllText(Path.Combine(instance, "test.jar"), "jar");
        Directory.CreateDirectory(Path.Combine(instance, "mods"));
        File.WriteAllText(Path.Combine(instance, "mods", "a.jar"), "a");
        Directory.CreateDirectory(Path.Combine(instance, "saves", "world"));
        File.WriteAllText(Path.Combine(instance, "saves", "world", "level.dat"), "nbt");
        Directory.CreateDirectory(Path.Combine(instance, "logs"));
        File.WriteAllText(Path.Combine(instance, "logs", "latest.log"), "log");
        Directory.CreateDirectory(Path.Combine(instance, "PCL"));
        File.WriteAllText(Path.Combine(instance, "PCL", "LoginCache.json"), """{"token":"secret"}""");
        return instance;
    }

    private static string CreateFakeJavaHome() {
        var home = Path.Combine(Path.GetTempPath(), "pcl-java-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        File.WriteAllText(Path.Combine(home, "bin", "java.exe"), "exe");
        File.WriteAllText(Path.Combine(home, "release"), "JAVA_VERSION=\"21.0.1\"\nOS_ARCH=\"x86_64\"");
        return home;
    }

    [Test]
    public async Task 导出并导入实例() {
        var instance = CreateInstance(out var minecraft);
        try {
            var package = Path.Combine(minecraft, "test.pcltransfer");
            var result = MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = package
            });

            await Assert.That(File.Exists(package)).IsTrue();
            var preview = MachineTransferService.Inspect(package);
            await Assert.That(preview.Manifest.InstanceName).IsEqualTo("test");
            await Assert.That(preview.Manifest.IncludesJava).IsFalse();
            await Assert.That(result.FileCount).IsGreaterThan(0);

            var targetRoot = Path.Combine(minecraft, "target");
            var imported = MachineTransferService.Import(package, targetRoot);

            var target = Path.Combine(targetRoot, "versions", "test");
            await Assert.That(imported.InstanceName).IsEqualTo("test");
            await Assert.That(File.Exists(Path.Combine(target, "test.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(target, "mods", "a.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(target, "saves", "world", "level.dat"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(target, "logs"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(target, "PCL", "LoginCache.json"))).IsFalse();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 不允许覆盖已存在的实例() {
        var instance = CreateInstance(out var minecraft);
        try {
            var package = Path.Combine(minecraft, "test.pcltransfer");
            MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = package
            });
            var targetRoot = Path.Combine(minecraft, "target");
            Directory.CreateDirectory(Path.Combine(targetRoot, "versions", "test"));

            await Assert.That(() => MachineTransferService.Import(package, targetRoot))
                .Throws<IOException>();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 可以指定新实例名() {
        var instance = CreateInstance(out var minecraft);
        try {
            var package = Path.Combine(minecraft, "test.pcltransfer");
            MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = package
            });
            var targetRoot = Path.Combine(minecraft, "target");
            var imported = MachineTransferService.Import(package, targetRoot, "副本");

            await Assert.That(imported.InstanceName).IsEqualTo("副本");
            await Assert.That(File.Exists(Path.Combine(targetRoot, "versions", "副本", "副本.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(targetRoot, "versions", "副本", "test.json"))).IsFalse();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 导出时可以排除存档() {
        var instance = CreateInstance(out var minecraft);
        try {
            var package = Path.Combine(minecraft, "test.pcltransfer");
            MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = package,
                IncludeSaves = false
            });
            var targetRoot = Path.Combine(minecraft, "target");
            MachineTransferService.Import(package, targetRoot);

            var target = Path.Combine(targetRoot, "versions", "test");
            await Assert.That(Directory.Exists(Path.Combine(target, "saves"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(target, "mods", "a.jar"))).IsTrue();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 带Java打包并在导入时释放() {
        var instance = CreateInstance(out var minecraft);
        var javaHome = CreateFakeJavaHome();
        try {
            var package = Path.Combine(minecraft, "test.pcltransfer");
            MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = package,
                IncludeJava = true,
                JavaHome = javaHome
            });

            var preview = MachineTransferService.Inspect(package);
            await Assert.That(preview.Manifest.IncludesJava).IsTrue();
            await Assert.That(preview.Manifest.JavaVersion).IsEqualTo("21.0.1");

            var targetRoot = Path.Combine(minecraft, "target");
            var javaRoot = Path.Combine(minecraft, "target", "PCL", "Java");
            var imported = MachineTransferService.Import(package, targetRoot, null, javaRoot);

            await Assert.That(imported.JavaHome).IsNotNull();
            await Assert.That(File.Exists(Path.Combine(imported.JavaHome!, "bin", "java.exe"))).IsTrue();
        } finally {
            if (Directory.Exists(minecraft)) Directory.Delete(minecraft, true);
            if (Directory.Exists(javaHome)) Directory.Delete(javaHome, true);
        }
    }

    [Test]
    public async Task 只携带白名单实例设置() {
        var settings = new Dictionary<string, object?> {
            ["VersionRamType"] = 2,
            ["VersionRamCustom"] = 8192,
            ["LoginCacheJson"] = "secret",
            ["SomeGlobalKey"] = "x"
        };
        var filtered = MachineTransferService.FilterSettings(settings);

        await Assert.That(filtered.Count).IsEqualTo(2);
        await Assert.That(filtered.ContainsKey("VersionRamType")).IsTrue();
        await Assert.That(filtered.ContainsKey("LoginCacheJson")).IsFalse();
        await Assert.That(filtered.ContainsKey("SomeGlobalKey")).IsFalse();
    }

    [Test]
    public async Task 输出文件不能放在实例目录内() {
        var instance = CreateInstance(out var minecraft);
        try {
            await Assert.That(() => MachineTransferService.Export(new MachineTransferOptions {
                InstanceFolder = instance,
                OutputPath = Path.Combine(instance, "self.pcltransfer")
            })).Throws<InvalidOperationException>();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 拒绝路径穿越的包() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-transfer-evil-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var package = Path.Combine(root, "evil.pcltransfer");
            using (var zip = ZipFile.Open(package, ZipArchiveMode.Create)) {
                var manifest = zip.CreateEntry(MachineTransferService.ManifestEntry);
                using (var writer = new StreamWriter(manifest.Open())) {
                    writer.Write("""
                        {"format":"pcl-machine-transfer","version":1,"instanceName":"evil","versionId":"evil","includesJava":false}
                        """);
                }
                var instanceJson = zip.CreateEntry("instance/evil.json");
                using (var writer = new StreamWriter(instanceJson.Open())) {
                    writer.Write("""{"id":"evil"}""");
                }
                var evil = zip.CreateEntry("instance/../../evil.txt");
                using var evilWriter = new StreamWriter(evil.Open());
                evilWriter.Write("pwned");
            }

            var targetRoot = Path.Combine(root, "target");
            await Assert.That(() => MachineTransferService.Import(package, targetRoot))
                .Throws<InvalidDataException>();
            await Assert.That(File.Exists(Path.Combine(root, "evil.txt"))).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 非搬机包会被拒绝() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-transfer-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var package = Path.Combine(root, "bad.zip");
            using (var zip = ZipFile.Open(package, ZipArchiveMode.Create)) {
                var entry = zip.CreateEntry("readme.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("hello");
            }
            await Assert.That(() => MachineTransferService.Inspect(package))
                .Throws<InvalidDataException>();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 默认文件名去掉非法字符() {
        var name = MachineTransferService.DefaultFileName("1.20.1-Forge");
        await Assert.That(name).IsEqualTo("1.20.1-Forge.pcltransfer");
    }

}
