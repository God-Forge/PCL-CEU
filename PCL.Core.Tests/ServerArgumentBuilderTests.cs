using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ServerArgumentBuilderTests {

    [Test]
    public async Task 新版本使用QuickPlay() {
        var result = ServerArgumentBuilder.Build("mc.example.com", true);
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0]).IsEqualTo("--quickPlayMultiplayer");
        await Assert.That(result[1]).IsEqualTo("mc.example.com");
    }

    [Test]
    public async Task 老版本拆分主机与端口() {
        var result = ServerArgumentBuilder.Build("mc.example.com:25566", false);
        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result[0]).IsEqualTo("--server");
        await Assert.That(result[1]).IsEqualTo("mc.example.com");
        await Assert.That(result[2]).IsEqualTo("--port");
        await Assert.That(result[3]).IsEqualTo("25566");
    }

    [Test]
    public async Task 未指定端口时使用25565() {
        var result = ServerArgumentBuilder.Build("mc.example.com", false);
        await Assert.That(result.Count).IsEqualTo(4);
        await Assert.That(result[3]).IsEqualTo("25565");
    }

    [Test]
    public async Task 空服务器返回空参数() {
        await Assert.That(ServerArgumentBuilder.Build("", false).Count).IsEqualTo(0);
    }

}
