namespace PCL.Core.Net;

/// <summary>
/// 计算加权下载速度与速度下限。
/// </summary>
public sealed class DownloadSpeedTracker {

    private readonly List<double> _samples = new();
    private long _lastDone;

    public long Speed { get; private set; }
    public long Limit { get; private set; }

    public void Record(long done, long deltaMilliseconds) {
        if (deltaMilliseconds <= 0) return;
        var actual = Math.Max(0, (done - _lastDone) / (deltaMilliseconds / 1000d));
        _samples.Insert(0, actual);
        if (_samples.Count >= 31) _samples.RemoveAt(30);
        _lastDone = done;

        var sum = 0d;
        var divisor = 0;
        var weight = _samples.Count;
        foreach (var sample in _samples) {
            sum += sample * weight;
            divisor += weight;
            weight--;
        }
        Speed = divisor > 0 ? (long)(sum / divisor) : 0;
        Limit = _samples.Count >= 10 ? (long)(_samples.Take(10).Average() * 0.85) : 0;
    }

}
