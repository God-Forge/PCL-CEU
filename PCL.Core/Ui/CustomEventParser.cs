namespace PCL.Core.Ui;

/// <summary>
/// 自定义事件类型，与 VB 版 ModEvent 的 EventType 对齐。
/// </summary>
public enum CustomEventType {
    None,
    /// <summary>打开网页。</summary>
    OpenWebsite,
    /// <summary>打开文件或程序。</summary>
    OpenFile,
    /// <summary>打开帮助页面。</summary>
    OpenHelp,
    /// <summary>执行命令（出于安全原因默认不开放）。</summary>
    RunCommand,
    /// <summary>启动游戏。</summary>
    LaunchGame,
    /// <summary>复制文本。</summary>
    CopyText,
    RefreshHome,
    RefreshPage,
    RefreshHelp,
    DailyLuck,
    MemoryOptimize,
    CleanGarbage,
    /// <summary>弹出窗口。</summary>
    ShowWindow,
    /// <summary>弹出提示。</summary>
    ShowHint,
    /// <summary>切换页面。</summary>
    SwitchPage,
    ImportModpack,
    InstallModpack,
    DownloadFile,
    ModifySetting,
    WriteSetting,
    ModifyVariable,
    WriteVariable,
    JoinRoom,
    CheckUpdate
}

/// <summary>
/// 解析自定义事件的类型与参数。对应 VB 版 CustomEvent.Raise 的入参处理。
/// </summary>
public static class CustomEventParser {

    private static readonly Dictionary<string, CustomEventType> Aliases = new(StringComparer.OrdinalIgnoreCase) {
        ["打开网页"] = CustomEventType.OpenWebsite,
        ["OpenWebsite"] = CustomEventType.OpenWebsite,
        ["打开文件"] = CustomEventType.OpenFile,
        ["OpenFile"] = CustomEventType.OpenFile,
        ["打开帮助"] = CustomEventType.OpenHelp,
        ["OpenHelp"] = CustomEventType.OpenHelp,
        ["执行命令"] = CustomEventType.RunCommand,
        ["RunCommand"] = CustomEventType.RunCommand,
        ["启动游戏"] = CustomEventType.LaunchGame,
        ["LaunchGame"] = CustomEventType.LaunchGame,
        ["复制文本"] = CustomEventType.CopyText,
        ["复制"] = CustomEventType.CopyText,
        ["CopyText"] = CustomEventType.CopyText,
        ["Copy"] = CustomEventType.CopyText,
        ["刷新主页"] = CustomEventType.RefreshHome,
        ["刷新页面"] = CustomEventType.RefreshPage,
        ["刷新帮助"] = CustomEventType.RefreshHelp,
        ["今日人品"] = CustomEventType.DailyLuck,
        ["内存优化"] = CustomEventType.MemoryOptimize,
        ["清理垃圾"] = CustomEventType.CleanGarbage,
        ["弹出窗口"] = CustomEventType.ShowWindow,
        ["弹出提示"] = CustomEventType.ShowHint,
        ["提示"] = CustomEventType.ShowHint,
        ["切换页面"] = CustomEventType.SwitchPage,
        ["SwitchPage"] = CustomEventType.SwitchPage,
        ["导入整合包"] = CustomEventType.ImportModpack,
        ["安装整合包"] = CustomEventType.InstallModpack,
        ["下载文件"] = CustomEventType.DownloadFile,
        ["修改设置"] = CustomEventType.ModifySetting,
        ["写入设置"] = CustomEventType.WriteSetting,
        ["修改变量"] = CustomEventType.ModifyVariable,
        ["写入变量"] = CustomEventType.WriteVariable,
        ["加入房间"] = CustomEventType.JoinRoom,
        ["检查更新"] = CustomEventType.CheckUpdate,
        ["CheckUpdate"] = CustomEventType.CheckUpdate
    };

    /// <summary>把类型名（中文或英文别名）解析为枚举，未识别时返回 <see cref="CustomEventType.None"/>。</summary>
    public static CustomEventType ParseType(string? name) {
        if (string.IsNullOrWhiteSpace(name)) return CustomEventType.None;
        return Aliases.TryGetValue(name.Trim(), out var type) ? type : CustomEventType.None;
    }

    /// <summary>事件参数以竖线分隔，空参数会被丢弃。</summary>
    public static IReadOnlyList<string> SplitArguments(string? data) {
        if (string.IsNullOrWhiteSpace(data)) return [];
        return data.Split('|')
            .Select(argument => argument.Trim())
            .Where(argument => argument.Length > 0)
            .ToList();
    }

    /// <summary>
    /// 校验「打开网页」的目标。只接受 http/https 等带协议的地址，
    /// 与 VB 版「EventData 必须为网址」的检查一致。
    /// </summary>
    public static bool IsValidUrl(string? url, out string? error) {
        error = null;
        if (string.IsNullOrWhiteSpace(url)) {
            error = "事件数据为空，需要提供网址。";
            return false;
        }
        var normalized = url.Replace('\\', '/').Trim();
        if (!normalized.Contains("://", StringComparison.Ordinal)) {
            error = "需要提供完整的网址（包含协议，例如 https://）。";
            return false;
        }
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) {
            error = "网址格式不正确。";
            return false;
        }
        if (uri.Scheme is not ("http" or "https" or "file")) {
            error = "不支持的协议：" + uri.Scheme;
            return false;
        }
        return true;
    }

    /// <summary>出于安全考虑，这些类型默认不执行（需要用户显式确认的场景）。</summary>
    public static bool IsRestricted(CustomEventType type) => type is
        CustomEventType.RunCommand or
        CustomEventType.WriteSetting or
        CustomEventType.ModifySetting or
        CustomEventType.WriteVariable or
        CustomEventType.ModifyVariable;

}
