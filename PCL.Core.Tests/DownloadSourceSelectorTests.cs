using PCL.Core.Download;

namespace PCL.Core.Tests;

public class DownloadSourceSelectorTests {

    private static DownloadSpeedResult Result(string name, bool success, long elapsed) =>
        new() { Name = name, Url = "https://" + name + "/", Success = success, ElapsedMilliseconds = elapsed };

    [Test]
    public async Task 可用源按耗时升序排在前() {
        var ranked = DownloadSourceSelector.Rank([
            Result("mirror", true, 300),
            Result("official", true, 120),
            Result("dead", false, 5000)
        ]);
        await Assert.That(ranked[0].Name).IsEqualTo("official");
        await Assert.That(ranked[1].Name).IsEqualTo("mirror");
        await Assert.That(ranked[2].Name).IsEqualTo("dead");
    }

    [Test]
    public async Task 选最优源() {
        var best = DownloadSourceSelector.PickBest([
            Result("mirror", true, 300),
            Result("official", true, 120)
        ]);
        await Assert.That(best).IsNotNull();
        await Assert.That(best!.Name).IsEqualTo("official");
    }

    [Test]
    public async Task 没有可用源时返回空() {
        await Assert.That(DownloadSourceSelector.PickBest([Result("a", false, 1)])).IsNull();
        await Assert.That(DownloadSourceSelector.PickBest([])).IsNull();
        await Assert.That(DownloadSourceSelector.PickBest(null)).IsNull();
    }

    [Test]
    public async Task 尝试顺序默认原地址优先() {
        var order = DownloadSourceSelector.BuildAttemptOrder("https://official/x.jar", "https://mirror/x.jar", false);
        await Assert.That(order[0]).IsEqualTo("https://official/x.jar");
        await Assert.That(order[1]).IsEqualTo("https://mirror/x.jar");
    }

    [Test]
    public async Task 偏好镜像时镜像优先() {
        var order = DownloadSourceSelector.BuildAttemptOrder("https://official/x.jar", "https://mirror/x.jar", true);
        await Assert.That(order[0]).IsEqualTo("https://mirror/x.jar");
        await Assert.That(order[1]).IsEqualTo("https://official/x.jar");
    }

    [Test]
    public async Task 缺少镜像时只尝试原地址() {
        var order = DownloadSourceSelector.BuildAttemptOrder("https://official/x.jar", null, true);
        await Assert.That(order.Count).IsEqualTo(1);
        await Assert.That(order[0]).IsEqualTo("https://official/x.jar");
    }

    [Test]
    public async Task 重复地址会去重() {
        var order = DownloadSourceSelector.BuildAttemptOrder("https://a/x.jar", "https://a/x.jar", false);
        await Assert.That(order.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 偏好源不可用时不优先镜像() {
        var results = new[] { Result("mirror", false, 9999), Result("official", true, 100) };
        await Assert.That(DownloadSourceSelector.ShouldPreferMirror("mirror", results)).IsFalse();
        await Assert.That(DownloadSourceSelector.ShouldPreferMirror("official", results)).IsTrue();
        await Assert.That(DownloadSourceSelector.ShouldPreferMirror(null, results)).IsFalse();
        await Assert.That(DownloadSourceSelector.ShouldPreferMirror("unknown", results)).IsFalse();
    }

}
