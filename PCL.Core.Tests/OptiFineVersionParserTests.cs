using Newtonsoft.Json.Linq;
using PCL.Core.Download;

namespace PCL.Core.Tests;

public class OptiFineVersionParserTests {

    [Test]
    public async Task 解析官方HTML() {
        var rows = string.Concat(Enumerable.Range(0, 10).Select(i =>
            $"<td class='colForge'>Forge #47.{i}</td>" +
            $"<td class='colDate'>2024.1.{i + 1}</td>" +
            $"<a href=\"OptiFine_1.20.1_HD_U_I{i}.jar\">download</a>"));
        var versions = OptiFineVersionParser.ParseOfficial(rows);
        await Assert.That(versions.Count).IsEqualTo(10);
        await Assert.That(versions[0].Inherit).IsEqualTo("1.20.1");
        await Assert.That(versions[0].DisplayName).IsEqualTo("1.20.1 I0");
        await Assert.That(versions[0].ReleaseTime).IsEqualTo("1/1/2024");
        await Assert.That(versions[0].RequiredForgeVersion).IsEqualTo("47.0");
        await Assert.That(versions[0].FileName).IsEqualTo("OptiFine_1.20.1_HD_U_I0.jar");
    }

    [Test]
    public async Task 解析BMCLAPI并处理NA() {
        var json = JArray.Parse("""
        [
          { "mcversion": "1.20.1", "type": "HD_U_I6", "patch": "", "filename": "OptiFine_1.20.1_HD_U_I6.jar", "forge": "Forge #47.2.0" },
          { "mcversion": "1.8.9", "type": "HD_U_D8", "patch": "", "filename": "OptiFine_1.8.9_HD_U_D8.jar", "forge": "N/A" }
        ]
        """);
        var versions = OptiFineVersionParser.ParseBmclapi(json);
        await Assert.That(versions.Count).IsEqualTo(2);
        await Assert.That(versions[0].Inherit).IsEqualTo("1.20.1");
        await Assert.That(versions[0].RequiredForgeVersion).IsEqualTo("47.2.0");
        await Assert.That(versions[1].RequiredForgeVersion).IsNull();
    }

}
