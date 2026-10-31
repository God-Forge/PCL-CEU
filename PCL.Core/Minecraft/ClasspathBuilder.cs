namespace PCL.Core.Minecraft;

public sealed class ClasspathEntry {

    public string Name { get; init; } = "";
    public string LocalPath { get; init; } = "";
    public bool IsNative { get; init; }

}

/// <summary>
/// 构建 Minecraft 启动 classpath，保持 OptiFine 位于倒数第二位的 PCL 规则。
/// </summary>
public static class ClasspathBuilder {

    public static string Build(IEnumerable<ClasspathEntry> libraries, string separator = ";") {
        var classpath = new List<string>();
        string? optifine = null;
        foreach (var library in libraries) {
            if (library.IsNative) continue;
            if (library.Name == "optifine:OptiFine") {
                optifine = library.LocalPath;
            } else {
                classpath.Add(library.LocalPath);
            }
        }
        if (optifine is not null) {
            classpath.Insert(Math.Max(0, classpath.Count - 2), optifine);
        }
        return string.Join(separator, classpath);
    }

}
