using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashFixUndoStoreTests {

    private static string TempRoot() => Path.Combine(Path.GetTempPath(), "pcl-undo-" + Guid.NewGuid().ToString("N"));

    [Test]
    public async Task 保存后可读回() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            var path = CrashFixUndoStore.Save(@"C:\mc\versions\26.3", "-Xmx4G", root);
            await Assert.That(File.Exists(path)).IsTrue();
            var record = CrashFixUndoStore.Load(root);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Value.InstancePath).IsEqualTo(@"C:\mc\versions\26.3");
            await Assert.That(record.Value.PreviousValue).IsEqualTo("-Xmx4G");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 没有记录时返回空() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            await Assert.That(CrashFixUndoStore.Load(root)).IsNull();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 清除后读不到() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            CrashFixUndoStore.Save(@"C:\mc", "", root);
            CrashFixUndoStore.Clear(root);
            await Assert.That(CrashFixUndoStore.Load(root)).IsNull();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 损坏内容返回空而不抛异常() {
        var root = TempRoot();
        try {
            var folder = Path.Combine(root, "PCL");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "CrashFixUndo.json"), "{ 不是 JSON");
            await Assert.That(CrashFixUndoStore.Load(root)).IsNull();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 重复保存覆盖旧记录() {
        var root = TempRoot();
        try {
            Directory.CreateDirectory(root);
            CrashFixUndoStore.Save(@"C:\a", "-Xmx1G", root);
            CrashFixUndoStore.Save(@"C:\b", "-Xmx2G", root);
            var record = CrashFixUndoStore.Load(root);
            await Assert.That(record!.Value.InstancePath).IsEqualTo(@"C:\b");
            await Assert.That(record.Value.PreviousValue).IsEqualTo("-Xmx2G");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
