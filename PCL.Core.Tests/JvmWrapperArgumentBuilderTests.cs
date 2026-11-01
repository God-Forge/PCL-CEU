using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JvmWrapperArgumentBuilderTests {

    [Test]
    public async Task 构建Java9的JLW参数() {
        var arguments = JvmWrapperArgumentBuilder.BuildJLW(true, 17);
        await Assert.That(arguments.Count).IsEqualTo(3);
        await Assert.That(arguments[0]).IsEqualTo("--add-exports");
        await Assert.That(arguments[1]).IsEqualTo("cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
        await Assert.That(arguments[2]).IsEqualTo("-Doolloo.jlw.tmpdir=${pure_directory}");
    }

    [Test]
    public async Task 禁用JLW时返回空参数() {
        await Assert.That(JvmWrapperArgumentBuilder.BuildJLW(false, 17).Count).IsEqualTo(0);
    }

    [Test]
    public async Task 构建LUA参数() {
        await Assert.That(JvmWrapperArgumentBuilder.BuildLUA(@"C:\pcl\LwjglUnsafeAgent.jar"))
            .IsEqualTo("-javaagent:\"C:\\pcl\\LwjglUnsafeAgent.jar\"");
    }

}
