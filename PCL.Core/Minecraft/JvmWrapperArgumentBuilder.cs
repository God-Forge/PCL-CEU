namespace PCL.Core.Minecraft;

/// <summary>
/// 构建 JLW 与 LUA 的 JVM 参数。
/// </summary>
public static class JvmWrapperArgumentBuilder {

    public static IReadOnlyList<string> BuildJLW(bool use, int javaMajorVersion) {
        if (!use) return Array.Empty<string>();
        var arguments = new List<string>();
        if (javaMajorVersion >= 9) {
            arguments.Add("--add-exports");
            arguments.Add("cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
        }
        arguments.Add("-Doolloo.jlw.tmpdir=${pure_directory}");
        return arguments;
    }

    public static string BuildLUA(string agentPath) {
        return "-javaagent:\"" + agentPath + "\"";
    }

}
