using PCL.Core.Net;
using PCL.Core.Resources;

namespace PCL.Core.Tests;

public class ResourceSearchTests {

    private const string ModrinthPayload = """
    {
      "hits": [
        {"project_id":"AABB","slug":"sodium","title":"Sodium","description":"渲染优化","author":"jellysquid3",
         "downloads":120000,"follows":900,"icon_url":"https://cdn.example/icon.png",
         "categories":["optimization"],"loaders":["fabric"],"versions":["1.20.1"]},
        {"project_id":"CCDD","slug":"iris","title":"Iris Shaders","description":"光影加载器","author":"coderbot",
         "downloads":80000,"follows":400,"categories":["shaders"],"loaders":["fabric"],"versions":["1.20.1"]}
      ],
      "total_hits": 2
    }
    """;

    private const string CurseForgePayload = """
    {
      "data": [
        {"id":238222,"name":"Just Enough Items","slug":"jei",
         "links":{"websiteUrl":"https://www.curseforge.com/minecraft/mc-mods/jei"},
         "summary":"查看物品配方","downloadCount":500000,"thumbsUpCount":1200,
         "logo":{"url":"https://edge.example/jei.png"},
         "authors":[{"name":"mezz"}],
         "categories":[{"name":"API and Library"}],
         "latestFilesIndexes":[{"gameVersion":"1.20.1","modLoader":4},{"gameVersion":"1.20.1","modLoader":1}]}
      ],
      "pagination":{"index":0,"pageSize":20,"resultCount":1,"totalCount":1}
    }
    """;

    private sealed class RecordingFetcher(string response) : IStringFetcher {
        public List<string> Urls { get; } = [];
        public IReadOnlyDictionary<string, string>? LastHeaders { get; private set; }

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            Urls.Add(url);
            return Task.FromResult(response);
        }

