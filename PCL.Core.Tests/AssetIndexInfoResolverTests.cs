using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class AssetIndexInfoResolverTests {

    [Test]
    public async Task 解析索引ID与URL() {
        var info = AssetIndexInfoResolver.Resolve(JObject.Parse("""{"id":"5","url":"https://example.com/5.json"}"""));
        await Assert.That(info.Id).IsEqualTo("5");
        await Assert.That(info.Url).IsEqualTo("https://example.com/5.json");
    }

    [Test]
    public async Task 缺少字段时返回空字符串() {
        var info = AssetIndexInfoResolver.Resolve(new JObject());
        await Assert.That(info.Id).IsEqualTo("");
        await Assert.That(info.Url).IsEqualTo("");
    }

}
