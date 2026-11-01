using PCL.Core.Net;

namespace PCL.Core.Tests;

public class DownloadFileValidatorTests {

    [Test]
    public async Task 校验HTTP与HTTPS地址() {
        await Assert.That(DownloadFileValidator.IsHttpUrl("https://example.com/a.jar")).IsTrue();
        await Assert.That(DownloadFileValidator.IsHttpUrl("http://example.com/a.jar")).IsTrue();
        await Assert.That(DownloadFileValidator.IsHttpUrl("ftp://example.com/a.jar")).IsFalse();
        await Assert.That(DownloadFileValidator.IsHttpUrl(null)).IsFalse();
    }

    [Test]
    public async Task 规范化本地路径并拒绝目录() {
        await Assert.That(DownloadFileValidator.NormalizeLocalPath("C:/mc/a.jar")).IsEqualTo("C:\\mc\\a.jar");
        await Assert.That(() => DownloadFileValidator.NormalizeLocalPath("relative/a.jar"))
            .Throws<ArgumentException>();
        await Assert.That(() => DownloadFileValidator.NormalizeLocalPath("C:\\mc\\"))
            .Throws<ArgumentException>();
    }

}
