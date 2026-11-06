using System.Text;
using System.Text.RegularExpressions;

namespace PCL.Core.Update;

/// <summary>
/// 把更新检查结果整理成启动时可展示的一条公告。
/// 项目没有独立公告服务，这里直接以最新 Release 的说明作为公告来源。
/// </summary>
public static class AnnouncementFormatter {

    /// <summary>公告摘要的最大长度。</summary>
    public const int DefaultMaxLength = 300;

    /// <summary>行内粗体标记。</summary>
    private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    /// <summary>行内代码标记。</summary>
    private static readonly Regex CodePattern = new("\x60(.+?)\x60", RegexOptions.Compiled);

    /// <summary>Markdown 链接。</summary>
    private static readonly Regex LinkPattern = new(@"\[(.+?)\]\(.+?\)", RegexOptions.Compiled);

    /// <summary>
    /// 生成公告文本；没有更新时返回 <c>null</c>（不打扰用户）。
    /// </summary>
    public static string? Build(UpdateInfo? info, int maxLength = DefaultMaxLength) {
        if (info is null || !info.HasUpdate) return null;
        if (string.IsNullOrWhiteSpace(info.LatestVersion)) return null;
        var title = $"发现新版本 {info.LatestVersion}（当前 {info.CurrentVersion}）";
        var notes = Summarize(info.ReleaseNotes, maxLength);
        return string.IsNullOrWhiteSpace(notes) ? title : title + "：" + notes;
    }

    /// <summary>
    /// 抽取发布说明的纯文本摘要：去掉 Markdown 标记与多余空行，超长截断。
    /// </summary>
    public static string Summarize(string? notes, int maxLength = DefaultMaxLength) {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        var builder = new StringBuilder();
        foreach (var rawLine in notes.Replace("\r\n", "\n").Split('\n')) {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            // 去掉标题、引用与列表前缀
            line = line.TrimStart('#', '>', '-', '*', ' ').Trim();
            // 去掉行内 Markdown 标记
            line = BoldPattern.Replace(line, "$1");
            line = CodePattern.Replace(line, "$1");
            line = LinkPattern.Replace(line, "$1");
            if (line.Length == 0) continue;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(line);
            if (builder.Length >= maxLength) break;
        }
        var text = builder.ToString();
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "…";
    }

}
