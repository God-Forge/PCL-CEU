namespace PCL.Core.Minecraft;

/// <summary>
/// 将崩溃原因映射为可执行的修复建议。
/// </summary>
public static class CrashFixSuggestionService {

    private static readonly Dictionary<string, string> Suggestions = new(StringComparer.Ordinal) {
        ["堆栈溢出"] = "检测到堆栈溢出。请更新或移除最近添加的 Mod，必要时在 JVM 参数中加入 -Xss4m 增大线程栈。",
        ["原生库加载失败"] = "请确认 Java 位数与系统一致，删除损坏的 natives 文件夹后重新启动；若仍失败，请重新安装对应 Mod 或启动器依赖。",
        ["文件访问失败"] = "请关闭占用该文件的程序，检查文件是否存在以及杀毒软件/权限是否拦截，然后重试。",
        ["启动参数过长"] = "Windows 命令行长度有限。请减少 JVM/游戏参数，缩短实例路径，或移除重复参数后重试。",
        ["存档或区块损坏"] = "请从世界备份恢复；若没有备份，可删除对应的 region 文件让游戏重新生成该区块。",
        ["资源包或光影加载失败"] = "请移除最近添加的资源包或光影，确认光影版本与游戏/加载器兼容，并更新显卡驱动。",
        ["Mod版本或API不兼容"] = "请更新或移除不兼容的 Mod，确认所有 Mod 与当前游戏版本、Forge/Fabric/NeoForge 版本匹配。",
        ["内存不足"] = "请关闭其他程序，降低渲染距离/材质，或在启动设置中增加游戏内存；同时确认使用的是 64 位 Java。",
        ["Mod重复安装"] = "请删除 mods 文件夹中重复的 Mod 文件，每个 Mod 只保留一个版本。",
        ["Mod缺少前置或MC版本错误"] = "请根据日志中的依赖列表安装缺失的前置 Mod，并确认前置版本与当前游戏版本匹配。",
        ["Mod互不兼容"] = "请根据日志列出的冲突 Mod 逐个禁用排查，并更新到兼容版本。",
        ["使用32位Java导致JVM无法分配足够多的内存"] = "请改用 64 位 Java，并在启动设置中重新分配内存。"
    };

    public static string? GetSuggestion(string reason) {
        return Suggestions.TryGetValue(reason, out var suggestion) ? suggestion : null;
    }

}
