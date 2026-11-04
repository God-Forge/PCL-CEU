using System.Windows.Media;
using MeloongCore;
using PCL.Core.Audio;

namespace PCL.App.Audio;

/// <summary>
/// 基于 WPF <see cref="MediaPlayer"/> 的背景音乐播放器。
/// 由 VB 版 ModMusic 的播放部分迁移而来，省去 NAudio 原生依赖。
/// </summary>
public sealed class MusicPlayerService : IDisposable {

    private readonly MediaPlayer _player = new();
    private MusicPlaylist? _playlist;
    private int _volume = 60;
    private bool _disposed;

    public MusicPlayerService() {
        _player.MediaEnded += (_, _) => Next();
        _player.MediaFailed += (_, args) => {
            // 单个文件解码失败不应中断整个播放列表
            Logger.Warn(args.ErrorException, "背景音乐播放失败：" + _playlist?.Current);
            Next();
        };
    }

    /// <summary>当前状态。</summary>
    public MusicPlaybackState State { get; private set; } = MusicPlaybackState.Stopped;

    /// <summary>当前播放的文件。</summary>
    public string? Current => _playlist?.Current;

    /// <summary>是否有可播放的音乐。</summary>
    public bool HasMusic => _playlist is { IsEmpty: false };

    /// <summary>音量，取值 0 ~ 100。</summary>
    public int Volume {
        get => _volume;
        set {
            _volume = Math.Clamp(value, 0, 100);
            _player.Volume = _volume / 100.0;
        }
    }

    public event Action<MusicPlaybackState>? StateChanged;
    public event Action<string>? TrackChanged;

    /// <summary>载入播放列表，可选立即开始播放。</summary>
    public void Load(MusicPlaylist playlist, bool autoPlay = true) {
        _playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
        if (!autoPlay || playlist.IsEmpty) return;
        Next();
    }

    /// <summary>继续或开始播放。</summary>
    public void Resume() {
        if (_playlist is null || _playlist.IsEmpty) return;
        if (string.IsNullOrEmpty(_playlist.Current)) {
            Next();
            return;
        }
        _player.Play();
        SetState(MusicPlaybackState.Playing);
    }

    public void Pause() {
        if (State != MusicPlaybackState.Playing) return;
        _player.Pause();
        SetState(MusicPlaybackState.Paused);
    }

    /// <summary>播放下一首。</summary>
    public void Next() {
        if (_playlist is null) return;
        var next = _playlist.Next();
        if (next is null) {
            Stop();
            return;
        }
        try {
            _player.Open(new Uri(next, UriKind.Absolute));
            _player.Play();
            SetState(MusicPlaybackState.Playing);
            TrackChanged?.Invoke(next);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "无法播放背景音乐：" + next);
            SetState(MusicPlaybackState.Stopped);
        }
    }

    public void Stop() {
        _player.Stop();
        _player.Close();
        SetState(MusicPlaybackState.Stopped);
    }

    /// <summary>暂停与继续之间切换，返回切换后的状态。</summary>
    public MusicPlaybackState Toggle() {
        if (State == MusicPlaybackState.Playing) Pause();
        else Resume();
        return State;
    }

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        try {
            _player.Stop();
            _player.Close();
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "关闭背景音乐播放器失败");
        }
    }

    private void SetState(MusicPlaybackState state) {
        if (State == state) return;
        State = state;
        try {
            StateChanged?.Invoke(state);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理背景音乐状态变更时出错");
        }
    }

}
