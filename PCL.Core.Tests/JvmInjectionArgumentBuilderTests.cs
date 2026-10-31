using System.Text;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JvmInjectionArgumentBuilderTests {

    [Test]
    public async Task 构建统一通行证参数() {
        var argument = JvmInjectionArgumentBuilder.BuildNide("https://example.com/auth");
        await Assert.That(argument).IsEqualTo("-javaagent:\"${pure_directory}\\nide8auth.jar\"=\"https://example.com/auth\"");
    }

    [Test]
    public async Task 构建Authlib参数并预取Base64() {
        var arguments = JvmInjectionArgumentBuilder.BuildAuthlib("https://example.com/api", "{\"a\":1}");
        await Assert.That(arguments.Count).IsEqualTo(3);
        await Assert.That(arguments[0]).IsEqualTo("-javaagent:\"${pure_directory}\\authlib-injector.jar\"=\"https://example.com/api\"");
        await Assert.That(arguments[1]).IsEqualTo("-Dauthlibinjector.side=client");
        await Assert.That(arguments[2]).IsEqualTo("-Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"a\":1}")));
    }

}
