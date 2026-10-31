using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JavaArgumentParserTests {

    [Test]
    public async Task 按空格分割但保留引号() {
        var result = JavaArgumentParser.Split("-cp \"C:\\a b\\x.jar\" -Xmx1G");
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo("-cp");
        await Assert.That(result[1]).IsEqualTo("\"C:\\a b\\x.jar\"");
        await Assert.That(result[2]).IsEqualTo("-Xmx1G");
    }

    [Test]
    public async Task 转义引号不切换引号状态() {
        var result = JavaArgumentParser.Split("-Dfoo=\"a\\\"b\" -Xmx1G");
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo("-Dfoo=\"a\\\"b\"");
    }

    [Test]
    public async Task 游戏参数重复时用新值覆盖() {
        var result = JavaArgumentParser.Deduplicate(["--width", "800", "--width", "1024"], false);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo("--width");
        await Assert.That(result[1]).IsEqualTo("1024");
    }

    [Test]
    public async Task JVM单参数重复时保留第一个() {
        var result = JavaArgumentParser.Deduplicate(["-Xmx1G", "-Xmx1G"], true);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("-Xmx1G");
    }

    [Test]
    public async Task TweakClass不覆盖并保留不同值() {
        var result = JavaArgumentParser.Deduplicate(["--tweakClass", "a", "--tweakClass", "b"], false);
        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result[1]).IsEqualTo("a");
        await Assert.That(result[3]).IsEqualTo("b");
    }

    [Test]
    public async Task 负数值仍被视为参数值() {
        var result = JavaArgumentParser.Deduplicate(["-xPos", "23", "-xPos", "-50"], false);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[1]).IsEqualTo("-50");
    }

}
