using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class GameArgumentCollectorTests {

    [Test]
    public async Task 收集字符串和值数组() {
        var json = JObject.Parse("""
        {
          "arguments": {
            "game": [
              "--username",
              { "value": ["--width", "854"] },
              { "value": "--height" }
            ]
          }
        }
        """);
        var result = GameArgumentCollector.Collect(json, "10.0");
        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result[0]).IsEqualTo("--username");
        await Assert.That(result[1]).IsEqualTo("--width");
        await Assert.That(result[2]).IsEqualTo("854");
        await Assert.That(result[3]).IsEqualTo("--height");
    }

    [Test]
    public async Task 按规则过滤参数() {
        var json = JObject.Parse("""
        {
          "arguments": {
            "game": [
              { "rules": [{"action":"allow","os":{"name":"windows"}}], "value": "--windows" },
              { "rules": [{"action":"allow","os":{"name":"linux"}}], "value": "--linux" }
            ]
          }
        }
        """);
        var result = GameArgumentCollector.Collect(json, "10.0");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0]).IsEqualTo("--windows");
    }

    [Test]
    public async Task 缺少arguments时返回空列表() {
        var result = GameArgumentCollector.Collect(new JObject(), "10.0");
        await Assert.That(result.Count).IsEqualTo(0);
    }

}
