namespace PCL.Core.Minecraft;

/// <summary>
/// 进程退出后的判定结果。
/// </summary>
public enum MinecraftExitVerdict {
    /// <summary>正常退出。</summary>
    Normal,
    /// <summary>窗口尚未出现就退出，视为启动失败或崩溃。</summary>
    CrashedBeforeWindow,
    /// <summary>已经进入游戏但返回了非 0 退出码。</summary>
    CrashedAbnormal
}

/// <summary>
/// 根据监视状态与退出码判断游戏是否崩溃。
/// </summary>
public static class MinecraftExitClassifier {

    /// <summary>该年份之前的版本允许非 0 退出码（旧版关闭时可能返回非 0）。</summary>
    public const int LegacyReleaseYear = 2012;

    public static MinecraftExitVerdict Classify(MinecraftGameState state, int exitCode, int? releaseYear) {
        if (state == MinecraftGameState.Crashed) return MinecraftExitVerdict.CrashedAbnormal;
        if (state == MinecraftGameState.Loading) return MinecraftExitVerdict.CrashedBeforeWindow;
        if (state != MinecraftGameState.Running || exitCode == 0) return MinecraftExitVerdict.Normal;
        // 取不到发布年份时按现代版本处理：已进入游戏却非 0 退出，基本就是崩溃。
        return releaseYear is null || releaseYear >= LegacyReleaseYear
            ? MinecraftExitVerdict.CrashedAbnormal
            : MinecraftExitVerdict.Normal;
    }

    /// <summary>判定结果是否需要走崩溃分析。</summary>
    public static bool IsCrash(MinecraftExitVerdict verdict) => verdict != MinecraftExitVerdict.Normal;

}
