namespace PCL.Core.Minecraft;

public sealed class ExitCodeInfo {

    public int Code { get; init; }
    public string Description { get; init; } = "";
    public string Suggestion { get; init; } = "";

}

/// <summary>
/// 识别常见 Windows 进程退出码。
/// </summary>
public static class ExitCodeAnalyzer {

    private static readonly Dictionary<int, ExitCodeInfo> Codes = new() {
        [1] = new ExitCodeInfo { Description = "通用错误", Suggestion = "请查看 latest.log 或 crash-reports 中的详细异常。" },
        [-1073741819] = new ExitCodeInfo { Description = "访问冲突（0xC0000005）", Suggestion = "通常是显卡驱动、原生库或 Mod 访问了非法内存。请更新显卡驱动、移除最近添加的 Mod，并确认 Java 位数与系统一致。" },
        [-1073740791] = new ExitCodeInfo { Description = "栈缓冲区溢出（0xC0000409）", Suggestion = "可能存在不兼容的 Mod 或原生库，请更新/移除最近添加的 Mod。" },
        [-1073741571] = new ExitCodeInfo { Description = "栈溢出（0xC00000FD）", Suggestion = "请移除递归过深的 Mod，或在 JVM 参数中加入 -Xss4m。" },
        [-1073741515] = new ExitCodeInfo { Description = "缺少 DLL（0xC0000135）", Suggestion = "请安装/修复 Visual C++ 运行库，并确认 Java 与系统位数一致。" },
        [-1073741701] = new ExitCodeInfo { Description = "映像格式错误（0xC000007B）", Suggestion = "通常是 32/64 位不匹配或 DLL 损坏。请改用与系统一致的 64 位 Java，并修复运行库。" },
        [-1073741795] = new ExitCodeInfo { Description = "非法指令（0xC000001D）", Suggestion = "请检查 CPU 指令集兼容性，更新 Java 与显卡驱动。" },
        [-1073741676] = new ExitCodeInfo { Description = "整数除零（0xC0000094）", Suggestion = "可能是 Mod 计算错误，请更新或移除最近添加的 Mod。" },
        [-1073741674] = new ExitCodeInfo { Description = "特权指令（0xC0000096）", Suggestion = "请检查原生库或反作弊/杀毒软件是否拦截。" },
        [-1073741510] = new ExitCodeInfo { Description = "控制台中断（0xC000013A）", Suggestion = "进程被 Ctrl+C 或外部程序终止。" },
        [-1073741502] = new ExitCodeInfo { Description = "DLL 初始化失败（0xC0000142）", Suggestion = "请修复 Visual C++ 运行库，并检查杀毒软件是否拦截。" }
    };

    public static ExitCodeInfo? Analyze(int exitCode) {
        if (exitCode == 0) return null;
        return Codes.TryGetValue(exitCode, out var info) ? info : null;
    }

}
