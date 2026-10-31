using PCL.Core.Download;

namespace PCL.Core.Tests;

public class NeoForgeVersionParserTests {

    [Test]
    public async Task 提取并排除不可下载版本() {
        const string latest = """
        {"versions":["21.1.1","21.1.2-beta","47.1.82","1.20.1-47.1.99","26.1.0.0-alpha.1+snapshot-1"]}
        """;
        const string legacy = """{"versions":["1.20.1-47.1.99"]}""";
        var versions = NeoForgeVersionParser.Parse(latest, legacy);
        await Assert.That(versions.Contains("21.1.1")).IsTrue();
        await Assert.That(versions.Contains("21.1.2-beta")).IsTrue();
        await Assert.That(versions.Contains("47.1.82")).IsFalse();
        await Assert.That(versions.Contains("1.20.1-47.1.99")).IsTrue();
        await Assert.That(versions.Contains("26.1.0.0-alpha.1+snapshot-1")).IsTrue();
        await Assert.That(versions.Count).IsEqualTo(versions.Distinct().Count());
    }

}
