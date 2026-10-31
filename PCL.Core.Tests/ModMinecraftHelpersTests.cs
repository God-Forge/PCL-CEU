using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ModMinecraftHelpersTests {

    [Test]
    public async Task 规则为空时允许() {
        await Assert.That(PclRuleEvaluator.IsAllowed(null, "10.0.19045")).IsTrue();
    }

    [Test]
    public async Task 仅允许Windows和匹配的系统版本() {
        var allowWindows = JToken.Parse("""[{"action":"allow","os":{"name":"windows","version":"^10\\."}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(allowWindows, "10.0.19045")).IsTrue();
        await Assert.That(PclRuleEvaluator.IsAllowed(allowWindows, "6.1.7601")).IsFalse();

        var allowLinux = JToken.Parse("""[{"action":"allow","os":{"name":"linux"}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(allowLinux, "10.0.19045")).IsFalse();
    }

    [Test]
    public async Task 排除x86和ARM架构() {
        var x86 = JToken.Parse("""[{"action":"allow","os":{"name":"windows","arch":"x86"}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(x86, "10.0")).IsFalse();

        var arm = JToken.Parse("""[{"action":"allow","os":{"name":"windows","arch":"arm64"}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(arm, "10.0")).IsFalse();

        var x64 = JToken.Parse("""[{"action":"allow","os":{"name":"windows","arch":"x86_64"}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(x64, "10.0")).IsTrue();
    }

    [Test]
    public async Task 排除Demo用户和QuickPlay() {
        var demo = JToken.Parse("""[{"action":"allow","features":{"is_demo_user":false}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(demo, "10.0")).IsFalse();

        var quickPlay = JToken.Parse("""[{"action":"allow","features":{"has_quick_play_multiplayer":true}}]""");
        await Assert.That(PclRuleEvaluator.IsAllowed(quickPlay, "10.0")).IsFalse();
    }

    [Test]
    public async Task Maven路径保留分类器() {
        var path = LibraryResolver.GetLocalPath(
            "net.minecraftforge:forge:1.21.4-54.0.34:client", @"D:\mc");
        await Assert.That(path).IsEqualTo(
            @"D:\mc\libraries\net\minecraftforge\forge\1.21.4-54.0.34\forge-1.21.4-54.0.34-client.jar");

        var relative = LibraryResolver.GetLocalPath(
            "com.example:demo:1.0", @"D:\mc", withHead: false);
        await Assert.That(relative).IsEqualTo(@"com\example\demo\1.0\demo-1.0.jar");
    }

}
