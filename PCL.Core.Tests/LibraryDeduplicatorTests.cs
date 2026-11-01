using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LibraryDeduplicatorTests {

    private static ResolvedLibrary Library(string name, string version, bool native = false) {
        return new ResolvedLibrary {
            Name = name,
            LocalPath = "net/example/lib/" + version + "/lib-" + version + ".jar",
            IsNative = native
        };
    }

    [Test]
    public async Task 同版本重复时只保留一个() {
        var result = LibraryDeduplicator.Deduplicate([
            Library("com.example:lib", "1.0"),
            Library("com.example:lib", "1.0")
        ], false);
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 不同版本时保留较新版本() {
        var result = LibraryDeduplicator.Deduplicate([
            Library("com.example:lib", "1.9"),
            Library("com.example:lib", "1.10")
        ], false);
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].LocalPath).Contains("1.10");
    }

    [Test]
    public async Task 保留同名不同版本时输出两项() {
        var result = LibraryDeduplicator.Deduplicate([
            Library("com.example:lib", "1.9"),
            Library("com.example:lib", "1.10")
        ], true);
        await Assert.That(result.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Natives与普通库分别去重() {
        var result = LibraryDeduplicator.Deduplicate([
            Library("com.example:lib", "1.0"),
            Library("com.example:lib", "1.0", true)
        ], false);
        await Assert.That(result.Count).IsEqualTo(2);
    }

}
