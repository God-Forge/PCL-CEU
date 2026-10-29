using PCL.Core.Java;
using PCLCS;

namespace PCL.Core.Tests;

public class JavaRequirementTests {

    private static JavaRequirementContext CreateContext(
        Version vanilla,
        DateTime releaseTime,
        bool valid = true,
        bool optifine = false,
        ModLoaders loaders = ModLoaders.None,
        Dictionary<ModLoaders, string>? loaderVersions = null,
        bool nide = false,
        bool hasOverride = false,
        string? overrideRange = null,
        int? mojangJava = null,
        string? mojangComponent = null) {
        return new JavaRequirementContext {
            VersionDisplayName = vanilla.ToString(),
            VersionValid = valid,
            VanillaVersion = vanilla,
            HasOptiFine = optifine,
            ModLoaderFlags = loaders,
            ModLoaderVersions = loaderVersions ?? new Dictionary<ModLoaders, string>(),
            ReleaseTime = releaseTime,
            IsNideLogin = nide,
            HasJavaVersionOverride = hasOverride,
            JavaVersionOverride = overrideRange,
            MojangJavaMajor = mojangJava,
            MojangJavaComponent = mojangComponent
        };
    }

    [Test]
    public async Task 现代版本要求Java21() {
        var result = JavaRequirementResolver.Resolve(CreateContext(new Version(20, 0, 5), new DateTime(2024, 4, 2)));
        await Assert.That(result.Range.Contains(new Version(21, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(17, 0))).IsFalse();
    }

    [Test]
    public async Task 一点一八要求Java17() {
        var result = JavaRequirementResolver.Resolve(CreateContext(new Version(18, 0, 0), new DateTime(2021, 11, 30)));
        await Assert.That(result.Range.Contains(new Version(17, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(16, 0))).IsFalse();
    }

    [Test]
    public async Task 一点一二要求Java8() {
        var result = JavaRequirementResolver.Resolve(CreateContext(new Version(12, 0, 2), new DateTime(2017, 6, 1)));
        await Assert.That(result.Range.Contains(new Version(8, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(7, 0))).IsFalse();
    }

    [Test]
    public async Task 旧版本最高Java8() {
        var result = JavaRequirementResolver.Resolve(CreateContext(new Version(5, 2, 0), new DateTime(2013, 5, 1)));
        await Assert.That(result.Range.Contains(new Version(8, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(9, 0))).IsFalse();
    }

    [Test]
    public async Task OptiFine一点八限制Java8() {
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(8, 0, 0), new DateTime(2014, 1, 1), optifine: true));
        await Assert.That(result.Range.Contains(new Version(8, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(9, 0))).IsFalse();
    }

    [Test]
    public async Task Forge一点六点一要求Java7() {
        var loaders = new Dictionary<ModLoaders, string> {
            [ModLoaders.Forge] = "1.6.1-1.6.1"
        };
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(6, 0, 1), new DateTime(2013, 6, 1), loaders: ModLoaders.Forge, loaderVersions: loaders));
        await Assert.That(result.Range.Contains(new Version(7, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(8, 0))).IsFalse();
    }

    [Test]
    public async Task Fabric一点一八要求Java17且旧Loader限制Java25() {
        var loaders = new Dictionary<ModLoaders, string> {
            [ModLoaders.Fabric] = "0.16.0"
        };
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(18, 0, 0), new DateTime(2021, 12, 1), loaders: ModLoaders.Fabric, loaderVersions: loaders));
        await Assert.That(result.Range.Contains(new Version(17, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(24, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(25, 0))).IsFalse();
    }

    [Test]
    public async Task NeoForge一点二十点一最高Java21() {
        var loaders = new Dictionary<ModLoaders, string> {
            [ModLoaders.NeoForge] = "20.2.62-beta"
        };
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(20, 0, 1), new DateTime(2023, 6, 1), loaders: ModLoaders.NeoForge, loaderVersions: loaders));
        await Assert.That(result.Range.Contains(new Version(21, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(22, 0))).IsFalse();
    }

    [Test]
    public async Task 统一通行证至少Java8u141() {
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(8, 0, 0), new DateTime(2014, 1, 1), nide: true));
        await Assert.That(result.Range.Contains(new Version(8, 0, 141))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(8, 0, 140))).IsFalse();
    }

    [Test]
    public async Task 手动覆盖直接返回指定范围() {
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(20, 0, 5), new DateTime(2024, 4, 2), hasOverride: true, overrideRange: "[17, 21]"));
        await Assert.That(result.Range.Contains(new Version(17, 0, 0, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(21, 0, 0, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(22, 0, 0, 0))).IsFalse();
        await Assert.That(result.RecommendedComponent).IsNull();
    }

    [Test]
    public async Task MojangJava版本与组件被保留() {
        var result = JavaRequirementResolver.Resolve(
            CreateContext(new Version(26, 0, 0), new DateTime(2026, 1, 1), mojangJava: 25, mojangComponent: "java-runtime-delta"));
        await Assert.That(result.Range.Contains(new Version(25, 0))).IsTrue();
        await Assert.That(result.Range.Contains(new Version(24, 0))).IsFalse();
        await Assert.That(result.RecommendedComponent).IsEqualTo("java-runtime-delta");
    }

}
