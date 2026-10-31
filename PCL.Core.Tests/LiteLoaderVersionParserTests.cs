using Newtonsoft.Json.Linq;
using PCL.Core.Download;

namespace PCL.Core.Tests;

public class LiteLoaderVersionParserTests {

    [Test]
    public async Task 解析版本并跳过旧版() {
        var json = JObject.Parse("""
        {
          "versions": {
            "1.5.2": { "artefacts": { "com.mumfrey:liteloader": { "latest": { "stream": "release", "md5": "skip" } } } },
            "1.8": { "artefacts": { "com.mumfrey:liteloader": { "latest": { "stream": "release", "md5": "a", "timestamp": "2020-01-01T00:00:00Z" } } } },
            "1.12.2": { "snapshots": { "com.mumfrey:liteloader": { "latest": { "stream": "snapshot", "md5": "b", "timestamp": "2021-01-01T00:00:00Z" } } } }
          }
        }
        """);
        var versions = LiteLoaderVersionParser.Parse(json);
        await Assert.That(versions.Count).IsEqualTo(2);
        await Assert.That(versions[0].Inherit).IsEqualTo("1.8");
        await Assert.That(versions[0].FileName).IsEqualTo("liteloader-installer-1.8.0-00-SNAPSHOT.jar");
        await Assert.That(versions[0].IsPreview).IsFalse();
        await Assert.That(versions[1].Inherit).IsEqualTo("1.12.2");
        await Assert.That(versions[1].IsPreview).IsTrue();
        await Assert.That(versions[1].IsLegacy).IsFalse();
        await Assert.That(versions[1].Md5).IsEqualTo("b");
    }

}
