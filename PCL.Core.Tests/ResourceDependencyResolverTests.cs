using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceDependencyResolverTests {

    private sealed class FakeSource(Dictionary<string, IReadOnlyList<ResourceVersionFile>> map) : IResourceVersionSource {
        public List<string> Requested { get; } = [];

        public Task<IReadOnlyList<ResourceVersionFile>> GetVersionsAsync(
            string projectId, ResourceKind kind, string? minecraftVersion = null, string? loader = null,
            CancellationToken cancellationToken = default) {
            Requested.Add(projectId);
            return Task.FromResult(map.TryGetValue(projectId, out var versions)
                ? versions
                : (IReadOnlyList<ResourceVersionFile>)[]);
        }
    }

    private static ResourceVersionFile Version(string id, string fileName, params string[] dependencies) => new() {
        Id = id,
        Kind = ResourceKind.Mod,
        FileName = fileName,
        DownloadUrls = ["https://cdn.example/" + fileName],
        GameVersions = ["1.20.1"],
        Loaders = ["fabric"],
        ReleaseDate = DateTimeOffset.Parse("2024-01-01"),
        Dependencies = dependencies
    };

    [Test]
    public async Task 无依赖时只返回根资源() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>>());
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(1);
        await Assert.That(plan.Files[0].FileName).IsEqualTo("root.jar");
        await Assert.That(plan.IsComplete).IsTrue();
        await Assert.That(source.Requested.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 解析一层依赖() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar")]
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(2);
        await Assert.That(plan.Files.Any(file => file.FileName == "dep1.jar")).IsTrue();
        await Assert.That(plan.IsComplete).IsTrue();
    }

    [Test]
    public async Task 递归解析多层依赖() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar", "dep2")],
            ["dep2"] = [Version("dep2", "dep2.jar")]
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(3);
        await Assert.That(plan.Files.Select(file => file.FileName))
            .IsEquivalentTo(new[] { "root.jar", "dep1.jar", "dep2.jar" });
    }

    [Test]
    public async Task 循环依赖不会无限递归() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar", "root")],
            ["root"] = [Version("root", "root.jar", "dep1")]
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(2);
        await Assert.That(plan.Skipped).Contains("root");
    }

    [Test]
    public async Task 同一依赖只解析一次() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar")],
            ["dep2"] = [Version("dep2", "dep2.jar", "dep1")]
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1", "dep2"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(3);
        await Assert.That(source.Requested.Count(id => id == "dep1")).IsEqualTo(1);
    }

    [Test]
    public async Task 依赖没有可用版本时记为未解析() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = []
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1"), source);
        await Assert.That(plan.IsComplete).IsFalse();
        await Assert.That(plan.Unresolved).Contains("dep1");
    }

    [Test]
    public async Task 超出深度限制时跳过() {
        var source = new FakeSource(new Dictionary<string, IReadOnlyList<ResourceVersionFile>> {
            ["dep1"] = [Version("dep1", "dep1.jar", "dep2")],
            ["dep2"] = [Version("dep2", "dep2.jar")]
        });
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1"), source, maxDepth: 1);
        await Assert.That(plan.Files.Count).IsEqualTo(2);
        await Assert.That(plan.Skipped).Contains("dep2");
    }

    [Test]
    public async Task 依赖获取异常时记为未解析且不中断() {
        var source = new ThrowingSource();
        var plan = await ResourceDependencyResolver.ResolveAsync(Version("root", "root.jar", "dep1", "dep2"), source);
        await Assert.That(plan.Files.Count).IsEqualTo(1);
        await Assert.That(plan.Unresolved.Count).IsEqualTo(2);
    }

    private sealed class ThrowingSource : IResourceVersionSource {
        public Task<IReadOnlyList<ResourceVersionFile>> GetVersionsAsync(
            string projectId, ResourceKind kind, string? minecraftVersion = null, string? loader = null,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("network down");
    }

}
