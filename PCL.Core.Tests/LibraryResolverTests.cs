using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LibraryResolverTests {

    private static RuleContext Windows => new() {
        OsName = "windows",
        OsVersion = "10.0.19045",
        Arch = "x86_64"
    };

    [Test]
    public async Task 解析Artifact路径与URL() {
        var json = JObject.Parse("{\"libraries\":[{\"name\":\"com.example:lib:1.0\",\"downloads\":{\"artifact\":{\"path\":\"com/example/lib/1.0/lib-1.0.jar\",\"url\":\"https://example.com/lib.jar\",\"sha1\":\"abc\",\"size\":123}}}]}");
        var result = LibraryResolver.Resolve(json, @"C:\mc", Windows);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].LocalPath).IsEqualTo(@"libraries\com\example\lib\1.0\lib-1.0.jar");
        await Assert.That(result[0].Url).IsEqualTo("https://example.com/lib.jar");
        await Assert.That(result[0].Sha1).IsEqualTo("abc");
        await Assert.That(result[0].Size).IsEqualTo(123);
    }

    [Test]
    public async Task 规则不满足时跳过() {
        var json = JObject.Parse("{\"libraries\":[{\"name\":\"com.example:lib:1.0\",\"rules\":[{\"action\":\"allow\",\"os\":{\"name\":\"linux\"}}],\"downloads\":{\"artifact\":{\"path\":\"lib.jar\"}}}]}");
        var result = LibraryResolver.Resolve(json, @"C:\mc", Windows);
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 解析Natives() {
        var json = JObject.Parse("{\"libraries\":[{\"name\":\"org.example:native:1.0\",\"natives\":{\"windows\":\"natives-windows\"},\"downloads\":{\"classifiers\":{\"natives-windows\":{\"path\":\"org/example/native/1.0/native-1.0-natives-windows.jar\",\"url\":\"https://example.com/native.jar\"}}}}]}");
        var result = LibraryResolver.Resolve(json, @"C:\mc", Windows);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].IsNative).IsTrue();
        await Assert.That(result[0].LocalPath.Contains("natives-windows")).IsTrue();
    }

    [Test]
    public async Task 使用Maven根地址推导() {
        var json = JObject.Parse("{\"libraries\":[{\"name\":\"net.fabricmc:fabric-loader:0.16.9\",\"url\":\"https://maven.fabricmc.net/\"}]}");
        var result = LibraryResolver.Resolve(json, @"C:\mc", Windows);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Url).IsEqualTo("https://maven.fabricmc.net/net/fabricmc/fabric-loader/0.16.9/fabric-loader-0.16.9.jar");
    }

}
