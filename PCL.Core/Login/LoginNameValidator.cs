namespace PCL.Core.Login;

/// <summary>
/// 离线登录玩家名的校验规则。VB 版在弹窗里通过 Validate 集合校验，这里收敛成一个纯函数。
/// </summary>
public static class LoginNameValidator {

    /// <summary>允许的最大长度。</summary>
    public const int MaxLength = 16;

    /// <summary>
    /// 校验玩家名。通过时返回 <c>null</c>，否则返回给用户看的错误提示。
    /// </summary>
    public static string? Validate(string? name) {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return "玩家名不能为空。";
        if (trimmed.Length > MaxLength) return $"玩家名不能超过 {MaxLength} 个字符。";
        if (trimmed.Any(char.IsControl)) return "玩家名不能包含控制字符。";
        return null;
    }

    public static bool IsValid(string? name) => Validate(name) is null;

    /// <summary>规范化玩家名：去掉首尾空白。</summary>
    public static string Normalize(string? name) => (name ?? "").Trim();

}