        public Task<string> GetStringAsync(string url, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) {
            LastHeaders = headers;
            return GetStringAsync(url, cancellationToken);
        }
    }

    private sealed class FakeProvider(ResourceSource source, ResourceSearchResult? result, Exception? failure = null) : IResourceSearchProvider {
        public ResourceSource Source => source;

        public Task<ResourceSearchResult> SearchAsync(ResourceSearchQuery query, CancellationToken cancellationToken = default) {
            if (failure is not null) return Task.FromException<ResourceSearchResult>(failure);
            return Task.FromResult(result ?? new ResourceSearchResult());
        }
    }

    private static ResourceProject Project(ResourceSource source, string id, string title, long downloads) => new() {
        Source = source,
        Id = id,
        Slug = id,
        Title = title,
        Downloads = downloads
    };

    [Test]
    public async Task Modrinth解析为统一模型() {
        var service = new ModrinthResourceService(new RecordingFetcher(ModrinthPayload));
        var result = await service.SearchAsync(new ResourceSearchQuery { Text = "sodium", Kind = ResourceKind.Mod });
        await Assert.That(result.Projects.Count).IsEqualTo(2);
        await Assert.That(result.TotalHits).IsEqualTo(2);
        var sodium = result.Projects[0];
        await Assert.That(sodium.Title).IsEqualTo("Sodium");
        await Assert.That(sodium.Source).IsEqualTo(ResourceSource.Modrinth);
        await Assert.That(sodium.Author).IsEqualTo("jellysquid3");
        await Assert.That(sodium.Downloads).IsEqualTo(120000);
        await Assert.That(sodium.Loaders).Contains("fabric");
        await Assert.That(sodium.ProjectUrl).IsEqualTo("https://modrinth.com/mod/sodium");
    }

    [Test]
    public async Task Modrinth搜索地址包含类型与版本过滤() {
        var url = ModrinthResourceService.BuildSearchUrl(new ResourceSearchQuery {
            Text = "sodium",
            Kind = ResourceKind.Modpack,
            MinecraftVersion = "1.20.1",
            Loader = "fabric",
            Offset = 20,
            Limit = 30
        });
        var decoded = Uri.UnescapeDataString(url);
        await Assert.That(decoded).Contains("project_type:modpack");
        await Assert.That(decoded).Contains("versions:1.20.1");
        await Assert.That(decoded).Contains("categories:fabric");
        await Assert.That(url).Contains("offset=20");
        await Assert.That(url).Contains("limit=30");
    }

    [Test]
    public async Task 资源类型映射到Modrinth与CurseForge标识() {
        await Assert.That(ModrinthResourceService.ToProjectType(ResourceKind.ShaderPack)).IsEqualTo("shader");
        await Assert.That(ModrinthResourceService.ToProjectType(ResourceKind.DataPack)).IsEqualTo("datapack");
        await Assert.That(CurseForgeResourceService.ToClassId(ResourceKind.Modpack)).IsEqualTo(4471);
        await Assert.That(CurseForgeResourceService.ToClassId(ResourceKind.ResourcePack)).IsEqualTo(12);
        await Assert.That(CurseForgeResourceService.ToModLoaderType("NeoForge")).IsEqualTo(6);
        await Assert.That(CurseForgeResourceService.ToModLoaderType("unknown")).IsNull();
    }

    [Test]
    public async Task CurseForge解析为统一模型并携带APIKey() {
        var fetcher = new RecordingFetcher(CurseForgePayload);
        var service = new CurseForgeResourceService("test-key", fetcher);
        var result = await service.SearchAsync(new ResourceSearchQuery { Text = "jei", Kind = ResourceKind.Mod });
        await Assert.That(result.Projects.Count).IsEqualTo(1);
        var jei = result.Projects[0];
        await Assert.That(jei.Title).IsEqualTo("Just Enough Items");
        await Assert.That(jei.Author).IsEqualTo("mezz");
        await Assert.That(jei.Downloads).IsEqualTo(500000);
        await Assert.That(jei.Loaders).Contains("fabric");
        await Assert.That(jei.Loaders).Contains("forge");
        await Assert.That(jei.ProjectUrl).IsEqualTo("https://www.curseforge.com/minecraft/mc-mods/jei");
        await Assert.That(fetcher.LastHeaders!["x-api-key"]).IsEqualTo("test-key");
    }

    [Test]
    public async Task CurseForge未配置Key时抛出明确异常() {
        var service = new CurseForgeResourceService(null, new RecordingFetcher("{}"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchAsync(new ResourceSearchQuery()));
    }

    [Test]
    public async Task 聚合多个来源并按下载量排序() {
        var modrinth = new FakeProvider(ResourceSource.Modrinth, new ResourceSearchResult {
            Projects = [Project(ResourceSource.Modrinth, "a", "Alpha", 100), Project(ResourceSource.Modrinth, "b", "Beta", 900)],
            TotalHits = 2
        });
        var curseforge = new FakeProvider(ResourceSource.CurseForge, new ResourceSearchResult {
            Projects = [Project(ResourceSource.CurseForge, "c", "Gamma", 500)],
            TotalHits = 1
        });
        var search = new ResourceSearchService([modrinth, curseforge]);
        var result = await search.SearchAsync(new ResourceSearchQuery { Text = "", Limit = 10 });
        await Assert.That(result.Projects.Count).IsEqualTo(3);
        await Assert.That(result.TotalHits).IsEqualTo(3);
        await Assert.That(result.Projects[0].Title).IsEqualTo("Beta");
        await Assert.That(result.Projects[1].Title).IsEqualTo("Gamma");
    }

    [Test]
    public async Task 标题命中查询词的项目排在前面() {
        var provider = new FakeProvider(ResourceSource.Modrinth, new ResourceSearchResult {
            Projects = [Project(ResourceSource.Modrinth, "x", "Other Pack", 9999), Project(ResourceSource.Modrinth, "y", "Cool Pack", 1)]
        });
        var search = new ResourceSearchService([provider]);
        var result = await search.SearchAsync(new ResourceSearchQuery { Text = "cool" });
        await Assert.That(result.Projects[0].Title).IsEqualTo("Cool Pack");
    }

    [Test]
    public async Task 单个来源失败不影响整体结果() {
        var ok = new FakeProvider(ResourceSource.Modrinth, new ResourceSearchResult {
            Projects = [Project(ResourceSource.Modrinth, "a", "Alpha", 10)],
            TotalHits = 1
        });
        var failing = new FakeProvider(ResourceSource.CurseForge, null, new InvalidOperationException("未配置 CurseForge API Key"));
        var search = new ResourceSearchService([ok, failing]);
        var result = await search.SearchAsync(new ResourceSearchQuery());
        await Assert.That(result.Projects.Count).IsEqualTo(1);
        await Assert.That(result.HasFailure).IsTrue();
        await Assert.That(result.Failures[0]).Contains("CurseForge");
    }

    [Test]
    public async Task 按来源过滤只请求指定来源() {
        var modrinth = new FakeProvider(ResourceSource.Modrinth, new ResourceSearchResult { Projects = [Project(ResourceSource.Modrinth, "a", "Alpha", 1)] });
        var curseforge = new FakeProvider(ResourceSource.CurseForge, new ResourceSearchResult { Projects = [Project(ResourceSource.CurseForge, "b", "Beta", 2)] });
        var search = new ResourceSearchService([modrinth, curseforge]);
        var result = await search.SearchAsync(new ResourceSearchQuery { Sources = [ResourceSource.CurseForge] });
        await Assert.That(result.Projects.Count).IsEqualTo(1);
        await Assert.That(result.Projects[0].Source).IsEqualTo(ResourceSource.CurseForge);
    }

    [Test]
    public async Task 合并时按来源与ID去重() {
        var duplicate = Project(ResourceSource.Modrinth, "same", "Alpha", 10);
        var merged = ResourceSearchService.Merge([duplicate, duplicate, Project(ResourceSource.CurseForge, "same", "Alpha", 20)], null);
        await Assert.That(merged.Count).IsEqualTo(2);
    }

}
