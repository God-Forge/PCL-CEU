using Newtonsoft.Json.Linq;
using PCL.Core.Download;

namespace PCL.Core.Tests;

public class ForgeVersionParserTests {

    [Test]
    public async Task 解析官方HTML版本信息() {
        const string html = """
        <table>
        <td class="download-version fa promo-recommended">
        <a>47.2.0</a>
        <span class="download-time" title="2024-01-01 00:00:00"></span>
        <div class="classifier-installer">installer.jar</div>
        <strong>MD5:</strong> abcdef
        </td>
        </table>
        """;
        var versions = ForgeVersionParser.ParseOfficial(html, "1.20.1");
        await Assert.That(versions.Count).IsEqualTo(1);
        await Assert.That(versions[0].VersionName).IsEqualTo("47.2.0");
        await Assert.That(versions[0].Category).IsEqualTo("installer");
        await Assert.That(versions[0].Hash).IsEqualTo("abcdef");
        await Assert.That(versions[0].IsRecommended).IsTrue();
        await Assert.That(versions[0].Inherit).IsEqualTo("1.20.1");
    }

    [Test]
    public async Task 解析BMCLAPI版本与安装类型优先级() {
        var json = JArray.Parse("""
        [
          {
            "version": "47.2.0",
            "branch": null,
            "modified": "2024-01-01T00:00:00.000+0000",
            "files": [
              { "category": "client", "format": "zip", "hash": "client" },
              { "category": "universal", "format": "zip", "hash": "universal" },
              { "category": "installer", "format": "jar", "hash": "installer" }
            ]
          },
          {
            "version": "47.1.0",
            "branch": "beta",
            "modified": "2024-01-02T00:00:00.000+0000",
            "files": [
              { "category": "client", "format": "zip", "hash": "client2" }
            ]
          }
        ]
        """);

        var versions = ForgeVersionParser.ParseBmclapi(json, "1.20.1", "47.2.0");

        await Assert.That(versions.Count).IsEqualTo(2);
        await Assert.That(versions[0].VersionName).IsEqualTo("47.2.0");
        await Assert.That(versions[0].Category).IsEqualTo("installer");
        await Assert.That(versions[0].Hash).IsEqualTo("installer");
        await Assert.That(versions[0].IsRecommended).IsTrue();
        await Assert.That(versions[0].Inherit).IsEqualTo("1.20.1");
        await Assert.That(versions[0].ReleaseTime.Length).IsEqualTo(16);
        await Assert.That(versions[1].Branch).IsEqualTo("beta");
        await Assert.That(versions[1].Category).IsEqualTo("client");
        await Assert.That(versions[1].IsRecommended).IsFalse();
    }

    [Test]
    public async Task 无版本号时跳过() {
        var json = JArray.Parse("""[{"modified":"2024-01-01T00:00:00Z","files":[]}]""");
        var versions = ForgeVersionParser.ParseBmclapi(json, "1.20.1", null);
        await Assert.That(versions.Count).IsEqualTo(0);
    }

}
