using PCL.Core.Download;

namespace PCL.Core.Tests;

public class DownloadQueueServiceTests {

    [Test]
    public async Task 暂停与恢复队列() {
        var queue = new DownloadQueueService();
        queue.Add(new DownloadQueueItem { Name = "a", Url = "https://example.com/a" });
        queue.Pause();
        await Assert.That(queue.Next()).IsNull();
        queue.Resume();
        await Assert.That(queue.Next()).IsNotNull();
    }

    [Test]
    public async Task 按优先级选择任务() {
        var queue = new DownloadQueueService();
        var low = new DownloadQueueItem { Name = "low", Url = "https://example.com/a" };
        var high = new DownloadQueueItem { Name = "high", Url = "https://example.com/b" };
        queue.Add(low);
        queue.Add(high);
        queue.Prioritize(high.Id, 10);
        await Assert.That(queue.Next()!.Name).IsEqualTo("high");
    }

    [Test]
    public async Task 遵守并发限制() {
        var queue = new DownloadQueueService();
        queue.SetMaxConcurrency(1);
        queue.Add(new DownloadQueueItem { Name = "a", Url = "https://example.com/a" });
        queue.Add(new DownloadQueueItem { Name = "b", Url = "https://example.com/b" });
        await Assert.That(queue.Next()).IsNotNull();
        await Assert.That(queue.Next()).IsNull();
    }

}
