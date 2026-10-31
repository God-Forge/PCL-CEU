namespace PCL.Core.Net;

/// <summary>
/// 同时发起多个请求，采用错峰启动并返回第一个成功结果。
/// </summary>
public static class MultiRequestCoordinator {

    public static async Task<string> FetchFirstAsync(
        IReadOnlyList<Func<CancellationToken, Task<string>>> requests,
        int delayStepMilliseconds = 250,
        CancellationToken cancellationToken = default) {
        if (requests.Count == 0) throw new ArgumentException("至少需要一个请求。", nameof(requests));

        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tasks = requests.Select(async (request, index) => {
            if (index > 0) await Task.Delay(index * Math.Max(0, delayStepMilliseconds), source.Token).ConfigureAwait(false);
            source.Token.ThrowIfCancellationRequested();
            return await request(source.Token).ConfigureAwait(false);
        }).ToList();

        Exception? lastError = null;
        try {
            while (tasks.Count > 0) {
                var completed = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(completed);
                try {
                    var result = await completed.ConfigureAwait(false);
                    source.Cancel();
                    return result;
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    throw;
                } catch (Exception ex) {
                    lastError = ex;
                }
            }
        } finally {
            source.Cancel();
        }

        throw lastError ?? new InvalidOperationException("所有请求均失败。");
    }

}
