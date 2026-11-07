using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class ModUpdateSnapshotStoreTests {

    private static string TempMods() {
        var folder = Path.Combine(Path.GetTempPath(), "pcl-snap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>构造一次更新后的现场：旧文件备份在 .backup，新文件已在 mods 下。</summary>
    private static ModUpdateSnapshotEntry CreateUpdated(string modsFolder, string modId, string oldName, string newName) {
        var backupFolder = Path.Combine(modsFolder, ModUpdateApplier.BackupFolderName);
        Directory.CreateDirectory(backupFolder);
        var backup = Path.Combine(backupFolder, oldName + ".20260101-000000.bak");
        File.WriteAllText(backup, "old-" + modId);
        File.WriteAllText(Path.Combine(modsFolder, newName), "new-" + modId);
        return new ModUpdateSnapshotEntry {
            ModId = modId,
            OldFileName = oldName,
            NewFileName = newName,
            BackupPath = backup
        };
    }

    [Test]
    public async Task 没有快照时回滚失败() {
        var mods = TempMods();
        try {
            await Assert.That(ModUpdateSnapshotStore.List(mods).Count).IsEqualTo(0);
            var result = ModUpdateSnapshotStore.Rollback(mods);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("没有可回滚");
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 记录并列出快照() {
        var mods = TempMods();
        try {
            var entry = CreateUpdated(mods, "sodium", "sodium-1.0.jar", "sodium-2.0.jar");
            var snapshot = ModUpdateSnapshotStore.Record(mods, [entry]);
            await Assert.That(snapshot).IsNotNull();
            var list = ModUpdateSnapshotStore.List(mods);
            await Assert.That(list.Count).IsEqualTo(1);
            await Assert.That(list[0].Entries.Count).IsEqualTo(1);
            await Assert.That(list[0].Entries[0].OldFileName).IsEqualTo("sodium-1.0.jar");
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 备份不存在的条目不写入() {
        var mods = TempMods();
        try {
            var snapshot = ModUpdateSnapshotStore.Record(mods, [
                new ModUpdateSnapshotEntry {
                    ModId = "x", OldFileName = "x.jar", NewFileName = "y.jar",
                    BackupPath = Path.Combine(mods, "missing.bak")
                }
            ]);
            await Assert.That(snapshot).IsNull();
            await Assert.That(ModUpdateSnapshotStore.List(mods).Count).IsEqualTo(0);
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 回滚恢复旧文件并删除新文件() {
        var mods = TempMods();
        try {
            var entry = CreateUpdated(mods, "sodium", "sodium-1.0.jar", "sodium-2.0.jar");
            ModUpdateSnapshotStore.Record(mods, [entry]);
            var result = ModUpdateSnapshotStore.Rollback(mods);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Restored).IsEqualTo(1);
            await Assert.That(File.Exists(Path.Combine(mods, "sodium-2.0.jar"))).IsFalse();
            await Assert.That(File.ReadAllText(Path.Combine(mods, "sodium-1.0.jar"))).IsEqualTo("old-sodium");
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 回滚多个文件() {
        var mods = TempMods();
        try {
            var entries = new[] {
                CreateUpdated(mods, "a", "a-1.jar", "a-2.jar"),
                CreateUpdated(mods, "b", "b-1.jar", "b-2.jar")
            };
            ModUpdateSnapshotStore.Record(mods, entries);
            var result = ModUpdateSnapshotStore.Rollback(mods);
            await Assert.That(result.Restored).IsEqualTo(2);
            await Assert.That(File.Exists(Path.Combine(mods, "a-1.jar"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mods, "b-1.jar"))).IsTrue();
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 回滚后快照被移除() {
        var mods = TempMods();
        try {
            var entry = CreateUpdated(mods, "a", "a-1.jar", "a-2.jar");
            ModUpdateSnapshotStore.Record(mods, [entry]);
            ModUpdateSnapshotStore.Rollback(mods);
            await Assert.That(ModUpdateSnapshotStore.List(mods).Count).IsEqualTo(0);
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 快照数量超过上限时丢弃最旧() {
        var mods = TempMods();
        try {
            for (var i = 0; i < ModUpdateSnapshotStore.MaxSnapshots + 3; i++) {
                var entry = CreateUpdated(mods, "m" + i, $"m{i}-1.jar", $"m{i}-2.jar");
                ModUpdateSnapshotStore.Record(mods, [entry]);
                await Task.Delay(2);
            }
            await Assert.That(ModUpdateSnapshotStore.List(mods).Count)
                .IsLessThanOrEqualTo(ModUpdateSnapshotStore.MaxSnapshots);
        } finally {
            Directory.Delete(mods, true);
        }
    }

    [Test]
    public async Task 损坏的索引返回空而不抛异常() {
        var mods = TempMods();
        try {
            var backupFolder = Path.Combine(mods, ModUpdateApplier.BackupFolderName);
            Directory.CreateDirectory(backupFolder);
            File.WriteAllText(Path.Combine(backupFolder, "snapshots.json"), "{ 不是数组");
            await Assert.That(ModUpdateSnapshotStore.List(mods).Count).IsEqualTo(0);
        } finally {
            Directory.Delete(mods, true);
        }
    }

}
