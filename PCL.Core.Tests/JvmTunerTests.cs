using PCL.Core.Minecraft;
using PCL.Core.Settings;

namespace PCL.Core.Tests;

public class JvmTunerTests {

    [Test]
    public async Task 普通机器推荐一半内存() {
        // 16 GB 物理内存 → 8 GB，但上限是 8 GB
        await Assert.That(JvmTuner.RecommendRamMb(16384)).IsEqualTo(8192);
        // 8 GB → 4 GB
        await Assert.That(JvmTuner.RecommendRamMb(8192)).IsEqualTo(4096);
    }

    [Test]
    public async Task 内存建议有下限与上限() {
        await Assert.That(JvmTuner.RecommendRamMb(1024)).IsGreaterThanOrEqualTo(JvmTuner.MinRamMb);
        await Assert.That(JvmTuner.RecommendRamMb(65536)).IsLessThanOrEqualTo(JvmTuner.MaxRamMb);
    }

    [Test]
    public async Task 给系统留出余量() {
        // 6 GB 内存：一半是 3 GB，但要留 2 GB 给系统，所以取 4 GB 以内
        var ram = JvmTuner.RecommendRamMb(6144);
        await Assert.That(ram).IsLessThanOrEqualTo(6144 - JvmTuner.ReserveMb);
    }

    [Test]
    public async Task 极端小内存不超出总量() {
        var ram = JvmTuner.RecommendRamMb(1500);
        await Assert.That(ram).IsLessThanOrEqualTo(1500);
    }

    [Test]
    public async Task 多核大内存用高性能GC() {
        var (option, name) = JvmTuner.RecommendGc(16, 8192, 17);
        await Assert.That(option).IsEqualTo(5);
        await Assert.That(name).Contains("G1 增强");
    }

    [Test]
    public async Task 新Java大内存交给默认() {
        var (option, _) = JvmTuner.RecommendGc(8, 8192, 21);
        await Assert.That(option).IsEqualTo(4);
    }

    [Test]
    public async Task 普通配置使用标准G1() {
        var (option, name) = JvmTuner.RecommendGc(4, 4096, 17);
        await Assert.That(option).IsEqualTo(3);
        await Assert.That(name).IsEqualTo("G1");
    }

    [Test]
    public async Task 推荐结果包含可读结论() {
        var recommendation = JvmTuner.Recommend(8, 16384, 21);
        await Assert.That(recommendation.RamMb).IsGreaterThan(0);
        await Assert.That(recommendation.Summary).Contains("8 核");
        await Assert.That(recommendation.Summary).Contains("Java 21");
    }

    [Test]
    public async Task 转换成实例设置键() {
        var recommendation = new JvmRecommendation { RamMb = 6144, GcOption = 3, GcName = "G1" };
        var settings = JvmTuner.ToInstanceSettings(recommendation);
        await Assert.That(settings[InstanceSettingKeys.RamType]).IsEqualTo(2);
        await Assert.That(settings[InstanceSettingKeys.RamCustom]).IsEqualTo(6144);
        await Assert.That(settings[InstanceSettingKeys.Gc]).IsEqualTo(3);
    }

}
