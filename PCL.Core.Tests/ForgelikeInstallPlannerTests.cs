using System.IO.Compression;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class ForgelikeInstallPlannerTests {

    private sealed class FakeFetcher(Dictionary<string, string> data) : IStringFetcher {

        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
            return Task.FromResult(data.TryGetValue(url, out var value)
                ? value
                : throw new HttpRequestException("Unexpected URL: " + url));
        }

    }

    private static string CreateInstaller(string profile, string version) {
        var path = Path.Combine(Path.GetTempPath(), "pcl-forgelike-" + Guid.NewGuid().ToString("N") + ".jar");
        using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        WriteEntry(archive, "install_profile.json", profile);
        WriteEntry(archive, "version.json", version);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content) {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    [Test]
    public async Task 合并安装器元数据并补充Mappings() {
        var installer = CreateInstaller("""
        {
          "spec": 1,
          "libraries": [
            {
              "name": "net.minecraftforge:forge:1.20.1-47.2.0",
              "downloads": {
                "artifact": {
                  "path": "net/minecraftforge/forge/1.20.1-47.2.0/forge-1.20.1-47.2.0.jar",
                  "url": "https://example.com/forge.jar"
                }
              }
            },
            { "name": "com.example:profile-lib:1.0" }
          ],
          "data": {
            "MOJMAPS": { "client": "[net.minecraft:client:1.20.1:mappings@txt]" }
          }
        }
        """, """
        {
          "mainClass": "cpw.mods.bootstraplauncher.BootstrapLauncher",
          "libraries": [ { "name": "com.example:version-lib:2.0" } ]
        }
        """);
        try {
            const string vanillaUrl = "https://example.com/1.20.1.json";
            var fetcher = new FakeFetcher(new Dictionary<string, string> {
                [vanillaUrl] = """
                {
                  "downloads": {
                    "client_mappings": {
                      "url": "https://example.com/client.txt",
                      "sha1": "abc",
                      "size": 12
                    }
                  }
                }
                """
            });
            var plan = await ForgelikeInstallPlanner.CreatePlanAsync(
                installer, "Forge", "1.20.1", "47.2.0", fetcher, [vanillaUrl]);
            var libraries = plan.Json["libraries"]!.ToObject<List<JObject>>()!;
            var names = libraries.Select(library => library["name"]?.ToString()).ToList();

            await Assert.That(plan.Json["mainClass"]?.ToString()).IsEqualTo("cpw.mods.bootstraplauncher.BootstrapLauncher");
            await Assert.That(names.Contains("net.minecraftforge:forge:1.20.1-47.2.0")).IsFalse();
            await Assert.That(names.Contains("com.example:profile-lib:1.0")).IsTrue();
            await Assert.That(names.Contains("com.example:version-lib:2.0")).IsTrue();
            await Assert.That(names.Contains("net.minecraft:client:1.20.1:mappings")).IsTrue();

            var mappings = libraries.Single(library => library["name"]?.ToString() == "net.minecraft:client:1.20.1:mappings");
            await Assert.That(mappings["downloads"]!["artifact"]!["path"]!.ToString()).EndsWith(".txt");
            await Assert.That(mappings["downloads"]!["artifact"]!["url"]!.ToString()).IsEqualTo("https://example.com/client.txt");
        } finally {
            File.Delete(installer);
        }
    }

}
