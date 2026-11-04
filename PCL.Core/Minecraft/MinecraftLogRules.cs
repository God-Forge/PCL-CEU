namespace PCL.Core.Minecraft;

/// <summary>
/// 游戏启动阶段，由控制台输出逐级推进。
/// </summary>
public enum MinecraftLaunchProgress {
    None = 0,
    FirstLog = 1,
    UserSet = 2,
    LwjglReady = 3,
    OpenAlReady = 4,
    TexturesReady = 5
}

/// <summary>
/// 从单行游戏日志中识别出的关键事件。
/// </summary>
public enum MinecraftLogSignal {
    None,
    CloseRequested,
    CrashDetected
}

/// <summary>
/// Minecraft 控制台输出的识别规则。
/// 这些规则直接决定启动进度与崩溃判定，保持为纯函数以便测试。
/// </summary>
public static class MinecraftLogRules {

    /// <summary>[CHAT] 行不参与关闭与崩溃判定，避免玩家聊天误触发。</summary>
    public static bool IsChatLine(string line) => line.Contains("[CHAT]", StringComparison.Ordinal);

    /// <summary>
    /// 按 VB 版顺序推进启动阶段：同一行最多推进一级。
    /// </summary>
    public static MinecraftLaunchProgress AdvanceProgress(string line, MinecraftLaunchProgress current) {
        if (current < MinecraftLaunchProgress.FirstLog) return MinecraftLaunchProgress.FirstLog;
        if (current < MinecraftLaunchProgress.UserSet && line.Contains("Setting user:", StringComparison.Ordinal)) {
            return MinecraftLaunchProgress.UserSet;
        }
        if (current < MinecraftLaunchProgress.LwjglReady && line.Contains("lwjgl version", StringComparison.OrdinalIgnoreCase)) {
            return MinecraftLaunchProgress.LwjglReady;
        }
        if (current < MinecraftLaunchProgress.OpenAlReady
            && (line.Contains("OpenAL initialized", StringComparison.Ordinal)
                || line.Contains("Starting up SoundSystem", StringComparison.Ordinal))) {
            return MinecraftLaunchProgress.OpenAlReady;
        }
        if (current < MinecraftLaunchProgress.TexturesReady
            && ((line.Contains("Created", StringComparison.Ordinal)
                 && line.Contains("textures", StringComparison.Ordinal)
                 && line.Contains("-atlas", StringComparison.Ordinal))
                || line.Contains("Found animation info", StringComparison.Ordinal))) {
            return MinecraftLaunchProgress.TexturesReady;
        }
        return current;
    }

    /// <summary>
    /// 识别正常的关闭请求与崩溃日志。
    /// </summary>
    public static MinecraftLogSignal DetectSignal(string line) {
        if (string.IsNullOrEmpty(line) || IsChatLine(line)) return MinecraftLogSignal.None;
        if (line.Contains("Someone is closing me!", StringComparison.Ordinal)
            || line.Contains("Restarting Minecraft with command", StringComparison.Ordinal)) {
            return MinecraftLogSignal.CloseRequested;
        }
        if (line.Contains("Crash report saved to", StringComparison.Ordinal)
            || line.Contains("This crash report has been saved to:", StringComparison.Ordinal)
            || line.Contains("Could not save crash report to", StringComparison.Ordinal)) {
            return MinecraftLogSignal.CrashDetected;
        }
        if (line.Contains("/ERROR]: Unable to launch", StringComparison.Ordinal)
            || line.Contains("An exception was thrown, the game will display an error screen and halt.", StringComparison.Ordinal)) {
            return MinecraftLogSignal.CrashDetected;
        }
        return MinecraftLogSignal.None;
    }

    /// <summary>
    /// 判断窗口标题是否属于 Mod 加载器（此时游戏窗口还没出现）。
    /// </summary>
    public static bool IsLoaderWindowTitle(string title) {
        if (string.IsNullOrWhiteSpace(title)) return false;
        return title.StartsWith("FML", StringComparison.Ordinal)
               || title.StartsWith("Quilt Loader", StringComparison.Ordinal);
    }

    /// <summary>
    /// 根据启动阶段估算加载进度：最多到 0.9，窗口出现后由上层补到 0.95。
    /// </summary>
    public static double EstimateProgress(MinecraftLaunchProgress progress, bool windowAppeared) {
        if (windowAppeared || progress == MinecraftLaunchProgress.TexturesReady) return 0.95;
        return Math.Min((int)progress, 3) / 3.0 * 0.9;
    }

}
