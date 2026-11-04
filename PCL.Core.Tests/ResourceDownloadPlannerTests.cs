using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceDownloadPlannerTests {

    private static ResourceVersionFile Version(string fileName, params string[] urls) => new() {
        Id = "1",
        FileName = fileName,
        DownloadUrls = urls,
        Size = 1024
    };

    [Test]
    public async Task 生成带备用地址的下载请求() {
        var request = ResourceDownloadPlanner.CreateRequest(
            Version("sodium.jar", "https://a/x.jar", "https://b/x.jar"), @"C:\mc\mods");
        await Assert.That(request).IsNotNull();
        await Assert.That(request!.Url).IsEqualTo("https://a/x.jar");
        await Assert.That(request.BackupUrls.Count).IsEqualTo(1);
        await Assert.That(request.TargetPath).IsEqualTo(@"C:\mc\mods\sodium.jar");
        await Assert.That(request.MinSize).IsEqualTo(1024);
    }

    [Test]
    public async Task 缺少下载信息时返回空() {
        var request = ResourceDownloadPlanner.CreateRequest(Version("", "https://a/x.jar"), @"C:\mc\mods");
        await Assert.That(request).IsNull();
        var noUrl = ResourceDownloadPlanner.CreateRequest(Version("x.jar"), @"C:\mc\mods");
        await Assert.That(noUrl).IsNull();
    }

    [Test]
    public async Task 目标目录为空时抛出参数异常() {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Task.FromResult(ResourceDownloadPlanner.CreateRequest(Version("x.jar", "https://a/x.jar"), " ")));
    }

    [Test]
    public async Task 文件名非法字符被替换() {
        var request = ResourceDownloadPlanner.CreateRequest(
            Version("a/b:c*.jar", "https://a/x.jar"), @"C:\mc\mods");
        await Assert.That(request!.TargetPath).DoesNotContain("/");
        await Assert.That(Path.GetFileName(request.TargetPath)).IsEqualTo("a_b_c_.jar");
    }

    [Test]
    public async Task 可覆盖下载文件名() {
        var request = ResourceDownloadPlanner.CreateRequest(
            Version("original.jar", "https://a/x.jar"), @"C:\mc\mods", "renamed.jar");
        await Assert.That(Path.GetFileName(request!.TargetPath)).IsEqualTo("renamed.jar");
    }

    [Test]
    public async Task 路径穿越文件名被净化() {
        await Assert.That(ResourceDownloadPlanner.SanitizeFileName("..")).IsEqualTo("");
        await Assert.That(ResourceDownloadPlanner.SanitizeFileName("../../evil.jar")).IsEqualTo(".._.._evil.jar");
    }

}
