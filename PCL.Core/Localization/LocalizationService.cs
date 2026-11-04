namespace PCL.Core.Localization;

/// <summary>
/// 轻量本地化服务，先覆盖启动与通用提示，后续逐页补充资源键。
/// </summary>
public sealed class LocalizationService {

    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new(StringComparer.OrdinalIgnoreCase) {
        ["zh_CN"] = new() {
            ["app.title"] = "Plain Craft Launcher (PCL) CEU 修改版",
            ["common.ok"] = "确定",
            ["common.cancel"] = "取消",
            ["common.error"] = "错误",
            ["common.warning"] = "警告",
            ["common.info"] = "提示",
            ["common.loading"] = "加载中……",
            ["common.success"] = "操作完成",
            ["common.failed"] = "操作失败"
        },
        ["en_US"] = new() {
            ["app.title"] = "Plain Craft Launcher (PCL) CEU",
            ["common.ok"] = "OK",
            ["common.cancel"] = "Cancel",
            ["common.error"] = "Error",
            ["common.warning"] = "Warning",
            ["common.info"] = "Info",
            ["common.loading"] = "Loading...",
            ["common.success"] = "Done",
            ["common.failed"] = "Failed"
        }
    };

    private string _language = "zh_CN";

    public event Action<string>? LanguageChanged;

    public string Language {
        get => _language;
        set {
            if (string.IsNullOrWhiteSpace(value) || !Resources.ContainsKey(value)) return;
            if (_language == value) return;
            _language = value;
            LanguageChanged?.Invoke(value);
        }
    }

    public string Get(string key) {
        if (Resources.TryGetValue(_language, out var language) && language.TryGetValue(key, out var text)) return text;
        if (Resources["zh_CN"].TryGetValue(key, out var fallback)) return fallback;
        return key;
    }

    public string Format(string key, params object?[] arguments) {
        return string.Format(Get(key), arguments);
    }

}
