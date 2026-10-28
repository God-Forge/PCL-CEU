using PCL.Core.Memory;

namespace PCL.Core.Tests;

public class MemoryOptimizerTests {

    [Test]
    public async Task Status_返回合理的物理内存数值() {
        var status = MemoryOptimizer.GetStatus();
        await Assert.That(status.TotalBytes > 0).IsTrue();
        await Assert.That(status.AvailableBytes <= status.TotalBytes).IsTrue();
        await Assert.That(status.AvailableMB > 0).IsTrue();
    }

    [Test]
    public async Task CurrentProcess_优化可正常完成() {
        var result = MemoryOptimizer.Optimize(MemoryOptimizationKind.CurrentProcess);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Before.TotalBytes).IsEqualTo(result.After.TotalBytes);
    }

}
