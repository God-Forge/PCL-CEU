using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class OptiFineInstallerNameTests {

    [Test]
    public async Task 从标准文件名推导版本名() {
        await Assert.That(OptiFineInstallerName.BuildVersionName("OptiFine_1.20.1_HD_U_I6.jar", "1.20.1"))
            .IsEqualTo("1.20.1_OptiFine_HD_U_I6");
    }

    [Test]
    public async Task 预览版文件名() {
        await Assert.That(OptiFineInstallerName.BuildVersionName("preview_OptiFine_1.20.1_HD_U_I6_pre1.jar", "1.20.1"))
            .IsEqualTo("1.20.1_OptiFine_HD_U_I6_pre1");
    }

    [Test]
    public async Task 带路径的文件名() {
        await Assert.That(OptiFineInstallerName.BuildVersionName(@"C:\tmp\OptiFine_1.12.2_HD_U_G5.jar", "1.12.2"))
            .IsEqualTo("1.12.2_OptiFine_HD_U_G5");
    }

    [Test]
    public async Task 版本前缀不重复() {
        var name = OptiFineInstallerName.BuildVersionName("OptiFine_1.20.1_HD_U_I6.jar", "1.20.1");
        await Assert.That(name!.Split("_").Count(part => part == "1.20.1")).IsEqualTo(1);
    }

    [Test]
    public async Task 无法识别时返回空() {
        await Assert.That(OptiFineInstallerName.BuildVersionName(null, "1.20.1")).IsNull();
        await Assert.That(OptiFineInstallerName.BuildVersionName("", "1.20.1")).IsNull();
        await Assert.That(OptiFineInstallerName.BuildVersionName("OptiFine_1.20.1.jar", "")).IsNull();
        await Assert.That(OptiFineInstallerName.BuildVersionName("random.jar", "1.20.1")).IsNull();
        await Assert.That(OptiFineInstallerName.BuildVersionName("OptiFine_.jar", "1.20.1")).IsNull();
    }

    [Test]
    public async Task 构造下载地址() {
        var url = OptiFineInstallerName.BuildDownloadUrl("OptiFine_1.20.1_HD_U_I6.jar");
        await Assert.That(url).IsEqualTo("https://optifine.net/adloadx?f=OptiFine_1.20.1_HD_U_I6.jar");
        await Assert.That(OptiFineInstallerName.BuildDownloadUrl(null)).IsNull();
    }

}
