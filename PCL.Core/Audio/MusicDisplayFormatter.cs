namespace PCL.Core.Audio;

/// <summary>
/// 音乐界面的文本格式化。与 VB 版 ModMusic 的按钮提示文案对应。
/// </summary>
public static class MusicDisplayFormatter {

    /// <summary>当前播放曲目的显示名（去目录、去扩展名）。</summary>
    public static string FormatTrack(string? path) {
        if (string.IsNullOrWhiteSpace(path)) return "未播放";
        var name = Path.GetFileNameWithoutExtension(path);
        return string.IsNullOrWhiteSpace(name) ? "未播放" : name;
    }

    /// <summary>播放状态的短描述。</summary>
    public static string FormatState(MusicPlaybackState state) => state switch {
        MusicPlaybackState.Playing => "正在播放",
        MusicPlaybackState.Paused => "已暂停",
        _ => "未播放"
    };

    /// <summary>按钮上的动作文字。</summary>
    public static string FormatToggleText(MusicPlaybackState state) =>
        state == MusicPlaybackState.Playing ? "暂停" : "播放";

    /// <summary>
    /// 组合成一行提示，例如「正在播放：xxx」；没有音乐时返回空串。
    /// </summary>
    public static string FormatSummary(MusicPlaybackState state, string? currentTrack, bool hasMusic) {
        if (!hasMusic) return "";
        var track = FormatTrack(currentTrack);
        return state == MusicPlaybackState.Stopped ? "已停止" : $"{FormatState(state)}：{track}";
    }

}
