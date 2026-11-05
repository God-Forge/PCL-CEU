using PCL.Core.Mods;
using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ModUpdateCheckerTests {

    private const string SearchJson = """
    {"hits":[{"project_id":"AABB","slug":"sodium","title":"Sodium","description":"渲染优化",
      "author":"jellysquid3","downloads":1000,"project_type":"mod"}],"total_hits":1}
    """;

    private const string VersionsJson = """
    [{"id":"v1","project_id":"AABB","name":"Sodium 1.3.0","version_number":"1.3.0",
      "game_versions":["1.20.1"],"loaders":["fabric"],"release_type":"release",
      "date_published":"2024-05-01T10:00:00Z",
      "files":[{"url":"https://cdn.example/sodium.jar","filename":"sodium-1.3.0.jar","primary":true,"size":1}],
      "dependencies":[]}]
    """;

    private const string EmptySearchJson = """{"hits":[],"total_hits":0}""";

    private sealed class RoutingFetcher(string searchJson) : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            if (url.Contains("/search", StringComparison.Ordinal)) return Task.FromResult(searchJson);
            if (url.Contains("/version", StringComparison.Ordinal)) return Task.FromResult(VersionsJson);
            return Task.FromResult("{}");
        }
    }

    private sealed class FailingFetcher : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("network down");
    }

    private static ModInfo Mod(string modId, string version) => new() {
        FilePath = @"C:\mc\mods\" + modId + ".jar",
        FileName = modId + ".jar",
        ModId = modId,
        Name = modId,
        Version = version,
        Loader = "fabric"
    };

    [Test]
    public async Task 发现新版本时标记有更新() {
        var service = new ModrinthResourceService(new RoutingFetcher(SearchJson));
        var results = await ModUpdateChecker.CheckAsync([Mod("sodium", "1.2.0")], service, "1.20.1", "fabric");
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].HasUpdate).IsTrue();
        await Assert.That(results[0].LatestVersion).IsEqualTo("1.3.0");
        await Assert.That(results[0].ProjectSlug).IsEqualTo("sodium");
        await Assert.That(results[0].Error).IsNull();
    }

    [Test]
    public async Task 版本相同时不算更新() {
        var service = new ModrinthResourceService(new RoutingFetcher(SearchJson));
        var results = await ModUpdateChecker.CheckAsync([Mod("sodium", "1.3.0")], service, "1.20.1", "fabric");
        await Assert.That(results[0].HasUpdate).IsFalse();
    }

    [Test]
    public async Task 搜索不到项目时给出原因() {
        var service = new ModrinthResourceService(new RoutingFetcher(EmptySearchJson));
        var results = await ModUpdateChecker.CheckAsync([Mod("unknown", "1.0")], service);
        await Assert.That(results[0].HasUpdate).IsFalse();
        await Assert.That(results[0].Error).Contains("未在 Modrinth 找到");
    }

    [Test]
    public async Task 缺少ModId时不发起请求() {
        var service = new ModrinthResourceService(new RoutingFetcher(SearchJson));
        var results = await ModUpdateChecker.CheckAsync([Mod("", "1.0")], service);
        await Assert.That(results[0].Error).Contains("缺少 Mod ID");
        await Assert.That(results[0].HasUpdate).IsFalse();
    }

    [Test]
    public async Task 网络异常时返回错误而不中断整批() {
        var service = new ModrinthResourceService(new FailingFetcher());
        var results = await ModUpdateChecker.CheckAsync([Mod("a", "1.0"), Mod("b", "2.0")], service);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results.All(result => result.Error is not null)).IsTrue();
    }

    [Test]
    public async Task 没有发布版本的资源不算更新() {
        var fetcher = new RoutingFetcher(SearchJson);
        var service = new ModrinthResourceService(fetcher);
        // 版本列表用空数组模拟「没有兼容版本」
        var emptyService = new ModrinthResourceService(new EmptyVersionFetcher());
        var results = await ModUpdateChecker.CheckAsync([Mod("sodium", "1.0")], emptyService, "1.19.2", "forge");
        await Assert.That(results[0].HasUpdate).IsFalse();
        await Assert.That(results[0].LatestVersion).IsNull();
    }

    private sealed class EmptyVersionFetcher : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(url.Contains("/search", StringComparison.Ordinal) ? SearchJson : "[]");
    }

}
