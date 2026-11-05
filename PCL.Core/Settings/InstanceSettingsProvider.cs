using System.Collections.Concurrent;

namespace PCL.Core.Settings;

/// <summary>
/// 按实例目录缓存 <see cref="InstanceSettings"/>，避免每次读取都重建对象。
/// 对应 VB 版 Setting.ValueCache 按版本路径缓存的行为。
/// </summary>
public static class InstanceSettingsProvider {

    private static readonly ConcurrentDictionary<string, InstanceSettings> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>已缓存的实例数。</summary>
    public static int Count => Cache.Count;

    /// <summary>取指定实例的设置对象。</summary>
    public static InstanceSettings For(string instanceFolder) {
        var normalized = InstanceSettings.NormalizeFolder(instanceFolder);
        return Cache.GetOrAdd(normalized, path => new InstanceSettings(path));
    }

    /// <summary>丢弃缓存（实例被删除或重命名后调用）。</summary>
    public static void Invalidate(string instanceFolder) {
        var normalized = InstanceSettings.NormalizeFolder(instanceFolder);
        Cache.TryRemove(normalized, out _);
    }

    public static void Clear() => Cache.Clear();

}
