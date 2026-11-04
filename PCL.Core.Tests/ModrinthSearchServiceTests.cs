using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ModrinthSearchServiceTests {

    private sealed class FakeFetcher(string response) : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) => Task.FromResult(response);
    }

    [Test]
    public async Task 解析搜索命中() {
        var service = new ModrinthSearchService(new FakeFetcher("""
        {"hits":[{"slug":"sodium","title":"Sodium","description":"Renderer","author":"jellysquid3","downloads":100,"project_type":"mod"}]}
        """));
        var result = await service.SearchAsync("sodium", "mod");
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Title).IsEqualTo("Sodium");
        await Assert.That(result[0].Downloads).IsEqualTo(100);
    }

}
