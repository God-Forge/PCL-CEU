using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceVersionTests {

    private const string ModrinthVersions = """
    [
      {"id":"ver1","project_id":"ABC","name":"Sodium 1.2.3","version_number":"1.2.3",
       "game_versions":["1.20.1"],"loaders":["fabric"],"release_type":"beta",
       "date_published":"2024-05-01T10:00:00Z","downloads":5000,
       "files":[
         {"url":"https://cdn.modrinth.com/sources.jar","filename":"sources.jar","primary":false,"size":1},
         {"url":"https://cdn.modrinth.com/sodium.jar","filename":"sodium-1.2.3.jar","primary":true,"size":2048,
          "hashes":{"sha1":"abcdef1234"}}
       ],
       "dependencies":[{"project_id":"DEP1","dependency_type":"required"},
                       {"project_id":"DEP2","dependency_type":"optional"}]}
    ]
    """;

    private const string CurseForgeFiles = """
    {
      "data": [
        {"id":9001,"displayName":"jei-1.20.1-15.2","fileName":"jei-1.20.1-15.2.jar",
         "releaseType":2,"fileDate":"2024-06-01T00:00:00Z","fileLength":123456,"downloadCount":42,
         "downloadUrl":"https://edge.forgecdn.net/files/9001/jei.jar",
         "gameVersions":["1.20.1","Fabric"],
         "hashes":[{"value":"DEADBEEF","algo":1}],
         "dependencies":[{"modId":306612,"relationType":3},{"modId":238222,"relationType":2}]}
      ]
    }
    """;

    private sealed class FakeFetcher(string response) : IStringFetcher {
        public string? LastUrl { get; private set; }
        public IReadOnlyDictionary<string, string>? LastHeaders { get; private set; }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            LastUrl = url;
            return Task.FromResult(response);
        }

        public Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) {
            LastHeaders = headers;
            return GetStringAsync(url, cancellationToken);
        }
    }

    [Test]
    public async Task Modrinth版本解析优先取primary文件() {
        var service = new ModrinthResourceService(new FakeFetcher(ModrinthVersions));
        var versions = await service.GetVersionsAsync("sodium", ResourceKind.Mod);
        await Assert.That(versions.Count).IsEqualTo(1);
        var version = versions[0];
        await Assert.That(version.FileName).IsEqualTo("sodium-1.2.3.jar");
        await Assert.That(version.PrimaryUrl).IsEqualTo("https://cdn.modrinth.com/sodium.jar");
        await Assert.That(version.Hash).IsEqualTo("abcdef1234");
        await Assert.That(version.Size).IsEqualTo(2048);
        await Assert.That(version.Version).IsEqualTo("1.2.3");
        await Assert.That(version.Display).IsEqualTo("Sodium 1.2.3");
        await Assert.That(version.ReleaseType).IsEqualTo(ResourceReleaseType.Beta);
        await Assert.That(version.ReleaseTypeDisplay).IsEqualTo("测试版");
        await Assert.That(version.DownloadAvailable).IsTrue();
    }

    [Test]
    public async Task Modrinth只保留必需依赖() {
        var service = new ModrinthResourceService(new FakeFetcher(ModrinthVersions));
        var versions = await service.GetVersionsAsync("sodium", ResourceKind.Mod);
        await Assert.That(versions[0].Dependencies.Count).IsEqualTo(1);
        await Assert.That(versions[0].Dependencies[0]).IsEqualTo("DEP1");
    }

    [Test]
    public async Task Modrinth版本地址包含版本与加载器过滤() {
        var url = ModrinthResourceService.BuildVersionsUrl("sodium", "1.20.1", "fabric");
        var decoded = Uri.UnescapeDataString(url);
        await Assert.That(decoded).StartsWith("https://api.modrinth.com/v2/project/sodium/version?");
        await Assert.That(decoded).Contains("game_versions=");
        await Assert.That(decoded).Contains("1.20.1");
        await Assert.That(decoded).Contains("loaders=");
        await Assert.That(decoded).Contains("fabric");
    }

    [Test]
    public async Task CurseForge文件解析拆分游戏版本与加载器() {
        var service = new CurseForgeResourceService("key", new FakeFetcher(CurseForgeFiles));
        var files = await service.GetFilesAsync("238222", ResourceKind.Mod);
        await Assert.That(files.Count).IsEqualTo(1);
        var file = files[0];
        await Assert.That(file.GameVersions.Count).IsEqualTo(1);
        await Assert.That(file.GameVersions[0]).IsEqualTo("1.20.1");
        await Assert.That(file.Loaders.Count).IsEqualTo(1);
        await Assert.That(file.Loaders[0]).IsEqualTo("fabric");
        await Assert.That(file.FileName).IsEqualTo("jei-1.20.1-15.2.jar");
        await Assert.That(file.PrimaryUrl).IsEqualTo("https://edge.forgecdn.net/files/9001/jei.jar");
        await Assert.That(file.Size).IsEqualTo(123456);
        await Assert.That(file.ReleaseType).IsEqualTo(ResourceReleaseType.Beta);
        await Assert.That(file.DownloadAvailable).IsTrue();
    }

    [Test]
    public async Task CurseForge只保留必需依赖并携带APIKey() {
        var fetcher = new FakeFetcher(CurseForgeFiles);
        var service = new CurseForgeResourceService("key", fetcher);
        var files = await service.GetFilesAsync("238222", ResourceKind.Mod);
        await Assert.That(files[0].Dependencies.Count).IsEqualTo(1);
        await Assert.That(files[0].Dependencies[0]).IsEqualTo("306612");
        await Assert.That(fetcher.LastHeaders!["x-api-key"]).IsEqualTo("key");
    }

    [Test]
    public async Task CurseForge文件地址包含版本与加载器参数() {
        var url = CurseForgeResourceService.BuildFilesUrl("238222", "1.20.1", "fabric");
        await Assert.That(url).StartsWith("https://api.curseforge.com/v1/mods/238222/files?");
        await Assert.That(url).Contains("gameVersion=1.20.1");
        await Assert.That(url).Contains("modLoaderType=4");
    }

    [Test]
    public async Task 缺少下载信息时标记为不可下载() {
        var json = Newtonsoft.Json.Linq.JObject.Parse("""
        {"id":1,"name":"v","files":[],"dependencies":[]}
        """);
        var version = ResourceVersionParser.ParseModrinth(json, ResourceKind.Mod);
        await Assert.That(version.DownloadAvailable).IsFalse();
        await Assert.That(version.PrimaryUrl).IsNull();
    }

    [Test]
    public async Task 发布类型映射() {
        var json = Newtonsoft.Json.Linq.JObject.Parse("""
        {"id":1,"name":"v","release_type":"alpha","files":[],"dependencies":[]}
        """);
        var version = ResourceVersionParser.ParseModrinth(json, ResourceKind.Mod);
        await Assert.That(version.ReleaseType).IsEqualTo(ResourceReleaseType.Alpha);
        await Assert.That(version.ReleaseTypeDisplay).IsEqualTo("早期测试版");
    }

}
