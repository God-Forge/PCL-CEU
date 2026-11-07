using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class ModConflictAnalyzerTests {

    private static ModInfo Mod(
        string fileName,
        string modId = "",
        string version = "",
        string[]? mixins = null,
        int? requiredJava = null) => new() {
        FilePath = @"C:\mc\mods\" + fileName,
        FileName = fileName,
        Enabled = true,
        ModId = modId,
        Version = version,
        MixinConfigs = mixins ?? [],
        RequiredJavaMajor = requiredJava
    };

    [Test]
    public async Task 空列表没有冲突() {
        await Assert.That(ModConflictAnalyzer.Analyze(null).Count).IsEqualTo(0);
        await Assert.That(ModConflictAnalyzer.Analyze([]).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 识别同名mixin配置() {
        var mods = new[] {
            Mod("a.jar", "a", "1.0", ["common.mixins.json"]),
            Mod("b.jar", "b", "1.0", ["common.mixins.json"])
        };
        var issues = ModConflictAnalyzer.Analyze(mods);
        await Assert.That(issues.Count).IsEqualTo(1);
        await Assert.That(issues[0].Message).Contains("同一个 mixin 配置");
        await Assert.That(issues[0].FileName).Contains("a.jar");
    }

    [Test]
    public async Task 不同的mixin配置不报冲突() {
        var mods = new[] {
            Mod("a.jar", "a", "1.0", ["a.mixins.json"]),
            Mod("b.jar", "b", "1.0", ["b.mixins.json"])
        };
        await Assert.That(ModConflictAnalyzer.Analyze(mods).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 核心库多版本报警() {
        var mods = new[] {
            Mod("fabric-api-1.jar", "fabric-api", "0.90.0"),
            Mod("fabric-api-2.jar", "fabric-api", "0.92.0")
        };
        var issues = ModConflictAnalyzer.Analyze(mods);
        await Assert.That(issues.Count).IsEqualTo(1);
        await Assert.That(issues[0].Message).Contains("多个版本");
    }

    [Test]
    public async Task 核心库同版本不报警() {
        var mods = new[] {
            Mod("fabric-api-1.jar", "fabric-api", "0.90.0"),
            Mod("fabric-api-copy.jar", "fabric-api", "0.90.0")
        };
        await Assert.That(ModConflictAnalyzer.Analyze(mods).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 非核心库多版本不报警() {
        var mods = new[] {
            Mod("sodium-1.jar", "sodium", "0.5.0"),
            Mod("sodium-2.jar", "sodium", "0.6.0")
        };
        await Assert.That(ModConflictAnalyzer.Analyze(mods).Count).IsEqualTo(0);
    }

    [Test]
    public async Task Java要求高于实例时报警() {
        var mods = new[] { Mod("a.jar", "a", "1.0", requiredJava: 21) };
        var issues = ModConflictAnalyzer.Analyze(mods, 17);
        await Assert.That(issues.Count).IsEqualTo(1);
        await Assert.That(issues[0].Severity).IsEqualTo(ModIssueSeverity.Error);
        await Assert.That(issues[0].Message).Contains("Java 21");
        await Assert.That(issues[0].Message).Contains("Java 17");
    }

    [Test]
    public async Task Java要求满足时不报警() {
        var mods = new[] { Mod("a.jar", "a", "1.0", requiredJava: 17) };
        await Assert.That(ModConflictAnalyzer.Analyze(mods, 17).Count).IsEqualTo(0);
        await Assert.That(ModConflictAnalyzer.Analyze(mods, 21).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 未提供实例Java时跳过该检查() {
        var mods = new[] { Mod("a.jar", "a", "1.0", requiredJava: 21) };
        await Assert.That(ModConflictAnalyzer.Analyze(mods, null).Count).IsEqualTo(0);
        await Assert.That(ModConflictAnalyzer.Analyze(mods, 0).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 禁用的Mod不参与检查() {
        var disabled = new ModInfo {
            FilePath = @"C:\mc\mods\a.jar.disabled",
            FileName = "a.jar.disabled",
            Enabled = false,
            ModId = "fabric-api",
            Version = "0.90.0",
            MixinConfigs = ["common.mixins.json"]
        };
        var mods = new[] { disabled, Mod("b.jar", "fabric-api", "0.92.0", ["common.mixins.json"]) };
        await Assert.That(ModConflictAnalyzer.Analyze(mods).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 解析Java版本声明() {
        await Assert.That(ModScanner.ParseJavaRequirement(">=17")).IsEqualTo(17);
        await Assert.That(ModScanner.ParseJavaRequirement("17")).IsEqualTo(17);
        await Assert.That(ModScanner.ParseJavaRequirement(">=21")).IsEqualTo(21);
        await Assert.That(ModScanner.ParseJavaRequirement(null)).IsNull();
        await Assert.That(ModScanner.ParseJavaRequirement("")).IsNull();
    }

}
