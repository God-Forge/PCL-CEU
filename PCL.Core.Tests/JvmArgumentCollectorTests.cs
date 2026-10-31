using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JvmArgumentCollectorTests {

    [Test]
    public async Task 收集并去除首尾空白() {
        var json = JObject.Parse("""
        {
          "arguments": {
            "jvm": [
              " -Xmx2G ",
              { "value": [" -Dfoo=bar ", "-Djava.library.path=${natives_directory}"] }
            ]
          }
        }
        """);
        var result = JvmArgumentCollector.Collect(json, "10.0");
        await Assert.That(result.Count).IsEqualTo(3);
        await Assert.That(result[0]).IsEqualTo("-Xmx2G");
        await Assert.That(result[1]).IsEqualTo("-Dfoo=bar");
        await Assert.That(result[2]).IsEqualTo("-Djava.library.path=${natives_directory}");
    }

    [Test]
    public async Task 按规则过滤JVM参数() {
        var json = JObject.Parse("""
        {
          "arguments": {
            "jvm": [
              { "rules": [{"action":"allow","os":{"name":"windows"}}], "value": "-Dwindows=true" },
              { "rules": [{"action":"allow","os":{"name":"linux"}}], "value": "-Dlinux=true" }
            ]
          }
        }
        """);
        var result = JvmArgumentCollector.Collect(json, "10.0");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("-Dwindows=true");
    }

    [Test]
    public async Task 缺少jvm参数时返回空列表() {
        var result = JvmArgumentCollector.Collect(new JObject(), "10.0");
        await Assert.That(result.Count).IsEqualTo(0);
    }

}
