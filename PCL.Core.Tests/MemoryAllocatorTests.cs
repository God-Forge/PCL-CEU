using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class MemoryAllocatorTests {

    [Test]
    public async Task 手动滑杆分段映射() {
        await Assert.That(MemoryAllocator.FromCustomSlider(0)).IsEqualTo(0.3);
        await Assert.That(MemoryAllocator.FromCustomSlider(12)).IsEqualTo(1.5);
        await Assert.That(MemoryAllocator.FromCustomSlider(25)).IsEqualTo(8);
        await Assert.That(MemoryAllocator.FromCustomSlider(33)).IsEqualTo(16);
        await Assert.That(MemoryAllocator.FromCustomSlider(50)).IsEqualTo(50);
    }

    [Test]
    public async Task 滑杆值被钳制在范围内() {
        await Assert.That(MemoryAllocator.FromCustomSlider(-5)).IsEqualTo(0.3);
        await Assert.That(MemoryAllocator.FromCustomSlider(999))
            .IsEqualTo(MemoryAllocator.FromCustomSlider(MemoryAllocator.CustomSliderMaximum));
    }

    [Test]
    public async Task 目标内存随版本类型变化() {
        var vanilla = MemoryAllocator.EstimateTargets(0, modable: false, hasOptiFine: false);
        var optifine = MemoryAllocator.EstimateTargets(0, modable: false, hasOptiFine: true);
        var modded = MemoryAllocator.EstimateTargets(100, modable: true, hasOptiFine: false);

        await Assert.That(vanilla.Target2).IsEqualTo(2.5);
        await Assert.That(optifine.Target2).IsEqualTo(3);
        await Assert.That(modded.Target2).IsGreaterThan(vanilla.Target2);
    }

    [Test]
    public async Task 自动分配八G原版() {
        // 8 GB 可用、原版：分阶段填充后约 4.2 GB
        var ram = MemoryAllocator.CalculateAutoGigabytes(8, 0, modable: false, hasOptiFine: false);
        await Assert.That(ram).IsEqualTo(4.2);
    }

    [Test]
    public async Task 自动分配内存紧张时不低于最低值() {
        var ram = MemoryAllocator.CalculateAutoGigabytes(0.4, 0, modable: false, hasOptiFine: false);
        await Assert.That(ram).IsEqualTo(0.5);
    }

    [Test]
    public async Task 自动分配中等内存() {
        // 2 GB 可用：第一档 1.5，第二档再补 0.35，四舍五入到 0.1
        var ram = MemoryAllocator.CalculateAutoGigabytes(2, 0, modable: false, hasOptiFine: false);
        await Assert.That(ram).IsEqualTo(1.8);
    }

    [Test]
    public async Task 自动分配纯服务端或零可用内存() {
        await Assert.That(MemoryAllocator.CalculateAutoGigabytes(0)).IsEqualTo(0);
        await Assert.That(MemoryAllocator.CalculateAutoGigabytes(-1)).IsEqualTo(0);
    }

    [Test]
    public async Task Mod越多分配越多() {
        var few = MemoryAllocator.CalculateAutoGigabytes(8, 5, modable: true);
        var many = MemoryAllocator.CalculateAutoGigabytes(8, 100, modable: true);
        await Assert.That(many).IsGreaterThan(few);
    }

    [Test]
    public async Task 分配结果不会超出可用内存太多() {
        // 最低内存需求可能高于可用内存，这是 VB 版原本的行为
        var minimum = MemoryAllocator.EstimateTargets(150, modable: true, hasOptiFine: false).Minimum;
        foreach (var available in new[] { 1.0, 2.0, 4.0, 8.0, 16.0, 32.0 }) {
            var ram = MemoryAllocator.CalculateAutoGigabytes(available, 150, modable: true);
            await Assert.That(ram).IsLessThanOrEqualTo(Math.Max(available, minimum));
        }
    }

    [Test]
    public async Task GB转MB() {
        await Assert.That(MemoryAllocator.ToMegabytes(2)).IsEqualTo(2048);
        await Assert.That(MemoryAllocator.ToMegabytes(4.5)).IsEqualTo(4608);
        await Assert.That(MemoryAllocator.ToMegabytes(-1)).IsEqualTo(0);
    }

}
