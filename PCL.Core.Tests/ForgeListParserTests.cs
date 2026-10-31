using Newtonsoft.Json.Linq;
using PCL.Core.Download;

namespace PCL.Core.Tests;

public class ForgeListParserTests {

    [Test]
    public async Task 解析官方HTML并补上124() {
        var html = string.Concat(Enumerable.Range(0, 10).Select(i => $"<a href=\"index_1.{i}.2.html\">v</a>"));
        var names = ForgeListParser.ParseOfficial(html);
        await Assert.That(names.Count).IsEqualTo(11);
        await Assert.That(names.Contains("1.0.2")).IsTrue();
        await Assert.That(names.Contains("1.2.4")).IsTrue();
    }

    [Test]
    public async Task 解析BMCLAPI数组() {
        var json = JArray.Parse("""["1.20.1","1.19.2","1.7.10"]""");
        var names = ForgeListParser.ParseBmclapi(json);
        await Assert.That(names.Count).IsEqualTo(3);
        await Assert.That(names[1]).IsEqualTo("1.19.2");
    }

}
