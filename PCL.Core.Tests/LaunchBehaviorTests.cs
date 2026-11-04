using PCL.Core.Java;
using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class LaunchBehaviorTests {

    [Test]
    public async Task 启动器可见性映射() {
        await Assert.That(LauncherVisibilityResolver.Resolve(0)).IsEqualTo(LauncherVisibility.CloseAfterLaunch);
        await Assert.That(LauncherVisibilityResolver.Resolve(2)).IsEqualTo(LauncherVisibility.HideAfterLaunch);
        await Assert.That(LauncherVisibilityResolver.Resolve(3)).IsEqualTo(LauncherVisibility.HideAfterLaunch);
        await Assert.That(LauncherVisibilityResolver.Resolve(4)).IsEqualTo(LauncherVisibility.MinimizeAfterLaunch);
        await Assert.That(LauncherVisibilityResolver.Resolve(5)).IsEqualTo(LauncherVisibility.KeepVisible);
    }

    [Test]
    public async Task 未知可见性设置保持可见() {
        await Assert.That(LauncherVisibilityResolver.Resolve(-1)).IsEqualTo(LauncherVisibility.KeepVisible);
        await Assert.That(LauncherVisibilityResolver.Resolve(99)).IsEqualTo(LauncherVisibility.KeepVisible);
    }

    [Test]
    public async Task 隐藏与最小化需要在退出后恢复() {
        await Assert.That(LauncherVisibilityResolver.ShouldRestoreAfterExit(LauncherVisibility.HideAfterLaunch)).IsTrue();
        await Assert.That(LauncherVisibilityResolver.ShouldRestoreAfterExit(LauncherVisibility.MinimizeAfterLaunch)).IsTrue();
        await Assert.That(LauncherVisibilityResolver.ShouldRestoreAfterExit(LauncherVisibility.CloseAfterLaunch)).IsFalse();
        await Assert.That(LauncherVisibilityResolver.ShouldRestoreAfterExit(LauncherVisibility.KeepVisible)).IsFalse();
    }

    [Test]
    public async Task Java选择模式映射() {
        await Assert.That(JavaSelectionModeResolver.Resolve(0)).IsEqualTo(JavaSelectionMode.Automatic);
        await Assert.That(JavaSelectionModeResolver.Resolve(1)).IsEqualTo(JavaSelectionMode.Automatic);
        await Assert.That(JavaSelectionModeResolver.Resolve(2)).IsEqualTo(JavaSelectionMode.VersionFolder);
        await Assert.That(JavaSelectionModeResolver.Resolve(3)).IsEqualTo(JavaSelectionMode.Forced);
        await Assert.That(JavaSelectionModeResolver.Resolve(-1)).IsEqualTo(JavaSelectionMode.Automatic);
    }

    [Test]
    public async Task 只有版本文件夹模式需要扫描版本目录() {
        await Assert.That(JavaSelectionModeResolver.RequiresVersionFolderScan(JavaSelectionMode.VersionFolder)).IsTrue();
        await Assert.That(JavaSelectionModeResolver.RequiresVersionFolderScan(JavaSelectionMode.Automatic)).IsFalse();
        await Assert.That(JavaSelectionModeResolver.RequiresVersionFolderScan(JavaSelectionMode.Forced)).IsFalse();
    }

}
