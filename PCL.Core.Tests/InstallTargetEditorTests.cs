using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstallTargetEditorTests {

    private static InstallTarget Initial() => new() {
        MinecraftVersion = "26.3",
        FabricVersion = "0.15.0"
    };

    [Test]
    public async Task 初始状态无变更() {
        var editor = new InstallTargetEditor(Initial());
        await Assert.That(editor.HasChanges).IsFalse();
        await Assert.That(editor.Restorable.Count).IsEqualTo(0);
        await Assert.That(editor.DescribeCurrent()).IsEqualTo("26.3 + Fabric 0.15.0");
    }

    [Test]
    public async Task 修改组件后出现在可恢复列表() {
        var editor = new InstallTargetEditor(Initial());
        editor.Set(InstallTargetComponent.Fabric, "0.16.0");
        await Assert.That(editor.HasChanges).IsTrue();
        await Assert.That(editor.Current.FabricVersion).IsEqualTo("0.16.0");
        await Assert.That(editor.Restorable.Count).IsEqualTo(1);
        await Assert.That(editor.Restorable[0].Component).IsEqualTo(InstallTargetComponent.Fabric);
    }

    [Test]
    public async Task 恢复单个组件() {
        var editor = new InstallTargetEditor(Initial());
        editor.Set(InstallTargetComponent.Fabric, "0.16.0");
        editor.Set(InstallTargetComponent.Minecraft, "1.21.1");
        editor.Restore(InstallTargetComponent.Fabric);
        await Assert.That(editor.Current.FabricVersion).IsEqualTo("0.15.0");
        await Assert.That(editor.Current.MinecraftVersion).IsEqualTo("1.21.1");
        await Assert.That(editor.Restorable.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 全部恢复() {
        var editor = new InstallTargetEditor(Initial());
        editor.Set(InstallTargetComponent.Fabric, "0.16.0");
        editor.Set(InstallTargetComponent.Minecraft, "1.21.1");
        editor.RestoreAll();
        await Assert.That(editor.HasChanges).IsFalse();
        await Assert.That(editor.DescribeCurrent()).IsEqualTo(editor.DescribeInitial());
    }

    [Test]
    public async Task 空值视为清空并能恢复() {
        var editor = new InstallTargetEditor(Initial());
        editor.Set(InstallTargetComponent.Fabric, "  ");
        await Assert.That(editor.Current.FabricVersion).IsNull();
        await Assert.That(editor.HasChanges).IsTrue();
        editor.Restore(InstallTargetComponent.Fabric);
        await Assert.That(editor.Current.FabricVersion).IsEqualTo("0.15.0");
    }

    [Test]
    public async Task 新增初始没有的组件不可恢复() {
        var editor = new InstallTargetEditor(Initial());
        editor.Set(InstallTargetComponent.OptiFine, "HD_U_I6");
        await Assert.That(editor.HasChanges).IsTrue();
        await Assert.That(editor.Restorable.Count).IsEqualTo(0);
        editor.RestoreAll();
        await Assert.That(editor.Current.OptiFineVersion).IsNull();
    }

    [Test]
    public async Task 初始目标为null时抛出() {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(new InstallTargetEditor(null!)));
    }

}
