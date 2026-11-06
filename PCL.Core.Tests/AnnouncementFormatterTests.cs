using PCL.Core.Update;

namespace PCL.Core.Tests;

public class AnnouncementFormatterTests {

    private static UpdateInfo Info(bool hasUpdate, string notes = "", string latest = "v1.0.3", string current = "1.0.2") => new() {
        CurrentVersion = current,
        LatestVersion = latest,
        HasUpdate = hasUpdate,
        ReleaseNotes = notes
    };

    [Test]
    public async Task 没有更新时不打扰() {
        await Assert.That(AnnouncementFormatter.Build(null)).IsNull();
        await Assert.That(AnnouncementFormatter.Build(Info(false))).IsNull();
    }

    [Test]
    public async Task 有更新时生成公告() {
        var text = AnnouncementFormatter.Build(Info(true, "修复了若干问题"));
        await Assert.That(text).IsNotNull();
        await Assert.That(text!).Contains("v1.0.3");
        await Assert.That(text).Contains("1.0.2");
        await Assert.That(text).Contains("修复了若干问题");
    }

    [Test]
    public async Task 没有说明时只有标题() {
        var text = AnnouncementFormatter.Build(Info(true, ""));
        await Assert.That(text).IsEqualTo("发现新版本 v1.0.3（当前 1.0.2）");
    }

    [Test]
    public async Task 摘要去掉Markdown标记() {
        var notes = """
        ## 主要变化

        - 新增 **账号管理**
        - 修复崩溃分析
        """;
        var summary = AnnouncementFormatter.Summarize(notes);
        await Assert.That(summary).DoesNotContain("#");
        await Assert.That(summary).DoesNotContain("*");
        await Assert.That(summary).Contains("主要变化");
        await Assert.That(summary).Contains("账号管理");
    }

    [Test]
    public async Task 摘要在超长时截断() {
        var notes = string.Join("\n", Enumerable.Repeat("这是一行很长的说明文字", 40));
        var summary = AnnouncementFormatter.Summarize(notes, 50);
        await Assert.That(summary.Length).IsLessThanOrEqualTo(51);
        await Assert.That(summary.EndsWith('…')).IsTrue();
    }

    [Test]
    public async Task 空说明返回空串() {
        await Assert.That(AnnouncementFormatter.Summarize(null)).IsEqualTo("");
        await Assert.That(AnnouncementFormatter.Summarize("   \n\n  ")).IsEqualTo("");
    }

    [Test]
    public async Task 版本号为空时不生成() {
        var info = new UpdateInfo { HasUpdate = true, LatestVersion = "", CurrentVersion = "1.0.2" };
        await Assert.That(AnnouncementFormatter.Build(info)).IsNull();
    }

}
