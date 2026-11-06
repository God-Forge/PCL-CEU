namespace PCL.Core.Launch;

/// <summary>启动前命令可用的上下文。</summary>
public sealed class LaunchCommandContext {

    public string LauncherPath { get; init; } = "";
    public string MinecraftPath { get; init; } = "";
    public string InstancePath { get; init; } = "";
    public string PlayerName { get; init; } = "";
    public string JavaPath { get; init; } = "";

}

/// <summary>
/// 展开启动命令里的占位符。对应 VB 版 ArgumentReplace 的一个精简子集，
/// 只保留启动前后命令真正会用到的几项。
/// </summary>
public static class LaunchCommandExpander {

    public static string Expand(string? command, LaunchCommandContext? context) {
        if (string.IsNullOrWhiteSpace(command)) return "";
        if (context is null) return command;
        return command
            .Replace("{path}", context.LauncherPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{mc_path}", context.MinecraftPath, StringComparison.OrdinalIgnoreCase)
            .Replace("{version_path}", context.InstancePath, StringComparison.OrdinalIgnoreCase)
            .Replace("{indie}", context.InstancePath, StringComparison.OrdinalIgnoreCase)
            .Replace("{user}", context.PlayerName, StringComparison.OrdinalIgnoreCase)
            .Replace("{java}", context.JavaPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>命令里是否还有未展开的占位符。</summary>
    public static bool HasUnresolvedPlaceholder(string? command) =>
        !string.IsNullOrWhiteSpace(command) && command.Contains('{') && command.Contains('}');

}
