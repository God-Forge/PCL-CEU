namespace PCL.Core.Download;

public enum DownloadQueueState {
    Waiting,
    Running,
    Completed,
    Failed,
    Canceled
}

public sealed class DownloadQueueItem {

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public int Priority { get; set; }
    public DownloadQueueState State { get; set; } = DownloadQueueState.Waiting;

}

/// <summary>
/// 下载队列控制：暂停、继续、并发限制与优先级。
/// </summary>
public sealed class DownloadQueueService {

    private readonly List<DownloadQueueItem> _items = new();
    private readonly object _gate = new();

    public bool Paused { get; private set; }
    public int MaxConcurrency { get; private set; } = 3;

    public IReadOnlyList<DownloadQueueItem> Items {
        get { lock (_gate) return _items.ToList(); }
    }

    public void Add(DownloadQueueItem item) {
        lock (_gate) _items.Add(item);
    }

    public void Pause() {
        lock (_gate) Paused = true;
    }

    public void Resume() {
        lock (_gate) Paused = false;
    }

    public void SetMaxConcurrency(int value) {
        lock (_gate) MaxConcurrency = Math.Max(1, value);
    }

    public void Prioritize(string id, int priority) {
        lock (_gate) {
            var item = _items.FirstOrDefault(value => value.Id == id);
            if (item is not null) item.Priority = priority;
        }
    }

    public DownloadQueueItem? Next() {
        lock (_gate) {
            if (Paused) return null;
            if (_items.Count(item => item.State == DownloadQueueState.Running) >= MaxConcurrency) return null;
            var next = _items.Where(item => item.State == DownloadQueueState.Waiting)
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (next is not null) next.State = DownloadQueueState.Running;
            return next;
        }
    }

    public void Complete(string id, DownloadQueueState state) {
        lock (_gate) {
            var item = _items.FirstOrDefault(value => value.Id == id);
            if (item is not null) item.State = state;
        }
    }

}
