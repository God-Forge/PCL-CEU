using PCL.Core.Audio;

namespace PCL.Core.Tests;

public class MusicDisplayFormatterTests {

    [Test]
    public async Task 曲目名去掉目录与扩展名() {
        await Assert.That(MusicDisplayFormatter.FormatTrack(@"C:\m\a.mp3")).IsEqualTo("a");
        await Assert.That(MusicDisplayFormatter.FormatTrack("b.ogg")).IsEqualTo("b");
    }

    [Test]
    public async Task 未播放时显示占位() {
        await Assert.That(MusicDisplayFormatter.FormatTrack(null)).IsEqualTo("未播放");
        await Assert.That(MusicDisplayFormatter.FormatTrack("")).IsEqualTo("未播放");
    }

    [Test]
    public async Task 状态与按钮文字() {
        await Assert.That(MusicDisplayFormatter.FormatState(MusicPlaybackState.Playing)).IsEqualTo("正在播放");
        await Assert.That(MusicDisplayFormatter.FormatState(MusicPlaybackState.Paused)).IsEqualTo("已暂停");
        await Assert.That(MusicDisplayFormatter.FormatState(MusicPlaybackState.Stopped)).IsEqualTo("未播放");
        await Assert.That(MusicDisplayFormatter.FormatToggleText(MusicPlaybackState.Playing)).IsEqualTo("暂停");
        await Assert.That(MusicDisplayFormatter.FormatToggleText(MusicPlaybackState.Paused)).IsEqualTo("播放");
    }

    [Test]
    public async Task 组合提示() {
        await Assert.That(MusicDisplayFormatter.FormatSummary(MusicPlaybackState.Playing, @"C:\m\a.mp3", true))
            .IsEqualTo("正在播放：a");
        await Assert.That(MusicDisplayFormatter.FormatSummary(MusicPlaybackState.Stopped, null, true))
            .IsEqualTo("已停止");
    }

    [Test]
    public async Task 没有音乐时返回空() {
        await Assert.That(MusicDisplayFormatter.FormatSummary(MusicPlaybackState.Playing, "a.mp3", false)).IsEqualTo("");
    }

}
