using PCL.Core.Tools;

namespace PCL.Core.Tests;

public class PayloadServiceTests {

    [Test]
    public async Task Payload_打包解包往返() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-payload-" + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(root, "input");
        var output = Path.Combine(root, "output");
        var payload = Path.Combine(root, "test.payload");
        Directory.CreateDirectory(input);
        File.WriteAllText(Path.Combine(input, "hello.txt"), "payload-roundtrip");
        File.WriteAllText(Path.Combine(input, "test.json"), """{"id":"payload-test"}""");
        try {
            PayloadService.Encode(root, input, payload, "payload-test");
            await Assert.That(File.Exists(payload)).IsTrue();
            PayloadService.Decode(root, payload, output);
            await Assert.That(File.ReadAllText(Path.Combine(output, "hello.txt"))).IsEqualTo("payload-roundtrip");
            await Assert.That(File.Exists(Path.Combine(output, "test.json"))).IsTrue();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Payload_导入为实例() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-payload-import-" + Guid.NewGuid().ToString("N"));
        var input = Path.Combine(root, "input");
        var mc = Path.Combine(root, "mc");
        var payload = Path.Combine(root, "import.payload");
        Directory.CreateDirectory(input);
        Directory.CreateDirectory(mc);
        File.WriteAllText(Path.Combine(input, "payload-test.json"), """{"id":"payload-test","mainClass":"net.minecraft.client.main.Main"}""");
        File.WriteAllText(Path.Combine(input, "payload-test.jar"), "jar");
        try {
            PayloadService.Encode(root, input, payload, "payload-test");
            var name = PayloadService.Import(root, payload, mc);
            await Assert.That(name).IsEqualTo("payload-test");
            await Assert.That(File.Exists(Path.Combine(mc, "versions", name, name + ".json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(mc, "versions", name, name + ".jar"))).IsTrue();
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

}
