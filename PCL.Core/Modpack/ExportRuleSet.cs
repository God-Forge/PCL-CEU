using System.Text;
using System.Text.RegularExpressions;

namespace PCL.Core.Modpack;

/// <summary>
/// 导出规则集：解析 VB 版 ExportOption 里的 Rules 字符串。
/// 语法：以 <c>|</c> 分隔，<c>!</c> 前缀表示排除，其余为包含；
/// 以 <c>/</c> 结尾表示目录前缀，<c>*</c> 为通配。
/// </summary>
public sealed class ExportRuleSet {

    private readonly string[] _includes;
    private readonly string[] _excludes;

    private ExportRuleSet(string[] includes, string[] excludes) {
        _includes = includes;
        _excludes = excludes;
    }

    public static readonly ExportRuleSet Empty = new([], []);

    public IReadOnlyList<string> Includes => _includes;
    public IReadOnlyList<string> Excludes => _excludes;
    public bool IsEmpty => _includes.Length == 0 && _excludes.Length == 0;

    /// <summary>解析一条（或合并后的）规则字符串。</summary>
    public static ExportRuleSet Parse(string? rules) {
        if (string.IsNullOrWhiteSpace(rules)) return Empty;
        var includes = new List<string>();
        var excludes = new List<string>();
        foreach (var raw in rules.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            if (raw.Length == 0) continue;
            if (raw.StartsWith('!')) {
                var pattern = raw[1..].Trim();
                if (pattern.Length > 0) excludes.Add(pattern);
            } else {
                includes.Add(raw);
            }
        }
        return new ExportRuleSet(includes.ToArray(), excludes.ToArray());
    }

    /// <summary>合并多个规则集（用于汇总多个勾选项）。</summary>
    public static ExportRuleSet Merge(IEnumerable<ExportRuleSet?> sets) {
        var includes = new List<string>();
        var excludes = new List<string>();
        foreach (var set in sets) {
            if (set is null) continue;
            includes.AddRange(set._includes);
            excludes.AddRange(set._excludes);
        }
        return new ExportRuleSet(
            includes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            excludes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    /// <summary>
    /// 判断相对路径是否应被打包：先看排除，再看包含，都不匹配则不包含。
    /// </summary>
    public bool ShouldInclude(string? relativePath) {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        foreach (var pattern in _excludes) {
            if (Matches(pattern, normalized)) return false;
        }
        foreach (var pattern in _includes) {
            if (Matches(pattern, normalized)) return true;
        }
        return false;
    }

    /// <summary>
    /// 规则匹配：<c>dir/</c> 匹配该目录下所有内容；含 <c>*</c> 的按通配匹配；
    /// 其余按完整路径或前缀匹配。
    /// </summary>
    public static bool Matches(string pattern, string normalizedPath) {
        if (string.IsNullOrEmpty(pattern)) return false;
        var target = normalizedPath.Replace('\\', '/');
        if (pattern.EndsWith('/')) {
            var prefix = pattern.TrimEnd('/');
            return target.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                   || target.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.Contains('*') || pattern.Contains('?')) {
            return GlobToRegex(pattern).IsMatch(target);
        }
        return target.Equals(pattern, StringComparison.OrdinalIgnoreCase)
               || target.StartsWith(pattern + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static Regex GlobToRegex(string pattern) {
        var builder = new StringBuilder("^");
        foreach (var character in pattern) {
            builder.Append(character switch {
                '*' => "[^/]*",
                '?' => "[^/]",
                _ => Regex.Escape(character.ToString())
            });
        }
        builder.Append('$');
        return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>把规则集描述成一行，便于日志排查。</summary>
    public string Describe() =>
        IsEmpty ? "（空规则）" : $"包含 {_includes.Length} 条、排除 {_excludes.Length} 条";

}
