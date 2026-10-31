namespace PCL.Core.Minecraft;

/// <summary>
/// 构建自动进入服务器的游戏参数。
/// </summary>
public static class ServerArgumentBuilder {

    public static IReadOnlyList<string> Build(string? server, bool useQuickPlay) {
        if (string.IsNullOrWhiteSpace(server)) return Array.Empty<string>();
        if (useQuickPlay) return ["--quickPlayMultiplayer", server!];

        var separator = server!.IndexOf(':');
        if (separator >= 0) {
            return ["--server", server.Substring(0, separator), "--port", server.Substring(separator + 1)];
        }
        return ["--server", server, "--port", "25565"];
    }

}
