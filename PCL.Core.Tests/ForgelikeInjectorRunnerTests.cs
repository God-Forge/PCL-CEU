using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ForgelikeInjectorRunnerTests {

    [Test]
    public async Task 解析安装器进度行() {
        await Assert.That(ForgelikeInstallerOutputParser.TryGetProgress("Extracting json", 0, out var extracting)).IsTrue();
        await Assert.That(extracting).IsEqualTo(0.27);
        await Assert.That(ForgelikeInstallerOutputParser.TryGetProgress("  File exists: Checksum validated.", 0.2, out var checksum)).IsTrue();
        await Assert.That(checksum).IsEqualTo(0.203);
        await Assert.That(ForgelikeInstallerOutputParser.TryGetProgress("unknown", 0.5, out var unchanged)).IsFalse();
        await Assert.That(unchanged).IsEqualTo(0.5);
    }

    [Test]
    public async Task 构建带Wrapper和Java9的启动参数() {
        var arguments = ForgelikeInjectorRunner.BuildArguments(
            @"C:\installer.jar", @"D:\mc", true, @"C:\wrapper.jar", @"C:\temp", @"C:\pure\", 17);
        await Assert.That(arguments).IsEqualTo(
            "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " +
            "-Doolloo.jlw.tmpdir=\"C:\\pure\" -cp \"C:\\temp\\Cache\\forge_installer.jar;C:\\installer.jar\" " +
            "-jar \"C:\\wrapper.jar\" com.bangbang93.ForgeInstaller \"D:\\mc\"");
    }

    [Test]
    public async Task 构建无Wrapper的Java8启动参数() {
        var arguments = ForgelikeInjectorRunner.BuildArguments(
            @"C:\installer.jar", @"D:\mc", false, null, @"C:\temp", @"C:\pure", 8);
        await Assert.That(arguments).IsEqualTo(
            "-cp \"C:\\temp\\Cache\\forge_installer.jar;C:\\installer.jar\" " +
            "com.bangbang93.ForgeInstaller \"D:\\mc\"");
    }

}
