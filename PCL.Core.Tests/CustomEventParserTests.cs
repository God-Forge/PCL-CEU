using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class CustomEventParserTests {

    [Test]
    public async Task 解析中文类型名() {
        await Assert.That(CustomEventParser.ParseType("打开网页")).IsEqualTo(CustomEventType.OpenWebsite);
        await Assert.That(CustomEventParser.ParseType("复制文本")).IsEqualTo(CustomEventType.CopyText);
        await Assert.That(CustomEventParser.ParseType("切换页面")).IsEqualTo(CustomEventType.SwitchPage);
        await Assert.That(CustomEventParser.ParseType("今日人品")).IsEqualTo(CustomEventType.DailyLuck);
    }

    [Test]
    public async Task 解析英文别名() {
        await Assert.That(CustomEventParser.ParseType("OpenWebsite")).IsEqualTo(CustomEventType.OpenWebsite);
        await Assert.That(CustomEventParser.ParseType("openwebsite")).IsEqualTo(CustomEventType.OpenWebsite);
        await Assert.That(CustomEventParser.ParseType("Copy")).IsEqualTo(CustomEventType.CopyText);
    }

    [Test]
    public async Task 未知或空类型返回None() {
        await Assert.That(CustomEventParser.ParseType(null)).IsEqualTo(CustomEventType.None);
        await Assert.That(CustomEventParser.ParseType("")).IsEqualTo(CustomEventType.None);
        await Assert.That(CustomEventParser.ParseType("不存在的事件")).IsEqualTo(CustomEventType.None);
    }

    [Test]
    public async Task 参数按竖线拆分并去空() {
        var arguments = CustomEventParser.SplitArguments("a|b|| c ");
        await Assert.That(arguments.Count).IsEqualTo(3);
        await Assert.That(arguments[0]).IsEqualTo("a");
        await Assert.That(arguments[2]).IsEqualTo("c");
        await Assert.That(CustomEventParser.SplitArguments(null).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 合法网址通过校验() {
        await Assert.That(CustomEventParser.IsValidUrl("https://www.example.com/a?b=1", out _)).IsTrue();
        await Assert.That(CustomEventParser.IsValidUrl("http://example.com", out _)).IsTrue();
    }

    [Test]
    public async Task 缺少协议时拒绝() {
        await Assert.That(CustomEventParser.IsValidUrl("www.example.com", out var error)).IsFalse();
        await Assert.That(error).Contains("协议");
    }

    [Test]
    public async Task 空网址与非法协议被拒绝() {
        await Assert.That(CustomEventParser.IsValidUrl("", out _)).IsFalse();
        // 不含 :// 的伪协议会先被「缺少协议」拦下，同样是拒绝
        await Assert.That(CustomEventParser.IsValidUrl("javascript:alert(1)", out _)).IsFalse();
        await Assert.That(CustomEventParser.IsValidUrl("ftp://example.com", out var error)).IsFalse();
        await Assert.That(error).Contains("不支持的协议");
    }

    [Test]
    public async Task 高危类型被标记为受限() {
        await Assert.That(CustomEventParser.IsRestricted(CustomEventType.RunCommand)).IsTrue();
        await Assert.That(CustomEventParser.IsRestricted(CustomEventType.WriteSetting)).IsTrue();
        await Assert.That(CustomEventParser.IsRestricted(CustomEventType.OpenWebsite)).IsFalse();
        await Assert.That(CustomEventParser.IsRestricted(CustomEventType.ShowHint)).IsFalse();
    }

}
