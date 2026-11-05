using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstallTargetComparerTests {

    private static InstallTarget Initial() => new() {
        MinecraftVersion = "26.3",
        ForgeVersion = "47.2.0"
    };

    [Test]
    public async Task 相同目标没有变更() {
        var comparisons = InstallTargetComparer.Compare(Initial(), Initial());
        await Assert.That(comparisons.All(item => !item.Changed)).IsTrue();
        await Assert.That(InstallTargetComparer.HasChanges(Initial(), Initial())).IsFalse();
    }

    [Test]
    public async Task 检测组件变更() {
        var current = Initial().With(InstallTargetComponent.Forge, "47.3.0");
        var comparisons = InstallTargetComparer.Compare(Initial(), current);
        var forge = comparisons.First(item => item.Component == InstallTargetComponent.Forge);
        await Assert.That(forge.Changed).IsTrue();
        await Assert.That(forge.CanRestore).IsTrue();
        await Assert.That(InstallTargetComparer.HasChanges(Initial(), current)).IsTrue();
    }

    [Test]
    public async Task 恢复单个组件() {
        var current = Initial().With(InstallTargetComponent.Forge, "47.3.0");
        var restored = InstallTargetComparer.Restore(current, Initial(), InstallTargetComponent.Forge);
        await Assert.That(restored.ForgeVersion).IsEqualTo("47.2.0");
        await Assert.That(InstallTargetComparer.HasChanges(Initial(), restored)).IsFalse();
    }

    [Test]
    public async Task 恢复全部变更() {
        var current = Initial()
            .With(InstallTargetComponent.Forge, "48.0.0")
            .With(InstallTargetComponent.Minecraft, "1.21.1");
        var restored = InstallTargetComparer.RestoreAll(current, Initial());
        await Assert.That(restored.MinecraftVersion).IsEqualTo("26.3");
        await Assert.That(restored.ForgeVersion).IsEqualTo("47.2.0");
    }

    [Test]
    public async Task 初始为空时不可恢复() {
        var current = Initial().With(InstallTargetComponent.OptiFine, "HD_U_I6");
        var comparisons = InstallTargetComparer.Compare(Initial(), current);
        var optifine = comparisons.First(item => item.Component == InstallTargetComponent.OptiFine);
        await Assert.That(optifine.Changed).IsTrue();
        await Assert.That(optifine.CanRestore).IsFalse();
    }

    [Test]
    public async Task 清空组件也算变更() {
        var current = Initial().With(InstallTargetComponent.Forge, null);
        var comparisons = InstallTargetComparer.Compare(Initial(), current);
        var forge = comparisons.First(item => item.Component == InstallTargetComponent.Forge);
        await Assert.That(forge.Changed).IsTrue();
        await Assert.That(forge.Current).IsNull();
    }

    [Test]
    public async Task 描述一行目标() {
        await Assert.That(InstallTargetComparer.Describe(Initial())).IsEqualTo("26.3 + Forge 47.2.0");
        var multi = Initial().With(InstallTargetComponent.OptiFine, "HD_U_I6");
        await Assert.That(InstallTargetComparer.Describe(multi)).Contains("OptiFine");
    }

    [Test]
    public async Task 空目标描述() {
        await Assert.That(InstallTargetComparer.Describe(new InstallTarget())).IsEqualTo("未选择版本");
    }

    [Test]
    public async Task 组件中文名() {
        await Assert.That(InstallTargetComparer.Describe(InstallTargetComponent.Minecraft)).IsEqualTo("原版");
        await Assert.That(InstallTargetComparer.Describe(InstallTargetComponent.NeoForge)).IsEqualTo("NeoForge");
    }

}
