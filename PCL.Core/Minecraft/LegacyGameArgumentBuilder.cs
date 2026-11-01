namespace PCL.Core.Minecraft;

/// <summary>
/// 构建旧版 Minecraft 的 minecraftArguments 参数串。
/// </summary>
public static class LegacyGameArgumentBuilder {

    public static string Build(string arguments) {
        return arguments + " --height ${resolution_height} --width ${resolution_width}";
    }

}
