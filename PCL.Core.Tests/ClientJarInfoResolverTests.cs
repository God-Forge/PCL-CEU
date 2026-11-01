using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ClientJarInfoResolverTests {

    [Test]
    public async Task 解析客户端Jar信息() {
        var json = JObject.Parse("""
        {
          "downloads": {
            "client": { "url": "https://example.com/client.jar", "sha1": "abc", "size": 1234 }
          }
        }
        """);
        var info = ClientJarInfoResolver.Resolve(json);
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Url).IsEqualTo("https://example.com/client.jar");
        await Assert.That(info.Sha1).IsEqualTo("abc");
        await Assert.That(info.Size).IsEqualTo(1234);
    }

    [Test]
    public async Task 缺少客户端信息时返回空() {
        await Assert.That(ClientJarInfoResolver.Resolve(new JObject())).IsNull();
        await Assert.That(ClientJarInfoResolver.Resolve(JObject.Parse("""{"downloads":{}}"""))).IsNull();
    }

}
