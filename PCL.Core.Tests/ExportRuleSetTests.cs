using PCL.Core.Modpack;

namespace PCL.Core.Tests;

public class ExportRuleSetTests {

    [Test]
    public async Task 解析包含与排除() {
        var rules = ExportRuleSet.Parse("mods/|!mods/*.disabled|!mods/*.old|!mods/.connector/");
        await Assert.That(rules.Includes.Count).IsEqualTo(1);
        await Assert.That(rules.Excludes.Count).IsEqualTo(3);
        await Assert.That(rules.Includes[0]).IsEqualTo("mods/");
    }

    [Test]
    public async Task 空规则不包含任何内容() {
        var rules = ExportRuleSet.Parse(null);
        await Assert.That(rules.IsEmpty).IsTrue();
        await Assert.That(rules.ShouldInclude("mods/a.jar")).IsFalse();
    }

    [Test]
    public async Task 目录规则包含其下所有文件() {
        var rules = ExportRuleSet.Parse("mods/");
        await Assert.That(rules.ShouldInclude("mods/a.jar")).IsTrue();
        await Assert.That(rules.ShouldInclude("mods/sub/b.jar")).IsTrue();
        await Assert.That(rules.ShouldInclude("config/a.toml")).IsFalse();
    }

    [Test]
    public async Task 排除优先于包含() {
        var rules = ExportRuleSet.Parse("mods/|!mods/*.disabled");
        await Assert.That(rules.ShouldInclude("mods/a.jar")).IsTrue();
        await Assert.That(rules.ShouldInclude("mods/b.jar.disabled")).IsFalse();
    }

    [Test]
    public async Task 精确文件名规则() {
        var rules = ExportRuleSet.Parse("options.txt|hotbar.nbt");
        await Assert.That(rules.ShouldInclude("options.txt")).IsTrue();
        await Assert.That(rules.ShouldInclude("hotbar.nbt")).IsTrue();
        await Assert.That(rules.ShouldInclude("other.txt")).IsFalse();
    }

    [Test]
    public async Task 通配规则() {
        var rules = ExportRuleSet.Parse("shaderpacks/*.txt");
        await Assert.That(rules.ShouldInclude("shaderpacks/a.txt")).IsTrue();
        await Assert.That(rules.ShouldInclude("shaderpacks/a.zip")).IsFalse();
    }

    [Test]
    public async Task 反斜杠路径也能匹配() {
        var rules = ExportRuleSet.Parse("mods/");
        await Assert.That(rules.ShouldInclude(@"mods\a.jar")).IsTrue();
    }

    [Test]
    public async Task 合并多个规则集() {
        var first = ExportRuleSet.Parse("mods/");
        var second = ExportRuleSet.Parse("config/|!config/secret.txt");
        var merged = ExportRuleSet.Merge([first, second]);
        await Assert.That(merged.ShouldInclude("mods/a.jar")).IsTrue();
        await Assert.That(merged.ShouldInclude("config/a.toml")).IsTrue();
        await Assert.That(merged.ShouldInclude("config/secret.txt")).IsFalse();
    }

    [Test]
    public async Task 合并时去重() {
        var merged = ExportRuleSet.Merge([
            ExportRuleSet.Parse("mods/"), ExportRuleSet.Parse("mods/")
        ]);
        await Assert.That(merged.Includes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 规则描述() {
        await Assert.That(ExportRuleSet.Parse("").Describe()).Contains("空规则");
        await Assert.That(ExportRuleSet.Parse("a/|!b/").Describe()).Contains("包含 1 条、排除 1 条");
    }

}
