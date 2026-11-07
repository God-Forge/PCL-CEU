using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class DependencyGraphLayoutTests {

    private static ResourceVersionFile File(string id, params string[] dependencies) => new() {
        Id = id,
        Kind = ResourceKind.Mod,
        FileName = id + ".jar",
        DownloadUrls = ["https://cdn/" + id + ".jar"],
        Dependencies = dependencies
    };

    private static ResourceDependencyPlan Plan(params (ResourceVersionFile File, string[] Edges)[] items) {
        var files = items.Select(item => item.File).ToList();
        var edges = items
            .SelectMany(item => item.Edges.Select(to => new DependencyEdge(item.File.Id, to)))
            .ToList();
        return new ResourceDependencyPlan { Files = files, Edges = edges };
    }

    [Test]
    public async Task 空计划返回空图() {
        var graph = DependencyGraphLayout.Build(null);
        await Assert.That(graph.Nodes.Count).IsEqualTo(0);
        var empty = DependencyGraphLayout.Build(new ResourceDependencyPlan());
        await Assert.That(empty.Nodes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 单节点图() {
        var plan = Plan((File("root"), []));
        var graph = DependencyGraphLayout.Build(plan);
        await Assert.That(graph.Nodes.Count).IsEqualTo(1);
        await Assert.That(graph.Nodes[0].IsRoot).IsTrue();
        await Assert.That(graph.Nodes[0].Depth).IsEqualTo(0);
        await Assert.That(graph.MaxDepth).IsEqualTo(0);
    }

    [Test]
    public async Task 一层依赖排在第1层() {
        var plan = Plan((File("root", "a"), ["a"]), (File("a"), []));
        var graph = DependencyGraphLayout.Build(plan);
        var root = graph.Find("root")!;
        var child = graph.Find("a")!;
        await Assert.That(root.Depth).IsEqualTo(0);
        await Assert.That(child.Depth).IsEqualTo(1);
        await Assert.That(child.X).IsGreaterThan(root.X);
    }

    [Test]
    public async Task 多层依赖逐层展开() {
        var plan = Plan(
            (File("root", "a"), ["a"]),
            (File("a", "b"), ["b"]),
            (File("b"), []));
        var graph = DependencyGraphLayout.Build(plan);
        await Assert.That(graph.Find("root")!.Depth).IsEqualTo(0);
        await Assert.That(graph.Find("a")!.Depth).IsEqualTo(1);
        await Assert.That(graph.Find("b")!.Depth).IsEqualTo(2);
        await Assert.That(graph.MaxDepth).IsEqualTo(2);
    }

    [Test]
    public async Task 同层节点纵向均分() {
        var plan = Plan(
            (File("root", "a", "b"), ["a", "b"]),
            (File("a"), []),
            (File("b"), []));
        var graph = DependencyGraphLayout.Build(plan);
        var a = graph.Find("a")!;
        var b = graph.Find("b")!;
        await Assert.That(a.Depth).IsEqualTo(b.Depth);
        await Assert.That(a.Y).IsNotEqualTo(b.Y);
    }

    [Test]
    public async Task 坐标落在画布内() {
        var plan = Plan(
            (File("root", "a", "b", "c"), ["a", "b", "c"]),
            (File("a"), []), (File("b"), []), (File("c"), []));
        var graph = DependencyGraphLayout.Build(plan, 800, 400);
        foreach (var node in graph.Nodes) {
            await Assert.That(node.X).IsGreaterThanOrEqualTo(0);
            await Assert.That(node.X).IsLessThanOrEqualTo(800);
            await Assert.That(node.Y).IsGreaterThanOrEqualTo(0);
            await Assert.That(node.Y).IsLessThanOrEqualTo(400);
        }
    }

    [Test]
    public async Task 未连边的节点也有坐标() {
        var plan = Plan((File("root"), []), (File("orphan"), []));
        var graph = DependencyGraphLayout.Build(plan);
        await Assert.That(graph.Find("orphan")).IsNotNull();
        await Assert.That(graph.Find("orphan")!.Depth).IsGreaterThan(0);
    }

    [Test]
    public async Task 边能被查找到() {
        var plan = Plan((File("root", "a"), ["a"]), (File("a"), []));
        var graph = DependencyGraphLayout.Build(plan);
        await Assert.That(graph.Edges.Count).IsEqualTo(1);
        await Assert.That(graph.Find(graph.Edges[0].FromId)).IsNotNull();
        await Assert.That(graph.Find(graph.Edges[0].ToId)).IsNotNull();
    }

}
