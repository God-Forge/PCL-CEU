using PCL.Core.Audio;

namespace PCL.Core.Tests;

public class MusicPlaylistTests {

    private static string[] Files3 => [@"C:\m\a.mp3", @"C:\m\b.mp3", @"C:\m\c.mp3"];

    [Test]
    public async Task 过滤非音乐扩展名() {
        await Assert.That(MusicPlaylist.IsMusicFile(@"C:\m\a.mp3")).IsTrue();
        await Assert.That(MusicPlaylist.IsMusicFile(@"C:\m\a.ogg")).IsTrue();
        await Assert.That(MusicPlaylist.IsMusicFile(@"C:\m\cover.jpg")).IsFalse();
        await Assert.That(MusicPlaylist.IsMusicFile(@"C:\m\lyric.lrc")).IsFalse();
        await Assert.That(MusicPlaylist.IsMusicFile("")).IsFalse();
    }

    [Test]
    public async Task 创建列表时去重并忽略空项() {
        var playlist = MusicPlaylist.FromFiles([@"C:\m\a.mp3", @"C:\m\A.MP3", "", @"C:\m\b.mp3"]);
        await Assert.That(playlist.AllFiles.Count).IsEqualTo(2);
    }

    [Test]
    public async Task 顺序播放按列表顺序() {
        var playlist = MusicPlaylist.FromFiles(Files3);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[0]);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[1]);
        await Assert.That(playlist.Current).IsEqualTo(Files3[1]);
    }

    [Test]
    public async Task 队列用尽后自动重建() {
        var playlist = MusicPlaylist.FromFiles(Files3);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[0]);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[1]);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[2]);
        // 播完最后一首后队列重建，且刚播的那首不会立刻重复
        var fourth = playlist.Next();
        await Assert.That(fourth).IsNotNull();
        await Assert.That(fourth).IsNotEqualTo(Files3[2]);
    }

    [Test]
    public async Task 随机播放使用同一种子可复现() {
        var first = MusicPlaylist.FromFiles(Files3, shuffle: true, random: new Random(42));
        var second = MusicPlaylist.FromFiles(Files3, shuffle: true, random: new Random(42));
        var firstOrder = new[] { first.Next(), first.Next(), first.Next() };
        var secondOrder = new[] { second.Next(), second.Next(), second.Next() };
        await Assert.That(string.Join("|", firstOrder)).IsEqualTo(string.Join("|", secondOrder));
    }

    [Test]
    public async Task 随机播放仍包含全部文件() {
        var playlist = MusicPlaylist.FromFiles(Files3, shuffle: true, random: new Random(7));
        var played = new List<string?>();
        for (var i = 0; i < 3; i++) played.Add(playlist.Next());
        await Assert.That(played.Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task 空列表返回空() {
        var playlist = MusicPlaylist.FromFiles([]);
        await Assert.That(playlist.IsEmpty).IsTrue();
        await Assert.That(playlist.Next()).IsNull();
    }

    [Test]
    public async Task 目录不存在时返回空列表() {
        var playlist = MusicPlaylist.FromFolder(@"Z:\不存在的目录\musics");
        await Assert.That(playlist.IsEmpty).IsTrue();
    }

    [Test]
    public async Task 重置队列后从头开始() {
        var playlist = MusicPlaylist.FromFiles(Files3);
        await Assert.That(playlist.Next()).IsEqualTo(Files3[0]);
        playlist.Reset();
        // 重建后整个列表重新入队，且刚播过的那首不会被排到最前面
        await Assert.That(playlist.Queue.Count).IsEqualTo(3);
        await Assert.That(playlist.Queue[0]).IsNotEqualTo(Files3[0]);
    }

}
