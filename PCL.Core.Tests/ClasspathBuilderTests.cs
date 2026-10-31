using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ClasspathBuilderTests {

    [Test]
    public async Task 跳过Natives并按PCL规则插入OptiFine() {
        var entries = new[] {
            new ClasspathEntry { Name = "a", LocalPath = "a.jar" },
            new ClasspathEntry { Name = "b", LocalPath = "b.jar" },
            new ClasspathEntry { Name = "native", LocalPath = "n.dll", IsNative = true },
            new ClasspathEntry { Name = "optifine:OptiFine", LocalPath = "optifine.jar" },
            new ClasspathEntry { Name = "c", LocalPath = "c.jar" }
        };
        var result = ClasspathBuilder.Build(entries);
        await Assert.That(result).IsEqualTo("a.jar;optifine.jar;b.jar;c.jar");
    }

    [Test]
    public async Task 支持自定义分隔符() {
        var entries = new[] {
            new ClasspathEntry { Name = "a", LocalPath = "a.jar" },
            new ClasspathEntry { Name = "b", LocalPath = "b.jar" }
        };
        await Assert.That(ClasspathBuilder.Build(entries, ":")).IsEqualTo("a.jar:b.jar");
    }

}
