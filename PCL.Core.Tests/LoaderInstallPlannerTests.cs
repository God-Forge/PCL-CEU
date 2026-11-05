using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LoaderInstallPlannerTests {

    [Test]
    public async Task 空目标没有步骤() {
        var plan = LoaderInstallPlanner.Plan(null);
        await Assert.That(plan.HasSteps).IsFalse();
        await Assert.That(plan.IsValid).IsTrue();
        await Assert.That(LoaderInstallPlanner.Plan(new InstallTarget()).HasSteps).IsFalse();
    }

    [Test]
    public async Task 单个Fabric计划() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget { MinecraftVersion = "26.3", FabricVersion = "0.15.0" });
        await Assert.That(plan.Steps.Count).IsEqualTo(1);
        await Assert.That(plan.Steps[0].Kind).IsEqualTo(LoaderKind.Fabric);
        await Assert.That(LoaderInstallPlanner.CanInstallAutomatically(plan.Steps[0])).IsTrue();
    }

    [Test]
    public async Task Fabric与Forge冲突() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget {
            MinecraftVersion = "26.3", FabricVersion = "0.15.0", ForgeVersion = "47.2.0"
        });
        await Assert.That(plan.IsValid).IsFalse();
        await Assert.That(plan.Error!).Contains("Fabric 与 Forge");
    }

    [Test]
    public async Task Forge与NeoForge冲突() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget {
            MinecraftVersion = "26.3", ForgeVersion = "47.2.0", NeoForgeVersion = "20.4.1"
        });
        await Assert.That(plan.IsValid).IsFalse();
        await Assert.That(plan.Error!).Contains("Forge 与 NeoForge");
    }

    [Test]
    public async Task OptiFine排在最后() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget {
            MinecraftVersion = "26.3", FabricVersion = "0.15.0", OptiFineVersion = "HD_U_I6"
        });
        await Assert.That(plan.IsValid).IsTrue();
        await Assert.That(plan.Steps.Count).IsEqualTo(2);
        await Assert.That(plan.Steps[^1].Kind).IsEqualTo(LoaderKind.OptiFine);
    }

    [Test]
    public async Task Forge不能自动安装() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget { ForgeVersion = "47.2.0" });
        await Assert.That(LoaderInstallPlanner.CanInstallAutomatically(plan.Steps[0])).IsFalse();
    }

    [Test]
    public async Task 步骤按顺序编号() {
        var plan = LoaderInstallPlanner.Plan(new InstallTarget {
            FabricVersion = "0.15.0", LiteLoaderVersion = "1.12.2", OptiFineVersion = "HD_U_I6"
        });
        await Assert.That(plan.IsValid).IsTrue();
        await Assert.That(plan.Steps.Select(step => step.Order).SequenceEqual(plan.Steps.Select(step => step.Order).OrderBy(order => order))).IsTrue();
    }

}
