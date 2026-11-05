using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceCategoryFilterTests {

    [Test]
    public async Task 同时解析ID与slug() {
        var filter = ResourceCategoryFilter.Parse("406/worldgen");
        await Assert.That(filter.CurseForgeId).IsEqualTo(406);
        await Assert.That(filter.ModrinthSlug).IsEqualTo("worldgen");
        await Assert.That(filter.IsEmpty).IsFalse();
    }

    [Test]
    public async Task 只有slug时也有效() {
        var filter = ResourceCategoryFilter.Parse("/game-mechanics");
        await Assert.That(filter.CurseForgeId).IsNull();
        await Assert.That(filter.ModrinthSlug).IsEqualTo("game-mechanics");
    }

    [Test]
    public async Task 只有ID时也有效() {
        var filter = ResourceCategoryFilter.Parse("4558/");
        await Assert.That(filter.CurseForgeId).IsEqualTo(4558);
        await Assert.That(filter.ModrinthSlug).IsNull();
    }

    [Test]
    public async Task 空或无法识别时为空筛选() {
        await Assert.That(ResourceCategoryFilter.Parse(null).IsEmpty).IsTrue();
        await Assert.That(ResourceCategoryFilter.Parse("").IsEmpty).IsTrue();
        await Assert.That(ResourceCategoryFilter.Parse("   ").IsEmpty).IsTrue();
    }

    [Test]
    public async Task 分类会进入Modrinth的facets() {
        var url = ModrinthResourceService.BuildSearchUrl(new ResourceSearchQuery {
            Text = "sodium",
            Kind = ResourceKind.Mod,
            Category = ResourceCategoryFilter.Parse("6814/optimization")
        });
        var decoded = Uri.UnescapeDataString(url);
        await Assert.That(decoded).Contains("categories:optimization");
    }

    [Test]
    public async Task 分类会进入CurseForge的查询参数() {
        var url = CurseForgeResourceService.BuildSearchUrl(new ResourceSearchQuery {
            Text = "jei",
            Kind = ResourceKind.Mod,
            Category = ResourceCategoryFilter.Parse("406/worldgen")
        });
        await Assert.That(url).Contains("categoryId=406");
    }

    [Test]
    public async Task 无分类时不添加筛选参数() {
        var modrinth = Uri.UnescapeDataString(ModrinthResourceService.BuildSearchUrl(new ResourceSearchQuery { Text = "a" }));
        await Assert.That(modrinth).DoesNotContain("categories:optimization");
        var curseForge = CurseForgeResourceService.BuildSearchUrl(new ResourceSearchQuery { Text = "a" });
        await Assert.That(curseForge).DoesNotContain("categoryId=");
    }

}
