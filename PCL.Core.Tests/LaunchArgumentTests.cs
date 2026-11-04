using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchArgumentTests {

    [Test]
    public async Task 自定义JVM参数追加在版本参数之后() {
        var json = JObject.Parse("""
        {"arguments":{"jvm":["-Xmx2G","-Dfile.encoding=UTF-8"]}}
        """);
        var arguments = JvmArgumentCollector.Collect(json, "10.0", ["-XX:+UseG1GC", "-Dfoo=bar"]);
        await Assert.That(arguments.Count).IsEqualTo(4);
        await Assert.That(arguments[2]).IsEqualTo("-XX:+UseG1GC");
        await Assert.That(arguments[3]).IsEqualTo("-Dfoo=bar");
    }

    [Test]
    public async Task 空自定义参数被忽略() {
        var json = JObject.Parse("""{"arguments":{"jvm":["-Xmx2G"]}}""");
        var arguments = JvmArgumentCollector.Collect(json, "10.0", ["", "   ", "-Xss1M"]);
        await Assert.That(arguments.Count).IsEqualTo(2);
        await Assert.That(arguments[1]).IsEqualTo("-Xss1M");
    }

    [Test]
    public async Task 版本没有JVM参数时仍可追加() {
        var json = JObject.Parse("""{"id":"1.20.1"}""");
        var arguments = JvmArgumentCollector.Collect(json, "10.0", ["-XX:+UseG1GC"]);
        await Assert.That(arguments.Count).IsEqualTo(1);
        await Assert.That(arguments[0]).IsEqualTo("-XX:+UseG1GC");
    }

    [Test]
    public async Task 不传自定义参数时行为不变() {
        var json = JObject.Parse("""{"arguments":{"jvm":["-Xmx2G"]}}""");
        var arguments = JvmArgumentCollector.Collect(json, "10.0");
        await Assert.That(arguments.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 监视配置携带窗口标题与发布年份() {
        var json = JObject.Parse("""{"releaseTime":"2023-06-12T12:00:00+00:00"}""");
        var options = LaunchService.BuildWatcherOptions(json, "Minecraft {date}");
        await Assert.That(options.WindowTitle).IsEqualTo("Minecraft {date}");
        await Assert.That(options.ReleaseYear).IsEqualTo(2023);
    }

    [Test]
    public async Task 空白标题视为未设置() {
        var json = JObject.Parse("""{"releaseTime":"2011-01-01T00:00:00+00:00"}""");
        var options = LaunchService.BuildWatcherOptions(json, "   ");
        await Assert.That(options.WindowTitle).IsNull();
        await Assert.That(options.ReleaseYear).IsEqualTo(2011);
    }

}
