using System.Diagnostics;

namespace PCL.Core.Minecraft;

/// <summary>
/// 一次游戏启动的会话：进程本体与其状态监视器。
/// </summary>
public sealed class LaunchSession : IDisposable {

    internal LaunchSession(Process process, GameProcessWatcher watcher) {
        Process = process;
        Watcher = watcher;
    }

    /// <summary>游戏进程。</summary>
    public Process Process { get; }

    /// <summary>进程状态、日志与窗口监视器。</summary>
    public GameProcessWatcher Watcher { get; }

    /// <summary>强制结束游戏。</summary>
    public void Kill() => Watcher.Kill();

    /// <summary>停止监视（不会结束进程）。</summary>
    public void Dispose() => Watcher.Dispose();

}
