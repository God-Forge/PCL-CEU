using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class RuleEvaluatorTests {

    private static RuleContext Context(string os = "windows", string version = "10.0.19045", string arch = "x86_64", Dictionary<string, bool>? features = null) {
        return new RuleContext {
            OsName = os,
            OsVersion = version,
            Arch = arch,
            Features = features ?? new Dictionary<string, bool>()
        };
    }

    [Test]
    public async Task 无规则默认允许() {
        await Assert.That(RuleEvaluator.IsAllowed(null, Context())).IsTrue();
        await Assert.That(RuleEvaluator.IsAllowed(new JArray(), Context())).IsTrue();
    }

    [Test]
    public async Task 按系统与版本匹配() {
        var rules = JArray.Parse("[{\"action\":\"allow\",\"os\":{\"name\":\"windows\",\"version\":\"^10\\\\.\"}}]");
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context())).IsTrue();
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context(os: "linux"))).IsFalse();
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context(version: "6.1.7601"))).IsFalse();
    }

    [Test]
    public async Task 按特性匹配() {
        var rules = JArray.Parse("[{\"action\":\"allow\",\"features\":{\"is_demo_user\":true}}]");
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context(features: new Dictionary<string, bool> { ["is_demo_user"] = true }))).IsTrue();
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context())).IsFalse();
    }

    [Test]
    public async Task 最后一条匹配规则生效() {
        var rules = JArray.Parse("[{\"action\":\"allow\",\"os\":{\"name\":\"windows\"}},{\"action\":\"disallow\",\"os\":{\"name\":\"windows\"}}]");
        await Assert.That(RuleEvaluator.IsAllowed(rules, Context())).IsFalse();
    }

}
