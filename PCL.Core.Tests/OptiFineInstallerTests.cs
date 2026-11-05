using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class OptiFineInstallerTests {

    [Test]
    public async Task 安装参数包含静默参数与目录() {
        var arguments = OptiFineInstaller.BuildArguments(@"C:\tmp\OptiFine_1.20.1.jar", @"C:\mc");
        await Assert.That(arguments[0]).IsEqualTo("-jar");
        await Assert.That(arguments[1]).IsEqualTo(@"C:\tmp\OptiFine_1.20.1.jar");
        await Assert.That(arguments[2]).IsEqualTo("--install");
        await Assert.That(arguments[3]).IsEqualTo(@"C:\mc");
    }

    [Test]
    public async Task 版本目录推导() {
        await Assert.That(OptiFineInstaller.ResolveVersionFolder(@"C:\mc", "1.20.1_OptiFine_HD_U_I6"))
            .IsEqualTo(Path.Combine(@"C:\mc", "versions", "1.20.1_OptiFine_HD_U_I6"));
    }

    [Test]
    public async Task 产物判断依赖版本JSON() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-of-" + Guid.NewGuid().ToString("N"));
        var versionName = "1.20.1_OptiFine_HD_U_I6";
        try {
            await Assert.That(OptiFineInstaller.HasVersionFiles(root, versionName)).IsFalse();
            var folder = Path.Combine(root, "versions", versionName);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, versionName + ".json"), "{}");
            await Assert.That(OptiFineInstaller.HasVersionFiles(root, versionName)).IsTrue();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 安装器不存在时失败() {
        var result = await OptiFineInstaller.InstallAsync(
            Path.Combine(Path.GetTempPath(), "no-such-optifine.jar"),
            Path.GetTempPath(), "java.exe", "1.20.1_OptiFine");
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("安装器不存在");
    }

    [Test]
    public async Task Java不存在时失败() {
        var installer = Path.Combine(Path.GetTempPath(), "pcl-of-installer-" + Guid.NewGuid().ToString("N") + ".jar");
        File.WriteAllBytes(installer, [0x50, 0x4B, 0x03, 0x04]);
        try {
            var result = await OptiFineInstaller.InstallAsync(
                installer, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "no-java.exe"), "1.20.1_OptiFine");
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("找不到 Java");
        } finally {
            if (File.Exists(installer)) File.Delete(installer);
        }
    }

    [Test]
    public async Task 已安装时直接返回成功() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-of-" + Guid.NewGuid().ToString("N"));
        var versionName = "1.20.1_OptiFine_HD_U_I6";
        var installer = Path.Combine(root, "installer.jar");
        try {
            Directory.CreateDirectory(Path.Combine(root, "versions", versionName));
            File.WriteAllText(Path.Combine(root, "versions", versionName, versionName + ".json"), "{}");
            Directory.CreateDirectory(root);
            File.WriteAllBytes(installer, [0x50, 0x4B]);
            var result = await OptiFineInstaller.InstallAsync(installer, root, "java.exe", versionName);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Message).Contains("已安装");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Java不存在时不启动进程() {
        // 该用例只验证前置校验，真实安装需要 Java 环境
        var installer = Path.Combine(Path.GetTempPath(), "pcl-of-" + Guid.NewGuid().ToString("N") + ".jar");
        File.WriteAllBytes(installer, [0x50, 0x4B]);
        try {
            var result = await OptiFineInstaller.InstallAsync(installer, Path.GetTempPath(), "", "1.20.1_OptiFine");
            await Assert.That(result.Success).IsFalse();
        } finally {
            if (File.Exists(installer)) File.Delete(installer);
        }
    }

}
