using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class RecentChangeLogTests {

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), "pcl-recent-" + Guid.NewGuid().ToString("N"), "changes.json");

    private static RecentChange Change(DateTime time, RecentChangeKind kind, string target, string detail = "") =>
        new() { Time = time, Kind = kind, Target = target, Detail = detail };

    [Test]
    public async Task 空文件返回空列表() {
        var path = TempFile();
        await Assert.That(RecentChangeLog.Load(path).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 记录后可读回() {
        var path = TempFile();
        try {
            var now = new DateTime(2026, 6, 1, 12, 0, 0);
            RecentChangeLog.Record(path, Change(now, RecentChangeKind.ModInstall, "Sodium 0.5.0"));
            var loaded = RecentChangeLog.Load(path);
            await Assert.That(loaded.Count).IsEqualTo(1);
            await Assert.That(loaded[0].Target).IsEqualTo("Sodium 0.5.0");
            await Assert.That(loaded[0].Kind).IsEqualTo(RecentChangeKind.ModInstall);
        } finally {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task 超过上限时丢弃最旧() {
        var path = TempFile();
        try {
            for (var i = 0; i < RecentChangeLog.MaxEntries + 5; i++) {
                RecentChangeLog.Record(path, Change(DateTime.Now.AddMinutes(i), RecentChangeKind.Other, "t" + i));
            }
            var loaded = RecentChangeLog.Load(path);
            await Assert.That(loaded.Count).IsEqualTo(RecentChangeLog.MaxEntries);
            await Assert.That(loaded[^1].Target).IsEqualTo("t" + (RecentChangeLog.MaxEntries + 4));
        } finally {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task 按时间窗口筛选() {
        var now = new DateTime(2026, 6, 1, 12, 0, 0);
        var changes = new[] {
            Change(now.AddHours(-5), RecentChangeKind.ModInstall, "旧改动"),
            Change(now.AddMinutes(-30), RecentChangeKind.ModInstall, "新改动"),
            Change(now.AddMinutes(-10), RecentChangeKind.SettingChange, "改内存")
        };
        var recent = RecentChangeLog.Within(changes, TimeSpan.FromHours(2), now);
        await Assert.That(recent.Count).IsEqualTo(2);
        await Assert.That(recent[0].Target).IsEqualTo("新改动");
        await Assert.That(recent[1].Target).IsEqualTo("改内存");
    }

    [Test]
    public async Task 空窗口返回空() {
        await Assert.That(RecentChangeLog.Within(null, TimeSpan.FromHours(1)).Count).IsEqualTo(0);
        await Assert.That(RecentChangeLog.Within([], TimeSpan.FromHours(1)).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 描述包含类型与对象() {
        var now = new DateTime(2026, 6, 1, 12, 30, 0);
        var text = RecentChangeLog.Describe([
            Change(now, RecentChangeKind.ModInstall, "Sodium 0.5.0", "拖拽导入"),
            Change(now.AddMinutes(3), RecentChangeKind.SettingChange, "内存", "4096 → 6144")
        ]);
        await Assert.That(text).Contains("12:30");
        await Assert.That(text).Contains("安装 Mod");
        await Assert.That(text).Contains("Sodium 0.5.0");
        await Assert.That(text).Contains("修改设置");
        await Assert.That(text).Contains("4096 → 6144");
    }

    [Test]
    public async Task 没有改动时描述为空() {
        await Assert.That(RecentChangeLog.Describe(null)).IsEqualTo("");
        await Assert.That(RecentChangeLog.Describe([])).IsEqualTo("");
    }

    [Test]
    public async Task 损坏文件返回空而不抛异常() {
        var path = TempFile();
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ 不是数组");
            await Assert.That(RecentChangeLog.Load(path).Count).IsEqualTo(0);
        } finally {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task 类型标签可读() {
        await Assert.That(RecentChangeLog.Label(RecentChangeKind.ModInstall)).IsEqualTo("安装 Mod");
        await Assert.That(RecentChangeLog.Label(RecentChangeKind.SettingChange)).IsEqualTo("修改设置");
    }

}
