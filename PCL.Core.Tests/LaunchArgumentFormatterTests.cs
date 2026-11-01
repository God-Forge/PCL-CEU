using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchArgumentFormatterTests {

    [Test]
    public async Task 替换标记并保留普通参数() {
        var result = LaunchArgumentFormatter.Format(
            ["-Dfoo=${bar}", "--username", "Steve"],
            [("${bar}", "value")]);
        await Assert.That(result[0]).IsEqualTo("-Dfoo=value");
        await Assert.That(result[1]).IsEqualTo("--username");
        await Assert.That(result[2]).IsEqualTo("Steve");
    }

    [Test]
    public async Task 特殊字符参数补引号并转义内部引号() {
        var result = LaunchArgumentFormatter.Format(
            ["C:\\a b\\x.jar", "\"already quoted\"", "a\"b"],
            []);
        await Assert.That(result[0]).IsEqualTo("\"C:\\a b\\x.jar\"");
        await Assert.That(result[1]).IsEqualTo("\"already quoted\"");
        await Assert.That(result[2]).IsEqualTo("a\"b");
    }

    [Test]
    public async Task 空格参数补引号() {
        var result = LaunchArgumentFormatter.Format(["hello world"], []);
        await Assert.That(result[0]).IsEqualTo("\"hello world\"");
    }

    [Test]
    public async Task 单个参数格式化() {
        await Assert.That(LaunchArgumentFormatter.FormatSingle("hello world")).IsEqualTo("\"hello world\"");
        await Assert.That(LaunchArgumentFormatter.FormatSingle("\"already\"")).IsEqualTo("\"already\"");
        await Assert.That(LaunchArgumentFormatter.FormatSingle("plain")).IsEqualTo("plain");
    }

}
