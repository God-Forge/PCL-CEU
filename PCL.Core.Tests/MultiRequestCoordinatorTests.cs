using System.Net.Http;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class MultiRequestCoordinatorTests {

    [Test]
    public async Task 返回第一个成功结果() {
        var requests = new List<Func<CancellationToken, Task<string>>> {
            _ => Task.FromResult("first"),
            _ => Task.Delay(100).ContinueWith(_ => "second"),
            _ => Task.Delay(200).ContinueWith(_ => "third")
        };
        var result = await MultiRequestCoordinator.FetchFirstAsync(requests, 10);
        await Assert.That(result).IsEqualTo("first");
    }

    [Test]
    public async Task 全部失败时抛出最后一个异常() {
        var requests = new List<Func<CancellationToken, Task<string>>> {
            _ => Task.FromException<string>(new HttpRequestException("one")),
            _ => Task.FromException<string>(new HttpRequestException("two"))
        };
        await Assert.ThrowsAsync<HttpRequestException>(() => MultiRequestCoordinator.FetchFirstAsync(requests, 1));
    }

    [Test]
    public async Task 请求列表为空时抛出参数异常() {
        await Assert.ThrowsAsync<ArgumentException>(() => MultiRequestCoordinator.FetchFirstAsync([]));
    }

}
