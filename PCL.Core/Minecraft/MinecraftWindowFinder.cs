using System.Diagnostics;
using System.Text;
using PCL.Core.Native;

namespace PCL.Core.Minecraft;

/// <summary>
/// 枚举到的 Minecraft 窗口。
/// </summary>
/// <param name="Handle">窗口句柄。</param>
/// <param name="Title">窗口标题。</param>
/// <param name="IsLoaderWindow">是否为 Mod 加载器的前置窗口（此时游戏窗口尚未出现）。</param>
public readonly record struct MinecraftWindow(IntPtr Handle, string Title, bool IsLoaderWindow);

/// <summary>
/// 从所有顶层窗口中找出属于 Minecraft 进程的那个。
/// 依据窗口类名、可见性、标题与进程启动时间综合判断。
/// </summary>
public static class MinecraftWindowFinder {

    private const int SwMaximize = 3;

    private static readonly string[] AcceptedClassNames = ["GLFW30", "SDL_app", "LWJGL", "SunAwtFrame"];

    /// <summary>
    /// 查找游戏窗口。返回 <c>null</c> 表示尚未出现或无法访问。
    /// </summary>
    /// <param name="gameProcess">游戏进程。</param>
    public static MinecraftWindow? Find(Process gameProcess) {
        try {
            if (gameProcess.HasExited) return null;
            return Find(gameProcess.Id, gameProcess.StartTime);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "枚举 Minecraft 窗口进程失败");
            return null;
        }
    }

    /// <summary>
    /// 查找启动时间不早于 <paramref name="gameStartTime"/> 的 Minecraft 窗口。
    /// </summary>
    public static MinecraftWindow? Find(int gameProcessId, DateTime gameStartTime) {
        MinecraftWindow? result = null;
        NativeMethods.EnumWindowsProc callback = (handle, _) => {
            if (result is not null) return false;
            try {
                var className = GetClassName(handle);
                if (!AcceptedClassNames.Any(name => string.Equals(name, className, StringComparison.OrdinalIgnoreCase))) {
                    return true;
                }
                // SDL 会先创建隐藏窗口，初始化完成后才显示
                if (string.Equals(className, "SDL_app", StringComparison.OrdinalIgnoreCase)
                    && !NativeMethods.IsWindowVisible(handle)) {
                    return true;
                }
                var title = GetWindowText(handle);
                if (IsIgnorableTitle(title)) return true;
                NativeMethods.GetWindowThreadProcessId(handle, out var processId);
                if (processId != (uint)gameProcessId) {
                    if (!StartedNoEarlierThan((int)processId, gameStartTime)) return true;
                }
                result = new MinecraftWindow(handle, title, MinecraftLogRules.IsLoaderWindowTitle(title));
                return false;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "枚举窗口时出错");
                return true;
            }
        };
        NativeMethods.EnumWindows(callback, IntPtr.Zero);
        return result;
    }

    /// <summary>设置窗口标题。</summary>
    public static bool TrySetTitle(IntPtr handle, string title) {
        if (handle == IntPtr.Zero || string.IsNullOrEmpty(title)) return false;
        try {
            return NativeMethods.SetWindowText(handle, title);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "设置 Minecraft 窗口标题失败");
            return false;
        }
    }

    /// <summary>最大化窗口。</summary>
    public static bool Maximize(IntPtr handle) {
        if (handle == IntPtr.Zero) return false;
        try {
            return NativeMethods.ShowWindow(handle, SwMaximize);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "最大化 Minecraft 窗口失败");
            return false;
        }
    }

    private static bool IsIgnorableTitle(string title) {
        if (string.IsNullOrWhiteSpace(title)) return true;
        if (string.Equals(title, "SDL_app", StringComparison.Ordinal)) return true;
        if (string.Equals(title, "PopupMessageWindow", StringComparison.Ordinal)) return true;
        if (title.StartsWith("GLFW", StringComparison.Ordinal)) return true;
        return title.EndsWith("Hidden Utility Window", StringComparison.Ordinal);
    }

    private static bool StartedNoEarlierThan(int processId, DateTime threshold) {
        try {
            return Process.GetProcessById(processId).StartTime >= threshold;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            return false;
        }
    }

    private static string GetClassName(IntPtr handle) {
        var buffer = new StringBuilder(512);
        return NativeMethods.GetClassName(handle, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

    private static string GetWindowText(IntPtr handle) {
        var buffer = new StringBuilder(512);
        return NativeMethods.GetWindowText(handle, buffer, buffer.Capacity) > 0 ? buffer.ToString() : "";
    }

}
