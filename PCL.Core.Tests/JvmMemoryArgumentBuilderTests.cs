using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JvmMemoryArgumentBuilderTests {

    [Test]
    public async Task Java17默认使用ZGC并移除旧GC参数() {
        var args = new List<string> { "-XX:+UseG1GC", "-XX:MaxDirectMemorySize=256M" };
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 4096, 0, 17, new Version(10, 0, 19045), false);
        await Assert.That(useG1).IsFalse();
        await Assert.That(args.Contains("-Xmx4096m")).IsTrue();
        await Assert.That(args.Contains("-XX:+UseZGC")).IsTrue();
        await Assert.That(args.Contains("-XX:+UseG1GC")).IsFalse();
        await Assert.That(args.Contains("-XX:MaxDirectMemorySize=256M")).IsFalse();
        await Assert.That(args.Contains("-XX:+UnlockExperimentalVMOptions")).IsTrue();
    }

    [Test]
    public async Task Java21设置一使用分代ZGC() {
        var args = new List<string>();
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 2048, 1, 21, new Version(10, 0, 19045), false);
        await Assert.That(useG1).IsFalse();
        await Assert.That(args.Contains("-XX:+UseZGC")).IsTrue();
        await Assert.That(args.Contains("-XX:+ZGenerational")).IsTrue();
    }

    [Test]
    public async Task Java17设置一使用G1GC() {
        var args = new List<string>();
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 2048, 1, 17, new Version(10, 0, 19045), false);
        await Assert.That(useG1).IsTrue();
        await Assert.That(args.Contains("-XX:+UseG1GC")).IsTrue();
    }

    [Test]
    public async Task 优化G1GC添加额外参数() {
        var args = new List<string>();
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 4096, 4, 8, new Version(10, 0, 19045), false);
        await Assert.That(useG1).IsTrue();
        await Assert.That(args.Contains("-XX:+PerfDisableSharedMem")).IsTrue();
        await Assert.That(args.Contains("-XX:MaxPermSize=512m")).IsFalse();
        await Assert.That(args.Contains("-XX:+ParallelRefProcEnabled")).IsTrue();
        await Assert.That(args.Contains("-XX:MinHeapFreeRatio=25")).IsFalse();
    }

    [Test]
    public async Task 旧系统强制G1GC() {
        var args = new List<string>();
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 2048, 0, 17, new Version(6, 1, 7601), false);
        await Assert.That(useG1).IsTrue();
        await Assert.That(args.Contains("-XX:+UseG1GC")).IsTrue();
    }

    [Test]
    public async Task 自定义GC只添加内存参数() {
        var args = new List<string> { "-XX:+UseZGC" };
        var useG1 = JvmMemoryArgumentBuilder.Apply(args, 2048, 3, 17, new Version(10, 0, 19045), false);
        await Assert.That(useG1).IsFalse();
        await Assert.That(args.Count).IsEqualTo(2);
        await Assert.That(args.Contains("-XX:+UseZGC")).IsTrue();
    }

}
