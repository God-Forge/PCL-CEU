using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class AssetIndexResolverTests {

    [Test]
    public async Task 解析普通对象路径() {
        var json = JObject.Parse("""
        {
          "objects": {
            "minecraft/sounds/a.ogg": { "hash": "abcdef", "size": 12 }
          }
        }
        """);
        var result = AssetIndexResolver.ResolveObjects(json, @"D:\instances\demo\", @"D:\mc");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].SourcePath).IsEqualTo("minecraft/sounds/a.ogg");
        await Assert.That(result[0].LocalPath).IsEqualTo(@"D:\mc\assets\objects\ab\abcdef");
        await Assert.That(result[0].Size).IsEqualTo(12);
    }

    [Test]
    public async Task 支持virtual和mapToResources() {
        var virtualJson = JObject.Parse("""
        { "virtual": true, "objects": { "a/b.txt": { "hash": "12", "size": 1 } } }
        """);
        var virtualResult = AssetIndexResolver.ResolveObjects(virtualJson, @"D:\instances\demo\", @"D:\mc");
        await Assert.That(virtualResult[0].LocalPath).IsEqualTo(@"D:\mc\assets\virtual\legacy\a\b.txt");

        var remapJson = JObject.Parse("""
        { "map_to_resources": true, "objects": { "a/b.txt": { "hash": "12", "size": 1 } } }
        """);
        var remapResult = AssetIndexResolver.ResolveObjects(remapJson, @"D:\instances\demo\", @"D:\mc");
        await Assert.That(remapResult[0].LocalPath).IsEqualTo(@"D:\instances\demo\resources\a\b.txt");
    }

}
